using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Scene-level manager for real cricket physics:
/// builds the stumps, attaches swing tracking to the bat,
/// hooks every bowled ball, and shows BOWLED / FOUR / SIX messages.
/// Everything is wired at runtime — no scene edits needed.
/// </summary>
public class CricketMatchManager : MonoBehaviour
{
    public static CricketMatchManager Instance { get; private set; }

    [Header("Boundary")]
    [Tooltip("Distance from pitch centre counted as the boundary, in metres")]
    public float boundaryRadius = 55f;

    [Header("Match Rules")]
    [Min(1)] public int oversPerInnings = 5;
    [Min(1)] public int wicketsPerInnings = 10;
    [Range(1, 2)] public int inningsPerMatch = 2;

    public StumpsPhysics Stumps { get; private set; }
    public Vector3 PitchCentre { get; private set; }

    Text bigMessage;
    Text speedLabel;
    Text scoreLabel;
    float messageTimer;
    string matchResultMessage = "";
    BattingRunController battingRuns;
    readonly HashSet<EntityId> resolvedBalls = new HashSet<EntityId>();
    readonly Dictionary<EntityId, int> runCallsByBall = new Dictionary<EntityId, int>();

    public int Runs { get; private set; }
    public int Wickets { get; private set; }
    public int Extras { get; private set; }
    public int LegalBalls { get; private set; }
    public int InningsNumber { get; private set; } = 1;
    public int FirstInningsRuns { get; private set; }
    public bool InningsComplete { get; private set; }
    public bool MatchComplete { get; private set; }

    public enum DeliveryResult { Dot, Runs, BoundaryFour, BoundarySix, Wicket, RunOut, Wide, NoBall }

    public bool IsRunInProgress => battingRuns != null && battingRuns.IsRunning;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void Start()
    {
        Vector3 bowlerPos = new Vector3(2.23f, 0f, -6.93f);
        var activeBowler = FindAnyObjectByType<BowlingManager>();
        if (activeBowler != null)
        {
            bowlerPos = activeBowler.transform.position;
        }
        else
        {
            var anyBowler = FindAnyObjectByType<BowlingManager>(FindObjectsInactive.Include);
            if (anyBowler != null) bowlerPos = anyBowler.transform.position;
            else
            {
                var bGo = GameObject.Find("Bowler");
                if (bGo != null) bowlerPos = bGo.transform.position;
            }
        }

        Vector3 batsmanPos = new Vector3(-13.3f, 0f, 2.1f);
        var batsman = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (batsman != null) batsmanPos = batsman.transform.position;

        Vector3 pc = (bowlerPos + batsmanPos) * 0.5f;
        pc.y = 0f;
        PitchCentre = pc;

        BowlingManager humanTemplate = FindHumanBowlerTemplate();
        if (humanTemplate == null)
            Debug.LogWarning("CricketMatchManager: no humanoid template found; match scoring and ball play will continue without generated keeper/fielders.");

        var mainWicketTarget = GameObject.Find("StumpTarget");
        if (mainWicketTarget == null)
        {
            Debug.LogError("CricketMatchManager: main yellow wicket target 'StumpTarget' is missing.");
            StartCoroutine(WatchForBalls());
            return;
        }

        // Each system below is independent: if one fails, the rest must
        // still run. Never let one exception kill the whole match.
        try
        {
            // Use the yellow V-shaped scene wicket instead of creating a
            // second white set beside the batsman.
            Stumps = StumpsPhysics.Build(mainWicketTarget.transform.position, bowlerPos);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CricketMatchManager: stumps build failed: {e.Message}\n{e.StackTrace}");
        }

        try
        {
            // Put every pitch target on the true bowler->stumps line, so balls
            // stop spraying wide and actually threaten the wicket. Each length
            // keeps its character (yorker full, bouncer short), only the LINE
            // is corrected. Runs before the first ball is released.
            if (Stumps != null)
                CorrectPitchTargets(bowlerPos, Stumps.transform.position);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CricketMatchManager: pitch-target correction failed: {e.Message}");
        }

        try
        {
            // Wicket-keeper behind the stumps: tracks the ball, steps out,
            // and catches edges / leaves.
            if (Stumps != null && humanTemplate != null)
            {
                Vector3 awayDir = Stumps.transform.position - bowlerPos;
                awayDir.y = 0f;
                if (awayDir.sqrMagnitude < 0.01f) awayDir = Vector3.back;
                awayDir.Normalize();
                WicketKeeper.Build(Stumps.transform.position, awayDir, humanTemplate);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CricketMatchManager: keeper build failed: {e.Message}");
        }

        try
        {
            if (Stumps != null && humanTemplate != null)
                FieldingManager.Build(bowlerPos, Stumps.transform.position, humanTemplate);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CricketMatchManager: fielder setup failed: {e.Message}");
        }

        try
        {
            // Bat swing tracking (mouse swing and VR grab both move the bat transform).
            var snap = FindAnyObjectByType<SnapSceneBatToHand>();
            if (snap != null && snap.batInstance != null &&
                snap.batInstance.GetComponent<BatSwingTracker>() == null)
            {
                snap.batInstance.AddComponent<BatSwingTracker>();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CricketMatchManager: bat tracker hook failed: {e.Message}");
        }

        try
        {
            var nonStrikerCrease = GameObject.Find("CreaseTarget");
            if (batsman != null && nonStrikerCrease != null)
                battingRuns = BattingRunController.Build(batsman, nonStrikerCrease.transform.position);
            else
                Debug.LogWarning("CricketMatchManager: running between wickets is unavailable; batsman or CreaseTarget is missing.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CricketMatchManager: batting-run setup failed: {e.Message}");
        }

        StartCoroutine(WatchForBalls());
        Debug.Log("CricketMatchManager: all match systems started.");
    }

    BowlingManager FindHumanBowlerTemplate()
    {
        var bowlers = FindObjectsByType<BowlingManager>(FindObjectsInactive.Include);
        foreach (var bowler in bowlers)
        {
            if (bowler == null || bowler.animator == null) continue;
            if (bowler.animator.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return bowler;
        }
        return null;
    }

    // BowlingManager spawns balls via BallThrower; hook each one once.
    IEnumerator WatchForBalls()
    {
        var wait = new WaitForSeconds(0.3f);
        while (true)
        {
            foreach (var thrower in FindObjectsByType<BallThrower>(FindObjectsInactive.Exclude))
            {
                if (!thrower.enabled || thrower.GetComponent<BallFlightData>() == null) continue;
                if (thrower.GetComponent<BallHitPhysics>() == null)
                    thrower.gameObject.AddComponent<BallHitPhysics>();
            }
            yield return wait;
        }
    }

    // Move the scene's pitch targets onto the real stump line.
    // t = fraction from bowler (0) to stumps (1).
    void CorrectPitchTargets(Vector3 bowlerPos, Vector3 stumpPos)
    {
        SetPitchTarget("YorkerPoint", bowlerPos, stumpPos, 0.90f);
        SetPitchTarget("GoodLengthPoint", bowlerPos, stumpPos, 0.72f);
        SetPitchTarget("ShortLengthPoint", bowlerPos, stumpPos, 0.62f);
        SetPitchTarget("BouncerPoint", bowlerPos, stumpPos, 0.52f);
    }

    void SetPitchTarget(string name, Vector3 a, Vector3 b, float t)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            Debug.LogWarning($"CricketMatchManager: pitch target '{name}' not found, line not corrected.");
            return;
        }
        Vector3 p = Vector3.Lerp(a, b, t);
        p.y = 0.05f;
        go.transform.position = p;
        Debug.Log($"CricketMatchManager: {name} corrected to {p} (on stump line)");
    }

    void Update()
    {
        // R calls the batter to run while a struck ball is still live. It
        // works alongside the existing mouse/VR bat controls and camera keys.
        if (!InningsComplete && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            GameObject activeBall = BallThrower.ActiveMatchBall;
            BallHitPhysics hitBall = activeBall != null ? activeBall.GetComponent<BallHitPhysics>() : null;
            if (hitBall != null && hitBall.CanCallRun)
                CallRun(hitBall);
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && bigMessage != null)
                bigMessage.enabled = false;
        }
    }

    public void ShowMessage(string text, Color color, float seconds = 2.5f)
    {
        if (bigMessage == null) return;
        bigMessage.text = text;
        bigMessage.color = color;
        bigMessage.enabled = true;
        messageTimer = seconds;
    }

    public void ShowSpeed(float metresPerSecond)
    {
        if (speedLabel == null) return;
        speedLabel.text = $"Ball speed: {metresPerSecond * 3.6f:F0} km/h";
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("CricketUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        bigMessage = MakeText(canvasGo.transform, 36, 120f);
        RectTransform messageRect = bigMessage.rectTransform;
        messageRect.anchorMin = new Vector2(0.04f, 0.5f);
        messageRect.anchorMax = new Vector2(0.96f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = new Vector2(0f, 120f);
        messageRect.sizeDelta = new Vector2(0f, 180f);
        bigMessage.resizeTextForBestFit = true;
        bigMessage.resizeTextMinSize = 24;
        bigMessage.resizeTextMaxSize = 36;
        bigMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
        bigMessage.enabled = false;

        speedLabel = MakeText(canvasGo.transform, 24, -170f);
        speedLabel.alignment = TextAnchor.MiddleCenter;
        speedLabel.text = "";

        scoreLabel = MakeText(canvasGo.transform, 24, 220f);
        scoreLabel.alignment = TextAnchor.UpperLeft;
        RectTransform scoreRect = scoreLabel.rectTransform;
        scoreRect.anchorMin = new Vector2(0f, 1f);
        scoreRect.anchorMax = new Vector2(0f, 1f);
        scoreRect.pivot = new Vector2(0f, 1f);
        scoreRect.anchoredPosition = new Vector2(24f, -20f);
        scoreRect.sizeDelta = new Vector2(560f, 130f);
        scoreLabel.color = Color.white;
        RefreshScoreboard();
    }

    /// <summary>Resolve one delivery exactly once and update the innings score.</summary>
    public void ResolveDelivery(BallHitPhysics ball, DeliveryResult result, int runs = 0, bool legal = true)
    {
        if (InningsComplete || MatchComplete || ball == null || !resolvedBalls.Add(ball.GetEntityId())) return;

        // A run only counts after both batters reach the opposite crease.
        // Stop any run still in progress before adjudicating the delivery.
        if (battingRuns != null) battingRuns.CancelRun();

        int calledRuns = 0;
        EntityId ballId = ball.GetEntityId();
        if (runCallsByBall.TryGetValue(ballId, out calledRuns))
            runCallsByBall.Remove(ballId);
        // A boundary allowance is the total batter's score for the delivery;
        // don't add already-called runs on top of the four or six. Dismissals
        // also cancel unfinished running. For ordinary/extra deliveries,
        // include any runs the batters had called before the ball was dead.
        if (result != DeliveryResult.Wicket &&
            result != DeliveryResult.RunOut &&
            result != DeliveryResult.BoundaryFour &&
            result != DeliveryResult.BoundarySix)
        {
            Runs += result == DeliveryResult.Runs
                ? Mathf.Max(calledRuns, runs)
                : calledRuns;
        }
        else if (result == DeliveryResult.RunOut)
        {
            // Completed runs count before a run-out; the run currently in
            // progress is cancelled by BattingRunController.CancelRun().
            Runs += calledRuns;
        }

        switch (result)
        {
            case DeliveryResult.BoundaryFour: Runs += 4; break;
            case DeliveryResult.BoundarySix: Runs += 6; break;
            case DeliveryResult.Wicket:
            case DeliveryResult.RunOut: Wickets++; break;
            case DeliveryResult.Wide: Runs++; Extras++; legal = false; break;
            case DeliveryResult.NoBall: Runs++; Extras++; legal = false; break;
            // Runs for this result were included above with calledRuns.
            case DeliveryResult.Runs: break;
        }

        if (legal)
        {
            LegalBalls++;
            if (ball.DeliveringBowler != null)
                ball.DeliveringBowler.RecordLegalBall();
        }
        RefreshScoreboard();
        bool chaseComplete = InningsNumber > 1 && Runs >= FirstInningsRuns + 1;
        if (chaseComplete)
        {
            InningsComplete = true;
            MatchComplete = true;
            matchResultMessage = "CHASE WON!";
            RefreshScoreboard();
            ShowMessage(matchResultMessage, Color.green, 5f);
            return;
        }

        bool inningsEnded = Wickets >= wicketsPerInnings ||
            LegalBalls >= Mathf.Max(1, oversPerInnings) * 6;
        if (legal && !inningsEnded)
        {
            if (OverManager.Instance != null)
                OverManager.Instance.RecordBall();

            // Batters change strike at the end of every completed over.
            if (LegalBalls > 0 && LegalBalls % 6 == 0 && battingRuns != null)
                battingRuns.SwapStrike();
        }

        if (inningsEnded)
        {
            InningsComplete = true;
            if (InningsNumber < Mathf.Max(1, inningsPerMatch))
            {
                ShowMessage($"INNINGS {InningsNumber} COMPLETE\n{Runs}/{Wickets}", Color.yellow, 4f);
                StartCoroutine(BeginNextInnings());
            }
            else
            {
                MatchComplete = true;
                if (InningsNumber == 1)
                {
                    matchResultMessage = $"INNINGS COMPLETE  {Runs}/{Wickets}";
                    RefreshScoreboard();
                    ShowMessage(matchResultMessage, Color.yellow, 5f);
                }
                else
                {
                    string matchResult = Runs == FirstInningsRuns ? "MATCH TIED" : "DEFENDED!";
                    matchResultMessage = $"{matchResult}  {FirstInningsRuns} - {Runs}";
                    RefreshScoreboard();
                    ShowMessage(matchResultMessage, Color.yellow, 5f);
                }
            }
        }
    }

    IEnumerator BeginNextInnings()
    {
        // Let the last delivery finish its dismissal/boundary animation and
        // leave the one-ball registry before the next innings is unlocked.
        while (BallThrower.ActiveMatchBall != null)
            yield return null;
        yield return new WaitForSeconds(2f);

        FirstInningsRuns = Runs;
        InningsNumber++;
        Runs = 0;
        Wickets = 0;
        Extras = 0;
        LegalBalls = 0;
        runCallsByBall.Clear();
        InningsComplete = false;
        RefreshScoreboard();

        if (OverManager.Instance != null)
            OverManager.Instance.PrepareForNewInnings();
        else
        {
            var activeBowler = FindAnyObjectByType<BowlingManager>();
            if (activeBowler != null) activeBowler.BeginNewInnings();
        }

        ShowMessage($"INNINGS {InningsNumber}\nTARGET {FirstInningsRuns + 1}", Color.yellow, 4f);
    }

    void RefreshScoreboard()
    {
        if (scoreLabel == null) return;
        int pendingRuns = 0;
        foreach (int count in runCallsByBall.Values) pendingRuns += count;
        if (battingRuns != null && battingRuns.IsRunning) pendingRuns++;
        string running = pendingRuns > 0 ? $"\nRUNNING: {pendingRuns}" : "";
        string innings = InningsNumber == 1
            ? "1ST INNINGS"
            : $"2ND INNINGS  TARGET {FirstInningsRuns + 1}  NEED {Mathf.Max(0, FirstInningsRuns + 1 - Runs)}";
        string result = MatchComplete ? $"\n{matchResultMessage}" : "";
        scoreLabel.text = $"{innings}\nSCORE  {Runs}/{Wickets}   OVERS  {LegalBalls / 6}.{LegalBalls % 6}   EXTRAS {Extras}{running}{result}";
    }

    public void CallRun(BallHitPhysics ball)
    {
        if (InningsComplete || ball == null || !ball.CanCallRun)
            return;

        EntityId id = ball.GetEntityId();
        runCallsByBall.TryGetValue(id, out int currentRuns);
        if (currentRuns >= 3 || battingRuns == null || battingRuns.IsRunning) return;

        bool started = battingRuns.TryStartRun(completed =>
        {
            if (!completed || ball == null || !ball.CanCallRun || InningsComplete) return;
            runCallsByBall.TryGetValue(id, out int completedRuns);
            int totalCompletedRuns = completedRuns + 1;
            runCallsByBall[id] = Mathf.Min(3, totalCompletedRuns);
            // The two batters crossed ends; the non-striker is now on strike.
            if ((totalCompletedRuns & 1) == 1)
                battingRuns.SwapStrike();
            RefreshScoreboard();
            ShowMessage("RUN COMPLETED", Color.white, 0.8f);
        });
        if (!started) return;

        RefreshScoreboard();
        ShowMessage("RUN!", Color.white, 1.0f);
    }

    Text MakeText(Transform parent, int fontSize, float yOffset)
    {
        var go = new GameObject("CricketText", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(1000f, 180f);

        var t = go.AddComponent<Text>();
        // Unity 6 removed the old Arial.ttf built-in resource.
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.yellow;
        t.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, 3f);
        return t;
    }
}
