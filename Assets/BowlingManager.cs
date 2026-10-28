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

        runUpStartPos = transform.position;
        runUpStartRot = transform.rotation;

        currentBowler = bowlerOrder[bowlerIndex];
        ApplyBowlerType();
        UpdateBowlingStyleUI();
    }

    void ApplyBowlerType()
    {
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
        if (animator != null)
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
        if (v == null)
        {
            Debug.LogWarning("BowlingManager: no ball variations available.");
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

        BallThrower ballThrower = ball.GetComponent<BallThrower>();
        if (ballThrower == null)
        {
            Debug.LogError("BowlingManager: BallThrower component is missing on the ball prefab.");
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
        ballCount++;

        if (ballCount >= ballsPerOver)
        {
            ballCount = 0;
            bowlerIndex = (bowlerIndex + 1) % bowlerOrder.Length;
            currentBowler = bowlerOrder[bowlerIndex];
            UpdateBowlingStyleUI();
        }

        // Real rhythm: pause, walk back to the run-up start, THEN bowl again.
        // (Previously the trigger re-fired instantly, so the bowler bowled
        // nonstop and the run-up broke after a few balls.)
        if (!waitingForNextBall)
            StartCoroutine(NextBallRoutine());
    }

    IEnumerator NextBallRoutine()
    {
        waitingForNextBall = true;

        yield return new WaitForSeconds(betweenBallsDelay);

        // Walk back: return to the run-up start like a real bowler turning around.
        var runUp = GetComponent<RunUpController>();
        if (runUp != null)
            runUp.BeginRunUp();
        else
        {
            transform.position = runUpStartPos;
            transform.rotation = runUpStartRot;
        }

        waitingForNextBall = false;

        // Re-fire the Bowl trigger for the next delivery.
        // Bowler rotation still happens only on over completion.
        ApplyBowlerType();
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
