using System.Collections;
using UnityEngine;
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

    public StumpsPhysics Stumps { get; private set; }
    public Vector3 PitchCentre { get; private set; }

    Text bigMessage;
    Text speedLabel;
    float messageTimer;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void Start()
    {
        var bowler = GameObject.Find("Bowler");
        var batsman = FindFirstObjectByType<PlayerMovement>();
        Vector3 bowlerPos = bowler != null ? bowler.transform.position : new Vector3(2.23f, 0f, -6.93f);
        Vector3 batsmanPos = batsman != null ? batsman.transform.position : new Vector3(-13.3f, 0f, 2.1f);

        PitchCentre = (bowlerPos + batsmanPos) * 0.5f;
        PitchCentre.y = 0f;

        // Real stumps behind the batsman.
        Stumps = StumpsPhysics.Build(batsmanPos, bowlerPos);

        // Put every pitch target on the true bowler->stumps line, so balls
        // stop spraying wide and actually threaten the wicket. Each length
        // keeps its character (yorker full, bouncer short), only the LINE
        // is corrected. Runs before the first ball is released.
        CorrectPitchTargets(bowlerPos, Stumps.transform.position);

        // Wicket-keeper behind the stumps: tracks the ball, steps out,
        // and catches edges / leaves.
        Vector3 awayDir = Stumps.transform.position - bowlerPos;
        awayDir.y = 0f;
        if (awayDir.sqrMagnitude < 0.01f) awayDir = Vector3.back;
        awayDir.Normalize();
        WicketKeeper.Build(Stumps.transform.position, awayDir);

        // Bat swing tracking (mouse swing and VR grab both move the bat transform).
        var snap = FindFirstObjectByType<SnapSceneBatToHand>();
        if (snap != null && snap.batInstance != null &&
            snap.batInstance.GetComponent<BatSwingTracker>() == null)
        {
            snap.batInstance.AddComponent<BatSwingTracker>();
        }

        StartCoroutine(WatchForBalls());
    }

    // BowlingManager spawns balls via BallThrower; hook each one once.
    IEnumerator WatchForBalls()
    {
        var wait = new WaitForSeconds(0.3f);
        while (true)
        {
            foreach (var thrower in FindObjectsByType<BallThrower>(FindObjectsInactive.Exclude))
            {
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

        bigMessage = MakeText(canvasGo.transform, 64, 120f);
        bigMessage.enabled = false;

        speedLabel = MakeText(canvasGo.transform, 24, -170f);
        speedLabel.alignment = TextAnchor.MiddleCenter;
        speedLabel.text = "";
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
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
