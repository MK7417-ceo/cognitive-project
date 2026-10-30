using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum BowlerType
{
    LeftArmFast,
    RightArmFast,
    LeftArmMediumFast,
    RightArmMediumFast,
    LeftArmOrthodoxSpin,
    LeftArmWristSpin,
    RightArmOffSpin,
    RightArmLegSpin
}

[System.Serializable]
public class BallVariation
{
    public string name;
    public Transform targetPoint;
    public float timeToTarget = 0.6f;
    public float swingMeters = 0f;
    public Vector3 spinTorque = Vector3.zero;
}

public class BowlingManager : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Transform rightHandSpawn;
    public Transform leftHandSpawn;
    public GameObject ballPrefab;

    [Header("UI")]
    public BowlingStyleUI bowlingStyleUI;

    [Header("Over Settings")]
    public int ballsPerOver = 6;

    [Header("Real Bowling Rhythm")]
    [Tooltip("Pause between deliveries: the bowler walks back, then runs in again like a real bowler.")]
    public float betweenBallsDelay = 4f;

    [Header("Bowler Rotation Order")]
    public BowlerType[] bowlerOrder =
    {
        BowlerType.RightArmFast,
        BowlerType.LeftArmFast,
        BowlerType.RightArmOffSpin,
        BowlerType.LeftArmOrthodoxSpin
    };

    [Header("Ball Variations")]
    public List<BallVariation> variations = new List<BallVariation>();

    public BowlerType currentBowler;

    Transform activeSpawn;
    int lastIndex = -1;

    public bool isLeftArm { get; private set; }
    public bool isSpinBowler { get; private set; }

    int ballCount = 0;
    int bowlerIndex = 0;

    // Run-up start, captured before the first ball.
    Vector3 runUpStartPos;
    Quaternion runUpStartRot;
    bool waitingForNextBall = false;

    void Start()
    {
        if (bowlerOrder == null || bowlerOrder.Length == 0)
        {
            Debug.LogError("BowlingManager: bowlerOrder is empty.");
            return;
        }

        if (ballPrefab == null || ballPrefab.GetComponent<BallThrower>() == null ||
            ballPrefab.GetComponent<Rigidbody>() == null)
        {
            Debug.LogError("BowlingManager: ballPrefab must have BallThrower and Rigidbody on its root.");
            enabled = false;
            return;
        }

        if (variations == null || variations.Count == 0 ||
            variations.Exists(v => v == null || v.targetPoint == null || v.timeToTarget <= 0.01f))
        {
            Debug.LogError("BowlingManager: assign a target point and positive time to every ball variation.");
            enabled = false;
            return;
        }

        runUpStartPos = transform.position;
        runUpStartRot = transform.rotation;

        currentBowler = bowlerOrder[bowlerIndex];
        UpdateBowlingStyleUI();

        var runUp = GetComponent<RunUpController>();
        // Resolve the delivery hand before any animation event can fire. The
        // initial run-up can emit a stale release event on some imported rigs.
        ConfigureBowlerType(false);
        if (runUp != null)
        {
            runUp.RunUpReachedCrease += ApplyBowlerType;
            if (!runUp.isRunning) ApplyBowlerType();
        }
        else
        {
            ApplyBowlerType();
        }
    }

    void ApplyBowlerType()
    {
        ConfigureBowlerType(true);
    }

    void ConfigureBowlerType(bool playBowlingAnimation)
    {
        if (CricketMatchManager.Instance != null && CricketMatchManager.Instance.InningsComplete)
            return;

        // LEFT / RIGHT ARM
        isLeftArm =
            currentBowler == BowlerType.LeftArmFast ||
            currentBowler == BowlerType.LeftArmMediumFast ||
            currentBowler == BowlerType.LeftArmOrthodoxSpin ||
            currentBowler == BowlerType.LeftArmWristSpin;

        // SPIN / FAST
        isSpinBowler =
            currentBowler == BowlerType.LeftArmOrthodoxSpin ||
            currentBowler == BowlerType.LeftArmWristSpin ||
            currentBowler == BowlerType.RightArmOffSpin ||
            currentBowler == BowlerType.RightArmLegSpin;

        // HAND SPAWN
        activeSpawn = isLeftArm ? leftHandSpawn : rightHandSpawn;

        // ONLY ONE ANIMATOR TRIGGER
        if (playBowlingAnimation && animator != null)
        {
            animator.ResetTrigger("Bowl");
            animator.SetTrigger("Bowl");
        }
    }

    BallVariation PickVariation()
    {
        if (variations.Count == 0) return null;

        int idx = Random.Range(0, variations.Count);
        if (idx == lastIndex && variations.Count > 1)
            idx = (idx + 1) % variations.Count;

        lastIndex = idx;
        return variations[idx];
    }

    // ANIMATION EVENT
    public void OnBallRelease()
    {
        // A delivery animation may already be playing when the innings ends.
        // Do not emit a ball from its late animation event.
        if (CricketMatchManager.Instance != null && CricketMatchManager.Instance.InningsComplete)
            return;

        // Ignore a release event from an animation that fired before the
        // bowler reached the crease. RunUpReachedCrease starts a fresh bowl.
        var runUp = GetComponent<RunUpController>();
        if (runUp != null && runUp.isRunning) return;

        // Animation events can fire more than once while a delivery cycle is active.
        if (waitingForNextBall) return;

        // Another bowler or a leftover ball already owns the field. Queue the
        // normal delivery cycle instead of creating a second live ball.
        if (BallThrower.ActiveMatchBall != null)
        {
            StartCoroutine(NextBallRoutine());
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("BowlingManager: ballPrefab is not assigned.");
            return;
        }

        if (activeSpawn == null)
        {
            Debug.LogError("BowlingManager: activeSpawn is not set.");
            return;
        }

        BallVariation v = PickVariation();
        if (v == null || v.targetPoint == null || v.timeToTarget <= 0.01f)
        {
            Debug.LogError("BowlingManager: selected ball variation is incomplete.");
            return;
        }

        float finalSwing = isLeftArm ? -v.swingMeters : v.swingMeters;

        GameObject ball = Instantiate(
            ballPrefab,
            activeSpawn.position,
            activeSpawn.rotation
        );

        // Tag the ball with its delivery type so pitch physics can react
        // differently for fast / spin / googly / bouncer / yorker, left vs right arm.
        var flight = ball.AddComponent<BallFlightData>();
        flight.isLeftArm = isLeftArm;
        flight.variationName = v.name;
        flight.spinKind = ClassifySpin(currentBowler, v.name);
        flight.deliveryDirection = (v.targetPoint.position - activeSpawn.position).normalized;

        BallHitPhysics ballPhysics = ball.GetComponent<BallHitPhysics>();
        if (ballPhysics == null)
            ballPhysics = ball.AddComponent<BallHitPhysics>();
        ballPhysics.DeliveringBowler = this;

        BallThrower ballThrower = ball.GetComponent<BallThrower>();
        if (ballThrower == null)
        {
            Debug.LogError("BowlingManager: BallThrower component is missing on the ball prefab.");
            Destroy(ball);
            return;
        }

        ballThrower.Launch(
            activeSpawn.position,
            v.targetPoint.position,
            v.timeToTarget,
            finalSwing,
            v.spinTorque
        );

        OnBallDelivered();
    }

    // Classify the delivery for pitch behaviour. Mirroring (left vs right arm)
    // is applied by BallHitPhysics using flight.isLeftArm.
    BallFlightData.SpinKind ClassifySpin(BowlerType bowler, string variationName)
    {
        string n = (variationName ?? "").ToLowerInvariant();
        if (n.Contains("googly")) return BallFlightData.SpinKind.Googly;
        if (n.Contains("bouncer")) return BallFlightData.SpinKind.Bouncer;
        if (n.Contains("yorker")) return BallFlightData.SpinKind.Yorker;

        switch (bowler)
        {
            case BowlerType.RightArmOffSpin: return BallFlightData.SpinKind.OffSpin;
            case BowlerType.RightArmLegSpin: return BallFlightData.SpinKind.LegSpin;
            case BowlerType.LeftArmOrthodoxSpin: return BallFlightData.SpinKind.OffSpin;
            case BowlerType.LeftArmWristSpin: return BallFlightData.SpinKind.LegSpin;
            default: return BallFlightData.SpinKind.Fast;
        }
    }

    void OnBallDelivered()
    {
        // Real rhythm: pause, walk back to the run-up start, THEN bowl again.
        // (Previously the trigger re-fired instantly, so the bowler bowled
        // nonstop and the run-up broke after a few balls.)
        if (!waitingForNextBall)
            StartCoroutine(NextBallRoutine());
    }

    /// <summary>Count a legal delivery, not a wide/no-ball attempt.</summary>
    public void RecordLegalBall()
    {
        ballCount++;
        if (ballCount < Mathf.Max(1, ballsPerOver)) return;

        ballCount = 0;
        if (bowlerOrder != null && bowlerOrder.Length > 0)
        {
            bowlerIndex = (bowlerIndex + 1) % bowlerOrder.Length;
            currentBowler = bowlerOrder[bowlerIndex];
            UpdateBowlingStyleUI();
        }
    }

    /// <summary>Reset this bowler's over state and begin a fresh innings run-up.</summary>
    public void BeginNewInnings()
    {
        if (bowlerOrder == null || bowlerOrder.Length == 0) return;

        ballCount = 0;
        bowlerIndex = 0;
        currentBowler = bowlerOrder[bowlerIndex];
        waitingForNextBall = false;
        UpdateBowlingStyleUI();
        ConfigureBowlerType(false);

        var runUp = GetComponent<RunUpController>();
        if (runUp != null)
            runUp.BeginRunUp();
        else
        {
            transform.position = runUpStartPos;
            transform.rotation = runUpStartRot;
            ApplyBowlerType();
        }
    }

    IEnumerator NextBallRoutine()
    {
        waitingForNextBall = true;

        yield return new WaitForSeconds(betweenBallsDelay);

// Phase 1 / bowler-timing fix: do NOT bowl the next delivery while
        // the current ball is still live. Real cricket waits for the ball
        // to be dead, caught, bowled, or to cross the boundary. The old
        // fixed-delay-only logic, combined with BallThrower's 5 s destroy,
        // is what made a hit ball vanish between the bat and the boundary.
        // Safety timeout (30 s) so a stuck ball can never freeze the match.
        float waited = 0f;
        while (waited < 30f)
        {
            if (BallThrower.ActiveMatchBall == null)
            {
                bool anyLive = false;
                foreach (var hp in FindObjectsByType<BallHitPhysics>(FindObjectsInactive.Exclude))
                {
                    if (hp.State == BallHitPhysics.BallState.Flying) anyLive = true;
                    else if (hp.State == BallHitPhysics.BallState.Hit && !hp.IsBoundaryResolved) anyLive = true;
                }
                if (!anyLive) break;
            }
            yield return new WaitForSeconds(0.25f);
            waited += 0.25f;
        }

        bool inningsComplete = CricketMatchManager.Instance != null &&
            CricketMatchManager.Instance.InningsComplete;
        if (!inningsComplete)
        {
            // Walk back: return to the run-up start like a real bowler turning around.
            var runUp = GetComponent<RunUpController>();
            if (runUp != null)
                runUp.BeginRunUp();
            else
            {
                transform.position = runUpStartPos;
                transform.rotation = runUpStartRot;
                ApplyBowlerType();
            }
        }
        }

        waitingForNextBall = false;
    }

    void UpdateBowlingStyleUI()
    {
        if (bowlingStyleUI == null) return;

        string text = GetBowlerDisplayName(currentBowler);
        Color color = isSpinBowler ? Color.cyan : Color.yellow;

        bowlingStyleUI.Show(text, color);
    }

    string GetBowlerDisplayName(BowlerType type)
    {
        switch (type)
        {
            case BowlerType.RightArmFast: return "RIGHT ARM FAST BOWLER";
            case BowlerType.LeftArmFast: return "LEFT ARM FAST BOWLER";
            case BowlerType.RightArmMediumFast: return "RIGHT ARM MEDIUM FAST";
            case BowlerType.LeftArmMediumFast: return "LEFT ARM MEDIUM FAST";
            case BowlerType.RightArmOffSpin: return "RIGHT ARM OFF SPIN";
            case BowlerType.RightArmLegSpin: return "RIGHT ARM LEG SPIN";
            case BowlerType.LeftArmOrthodoxSpin: return "LEFT ARM ORTHODOX SPIN";
            case BowlerType.LeftArmWristSpin: return "LEFT ARM WRIST SPIN";
        }
        return "";
    }
}
