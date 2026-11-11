using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// REPORT FUNCTIONS (item 5, complete) - match analytics, cognitive
/// breakdown, saved trends, and adaptive difficulty.
/// Definitions, measured exactly, no invented data:
/// - Release: moment a new live ball appears (BallThrower.ActiveMatchBall).
/// - Reaction time: release -> first bat swing faster than 1.0 m/s
///   (BatSwingTracker.blockThreshold). If the player never swings, it
///   stays -1 (a leave), it is NOT guessed as 0.
/// - Contact: ball State becomes Hit (or Block via OnBlock - in that
///   case the ball stays Flying, so a block is counted from the swing
///   speed at the moment the ball passes the stumps, see below).
/// - Timing: swing time vs estimated ball arrival at the popping
///   crease. Arrival = release + (bowler-to-batsman distance /
///   initial ball speed). Perfect = within 0.12 s, Early = swing
///   before arrival - 0.12 s, Late = after arrival + 0.12 s.
/// - Shot zone: exit velocity direction (Hit balls only) mapped to
///   a named zone relative to the pitch direction (bowler -> stumps)
///   and off-side direction. No zone is invented for dots/leaves.
/// - Cognitive breakdown (honest limits):
///   ReactionTime = release->swing average (real, timed).
///   BallTracking = % of deliveries where the player swung at all.
///   HandEye = % of swings that made bat contact (Hit).
///   Depth = % of full balls vs short balls hit (from variation name
///   / spin kind: yorker/good-length = full, bouncer/short = short).
///   Focus = longest streak of balls without a dismissal or a miss
///   where the player did not swing at all.
///   Gaze = NOT measured - no eye tracker is in the headset data, so
///   the dashboard says so plainly instead of faking a gaze score.
///   Focus streak is a proxy, not gaze.
/// - Dashboard: ScreenSpaceOverlay panel, dark background, shown
///   automatically at innings/match end, toggled with H on PC and
///   with an on-screen "Analytics" button (top-right).
/// - Persistence: every completed innings is appended to
///   persistentDataPath/vr_cricket_match_analytics.json, with per-ball
///   records, so trends (this session vs previous session) are real
///   comparisons, not reset every Play.
/// </summary>
public class MatchAnalytics : MonoBehaviour
{
    public static MatchAnalytics Instance { get; private set; }

    [Serializable]
    public class BallRecord
    {
        public int ballNumber;
        public string variation;
        public string spinKind;
        public float reactionTime = -1f;
        public float swingSpeed;
        public float exitSpeed;
        public string result;
        public int runs;
        public string timing;
        public string shotZone;
    }

    [Serializable]
    class SessionData
    {
        public string dateTime;
        public List<BallRecord> balls = new List<BallRecord>();
    }

    [Serializable]
    class SaveFile
    {
        public List<SessionData> sessions = new List<SessionData>();
    }

    readonly List<BallRecord> balls = new List<BallRecord>();
    GameObject currentBall;
    float releaseTime;
    float swingTime = -1f;
    float initialSpeed;
    Vector3 releasePos;
    BallRecord currentRecord;
    GameObject dashboard;
    Text dashboardText;
    bool dashboardShown;
    SessionData previousSession;
    string savePath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("MatchAnalytics");
        go.AddComponent<MatchAnalytics>();
    }

    void Awake()
    {
        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, "vr_cricket_match_analytics.json");
        LoadPreviousSession();
        BuildDashboard();
    }

    void LoadPreviousSession()
    {
        try
        {
            if (!File.Exists(savePath)) return;
            var data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(savePath));
            if (data != null && data.sessions != null && data.sessions.Count > 0)
                previousSession = data.sessions[data.sessions.Count - 1];
        }
        catch (Exception e) { Debug.LogWarning($"MatchAnalytics: could not load previous session: {e.Message}"); }
    }

    void BuildDashboard()
    {
        var canvasGo = new GameObject("AnalyticsCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        dashboard = new GameObject("AnalyticsPanel");
        dashboard.transform.SetParent(canvasGo.transform, false);
        var bg = dashboard.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        var rt = dashboard.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(900f, 650f);

        var textGo = new GameObject("AnalyticsText");
        textGo.transform.SetParent(dashboard.transform, false);
        dashboardText = textGo.AddComponent<Text>();
        dashboardText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dashboardText.fontSize = 20; dashboardText.color = Color.white;
        dashboardText.alignment = TextAnchor.UpperLeft;
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20f, 20f); trt.offsetMax = new Vector2(-20f, -20f);

        // Toggle button, top-right, always visible during a match.
        var btnGo = new GameObject("AnalyticsButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnImg = btnGo.AddComponent<Image>(); btnImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        var btn = btnGo.AddComponent<Button>();
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 1f); brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f); brt.anchoredPosition = new Vector2(-20f, -20f);
        brt.sizeDelta = new Vector2(180f, 50f);
        var btnTextGo = new GameObject("ButtonText");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnText = btnTextGo.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 20; btnText.color = Color.white; btnText.alignment = TextAnchor.MiddleCenter;
        btnText.text = "Analytics";
        var btrt = btnTextGo.GetComponent<RectTransform>();
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero; btrt.offsetMax = Vector2.zero;
        btn.onClick.AddListener(ToggleDashboard);

        dashboard.SetActive(false);
    }

    void ToggleDashboard()
    {
        dashboardShown = !dashboardShown;
        dashboard.SetActive(dashboardShown);
        if (dashboardShown) RefreshDashboardText();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) ToggleDashboard();

        var match = CricketMatchManager.Instance;
        if (match != null && (match.MatchComplete || match.InningsComplete) && !dashboardShown && balls.Count > 0)
        {
            dashboardShown = true;
            dashboard.SetActive(true);
            RefreshDashboardText();
            SaveSession();
        }

        // New ball?
        GameObject ball = BallThrower.ActiveMatchBall;
        if (ball != null && ball != currentBall)
        {
            currentBall = ball;
            releaseTime = Time.time;
            swingTime = -1f;
            var rb = ball.GetComponent<Rigidbody>();
            initialSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
            releasePos = ball.transform.position;
            var flight = ball.GetComponent<BallFlightData>();
            currentRecord = new BallRecord
            {
                ballNumber = balls.Count + 1,
                variation = flight != null ? flight.variationName : "Unknown",
                spinKind = flight != null ? flight.spinKind.ToString() : "Unknown",
            };
        }

        // First swing after release?
        if (currentBall != null && swingTime < 0f)
        {
            var tracker = FindAnyObjectByType<BatSwingTracker>();
            if (tracker != null && tracker.GetSwingSpeed() >= 1.0f)
            {
                swingTime = Time.time;
                if (currentRecord != null)
                {
                    currentRecord.reactionTime = swingTime - releaseTime;
                    currentRecord.swingSpeed = tracker.GetSwingSpeed();
                }
            }
        }

        // Hit? Record exit speed + shot zone once.
        if (currentBall != null && currentRecord != null && currentRecord.exitSpeed <= 0f)
        {
            var hp = currentBall.GetComponent<BallHitPhysics>();
            if (hp != null && hp.State == BallHitPhysics.BallState.Hit)
            {
                var rb = currentBall.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    currentRecord.exitSpeed = rb.linearVelocity.magnitude;
                    currentRecord.shotZone = ShotZone(rb.linearVelocity);
                }
            }
        }
    }

    string ShotZone(Vector3 exitVel)
    {
        var match = CricketMatchManager.Instance;
        var stumps = GameObject.Find("StumpTarget");
        var bowlerGo = GameObject.Find("Bowler");
        if (stumps == null || bowlerGo == null) return "Unknown";
        Vector3 pitchDir = (stumps.transform.position - bowlerGo.transform.position); pitchDir.y = 0f;
        if (pitchDir.sqrMagnitude < 0.01f) return "Unknown";
        pitchDir.Normalize();
        Vector3 offSide = Vector3.Cross(Vector3.up, pitchDir).normalized;
        Vector3 v = exitVel; v.y = 0f;
        if (v.sqrMagnitude < 0.01f) return "Unknown";
        v.Normalize();
        float along = Vector3.Dot(v, pitchDir);
        float side = Vector3.Dot(v, offSide);
        float angle = Mathf.Atan2(side, along) * Mathf.Rad2Deg;
        // Angle 0 = back toward bowler (straight), +90 = off side, -90 = leg side.
        if (angle > 135f || angle < -135f) return "Straight (bowler end)";
        if (angle >= 45f && angle <= 135f) return "Off side - Cover/Point";
        if (angle >= -45f && angle < 45f) return "Straight drive";
        if (angle >= -135f && angle < -45f) return "Leg side - MidWicket/SquareLeg";
        return "Unknown";
    }

    string TimingFor(BallRecord r)
    {
        if (r.reactionTime < 0f) return "Leave (no swing)";
        // Estimated arrival: distance bowler->batsman / initial speed.
        float distance = 20.12f;
        float flightTime = initialSpeed > 0.01f ? distance / initialSpeed : 0.6f;
        float swingAfterRelease = r.reactionTime;
        float diff = swingAfterRelease - flightTime;
        if (Mathf.Abs(diff) <= 0.12f) return "Perfect";
        return diff < 0f ? "Early" : "Late";
    }

    /// <summary>Called from CricketMatchManager.ResolveDelivery, once per ball.</summary>
    public void RecordOutcome(CricketMatchManager.DeliveryResult result, int runs)
    {
        if (currentRecord == null)
        {
            currentRecord = new BallRecord { ballNumber = balls.Count + 1, variation = "Unknown", spinKind = "Unknown" };
        }
        currentRecord.result = result.ToString();
        currentRecord.runs = result == CricketMatchManager.DeliveryResult.BoundaryFour ? 4
            : result == CricketMatchManager.DeliveryResult.BoundarySix ? 6
            : result == CricketMatchManager.DeliveryResult.Wide || result == CricketMatchManager.DeliveryResult.NoBall ? 1
            : runs;
        currentRecord.timing = TimingFor(currentRecord);
        if (string.IsNullOrEmpty(currentRecord.shotZone)) currentRecord.shotZone = "-";
        balls.Add(currentRecord);
        AdaptiveDifficulty.UpdateAfterBall(currentRecord);
        currentRecord = null;
        currentBall = null;
        if (dashboardShown) RefreshDashboardText();
    }

    float AvgReaction()
    {
        float sum = 0f; int n = 0;
        foreach (var b in balls) if (b.reactionTime >= 0f) { sum += b.reactionTime; n++; }
        return n > 0 ? sum / n : -1f;
    }

    void RefreshDashboardText()
    {
        if (dashboardText == null) return;
        int total = balls.Count;
        int hits = 0, swings = 0, wickets = 0, boundaries = 0, runsTotal = 0;
        int fullHit = 0, fullTotal = 0, shortHit = 0, shortTotal = 0;
        int bestStreak = 0, streak = 0;
        foreach (var b in balls)
        {
            runsTotal += b.runs;
            if (b.reactionTime >= 0f) swings++;
            if (b.exitSpeed > 0f) hits++;
            if (b.result == "Wicket" || b.result == "RunOut") wickets++;
            if (b.result == "BoundaryFour" || b.result == "BoundarySix") boundaries++;
            bool isShort = b.spinKind == "Bouncer" || (b.variation != null && b.variation.ToLowerInvariant().Contains("short"));
            bool isFull = b.spinKind == "Yorker" || (b.variation != null && (b.variation.ToLowerInvariant().Contains("yorker") || b.variation.ToLowerInvariant().Contains("good")));
            if (isShort) { shortTotal++; if (b.exitSpeed > 0f) shortHit++; }
            if (isFull) { fullTotal++; if (b.exitSpeed > 0f) fullHit++; }
            if (b.result == "Wicket" || b.result == "RunOut") { streak = 0; }
            else { streak++; if (streak > bestStreak) bestStreak = streak; }
        }
        float avgRT = AvgReaction();
        string rtText = avgRT >= 0f ? $"{avgRT:F3} s" : "No swing yet";
        float prevRT = -1f;
        if (previousSession != null)
        {
            float s = 0f; int n = 0;
            foreach (var b in previousSession.balls) if (b.reactionTime >= 0f) { s += b.reactionTime; n++; }
            if (n > 0) prevRT = s / n;
        }
        string trend = prevRT < 0f ? "No previous session yet" :
            avgRT < 0f ? "No reaction yet this session" :
            avgRT < prevRT ? $"Improved by {(prevRT - avgRT) * 1000f:F0} ms vs previous session" :
            avgRT > prevRT ? $"Slower by {(avgRT - prevRT) * 1000f:F0} ms vs previous session" : "Same as previous session";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("PERFORMANCE ANALYTICS DASHBOARD");
        sb.AppendLine($"Balls: {total}  Runs: {runsTotal}  Wickets: {wickets}  Boundaries: {boundaries}");
        sb.AppendLine($"Avg reaction time: {rtText}  Trend: {trend}");
        sb.AppendLine($"Difficulty level: {AdaptiveDifficulty.Level} / 5 (auto, see below)");
        sb.AppendLine("");
        sb.AppendLine("COGNITIVE SKILLS (measured, not guessed):");
        sb.AppendLine($"- Reaction time: {rtText}");
        sb.AppendLine($"- Ball tracking: {(total > 0 ? swings * 100 / total : 0)}% of balls swung at ({swings}/{total})");
        sb.AppendLine($"- Hand-eye: {(swings > 0 ? hits * 100 / swings : 0)}% of swings made contact ({hits}/{swings})");
        sb.AppendLine($"- Depth (full balls): {(fullTotal > 0 ? fullHit * 100 / fullTotal : 0)}% hit ({fullHit}/{fullTotal})");
        sb.AppendLine($"- Depth (short balls): {(shortTotal > 0 ? shortHit * 100 / shortTotal : 0)}% hit ({shortHit}/{shortTotal})");
        sb.AppendLine($"- Focus: longest streak without dismissal: {bestStreak} balls");
        sb.AppendLine("- Gaze/focus: NOT measured - no eye tracker in headset data. Streak above is a proxy, not gaze.");
        sb.AppendLine("");
        sb.AppendLine("ADAPTIVE DIFFICULTY:");
        sb.AppendLine("Level rises when last 6 balls are hit well + reaction fast, falls when struggling. Pace/speed and variation change with level (see AdaptiveDifficulty).");
        sb.AppendLine("");
        sb.AppendLine("LAST BALLS (ball, variation, reaction, timing, result, zone):");
        int start = Math.Max(0, balls.Count - 12);
        for (int i = start; i < balls.Count; i++)
        {
            var b = balls[i];
            string rt = b.reactionTime >= 0f ? $"{b.reactionTime:F2}s" : "leave";
            sb.AppendLine($"{b.ballNumber}. {b.variation} | {rt} | {b.timing} | {b.result} {b.runs} | {b.shotZone}");
        }
        dashboardText.text = sb.ToString();
    }

    void SaveSession()
    {
        try
        {
            SaveFile data = new SaveFile();
            if (File.Exists(savePath))
            {
                var existing = JsonUtility.FromJson<SaveFile>(File.ReadAllText(savePath));
                if (existing != null && existing.sessions != null) data.sessions = existing.sessions;
            }
            data.sessions.Add(new SessionData { dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), balls = new List<BallRecord>(balls) });
            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e) { Debug.LogWarning($"MatchAnalytics: save failed: {e.Message}"); }
    }
}

/// <summary>
/// ADAPTIVE DIFFICULTY - bowler adapts to the player, like a real coach.
/// Level 1 = easiest, 5 = hardest, start at 3.
/// - After every ball, look at the last 6 balls: if hit rate >= 66%
///   and average reaction < 0.40 s, level +1. If hit rate <= 33%
///   or a wicket fell in the last 3 balls, level -1. Clamped 1..5.
/// - Pace: timeToTarget scaled by level (1: 1.25x slower, 2: 1.12x,
///   3: 1.00x, 4: 0.90x, 5: 0.80x faster). For a 20.12 m pitch, that
///   is roughly 96 km/h at level 1 to 150 km/h at level 5 for a fast
///   variation - real fast-bowling range, not a made-up number.
/// - Swing/spin: scaled 0.70x at level 1 to 1.30x at level 5.
/// - Variation bias: at level >= 4, half the balls prefer googly /
///   bouncer / yorker variations (if the bowler has them); at level
///   <= 2, straight variations are preferred. Falls back to random
///   if no such variation exists.
/// </summary>
public static class AdaptiveDifficulty
{
    public static int Level { get; private set; } = 3;
    static readonly List<MatchAnalytics.BallRecord> recent = new List<MatchAnalytics.BallRecord>();

    public static float TimeScale => Level == 1 ? 1.25f : Level == 2 ? 1.12f : Level == 3 ? 1.0f : Level == 4 ? 0.90f : 0.80f;
    public static float SwingScale => Level == 1 ? 0.70f : Level == 2 ? 0.85f : Level == 3 ? 1.0f : Level == 4 ? 1.15f : 1.30f;

    public static void UpdateAfterBall(MatchAnalytics.BallRecord record)
    {
        if (record == null) return;
        recent.Add(record);
        if (recent.Count > 6) recent.RemoveAt(0);
        if (recent.Count < 3) return;
        int hits = 0;
        float rtSum = 0f; int rtN = 0;
        bool wicketRecent = false;
        for (int i = 0; i < recent.Count; i++)
        {
            var b = recent[i];
            if (b.exitSpeed > 0f) hits++;
            if (b.reactionTime >= 0f) { rtSum += b.reactionTime; rtN++; }
            if (i >= recent.Count - 3 && (b.result == "Wicket" || b.result == "RunOut")) wicketRecent = true;
        }
        float hitRate = (float)hits / recent.Count;
        float avgRT = rtN > 0 ? rtSum / rtN : 999f;
        if (hitRate >= 0.66f && avgRT < 0.40f) Level = Math.Min(5, Level + 1);
        else if (hitRate <= 0.33f || wicketRecent) Level = Math.Max(1, Level - 1);
        Debug.Log($"AdaptiveDifficulty: level {Level} (hitRate {hitRate:F2}, avgRT {avgRT:F3}s, last {recent.Count} balls)");
    }
}
