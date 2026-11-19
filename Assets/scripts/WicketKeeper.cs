using UnityEngine;

/// <summary>Humanoid wicketkeeper that tracks, moves for, and gathers the ball.</summary>
public class WicketKeeper : MonoBehaviour
{
    const float CatchRadius = 1.25f;
    const float TrackRange = 3.5f;
    const float TrackSpeed = 7.5f;
    const float ReactRange = 16f;

    Transform gloveAnchor;
    Vector3 homePos;
    BallHitPhysics heldBall;
    float holdTimer;

    public static WicketKeeper Build(Vector3 stumpPos, Vector3 awayFromBowler, BowlingManager humanTemplate)
    {
        var existing = FindAnyObjectByType<WicketKeeper>();
        if (existing != null) return existing;

        Vector3 pos = stumpPos + awayFromBowler * 1.7f;
        pos.y = 0f;
        Quaternion facingBowler = Quaternion.LookRotation(-awayFromBowler, Vector3.up);
        GameObject root = HumanCricketActor.Create(humanTemplate, "HumanWicketKeeper", pos, facingBowler);
        if (root == null) return null;

        var keeper = root.AddComponent<WicketKeeper>();
        keeper.homePos = pos;
        keeper.Construct();
        Debug.Log($"WicketKeeper: human keeper positioned at {pos}");
        return keeper;
    }

    public static WicketKeeper Build(Vector3 stumpPos, Vector3 awayFromBowler)
    {
        return Build(stumpPos, awayFromBowler,
            FindAnyObjectByType<BowlingManager>(FindObjectsInactive.Include));
    }

    void Construct()
    {
        gloveAnchor = new GameObject("KeeperGloveTarget").transform;
        gloveAnchor.SetParent(transform, false);
        gloveAnchor.localPosition = new Vector3(0f, 1.0f, 0.38f);
    }

    void Update()
    {
        if (heldBall != null)
        {
            holdTimer -= Time.deltaTime;
            if (holdTimer <= 0f && heldBall != null)
            {
                Destroy(heldBall.gameObject);
                heldBall = null;
            }
            return;
        }

        BallHitPhysics ball = FindLiveBall();
        bool liveBall = ball != null &&
            (ball.State == BallHitPhysics.BallState.Flying || ball.State == BallHitPhysics.BallState.Hit);

        Vector3 target = homePos;
        float speed = 2f;

        if (liveBall)
        {
            Vector3 ballPos = ball.transform.position;
            Vector3 toBall = ballPos - homePos;
            toBall.y = 0f;

            if (toBall.magnitude <= ReactRange)
            {
                target = homePos + Vector3.ClampMagnitude(toBall, TrackRange);
                speed = TrackSpeed;

                Vector3 look = ballPos - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.04f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(look, Vector3.up), 10f * Time.deltaTime);
            }

            if (Vector3.Distance(gloveAnchor.position, ballPos) <= CatchRadius)
            {
                Catch(ball);
                return;
            }
        }

        target.y = 0f;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    void Catch(BallHitPhysics ball)
    {
        // Capture BEFORE MarkCaught(): it sets the ball Dead, after which
        // WouldBeCaughtOut can no longer be read. A Flying delivery the
        // keeper gathers untouched is a dot ball (Law 33), not a dismissal.
        bool caughtOut = ball.WouldBeCaughtOut;
        bool runOut = ball.MarkCaught();
        ball.transform.SetParent(gloveAnchor, true);
        heldBall = ball;
        holdTimer = 1.5f;

        if (CricketMatchManager.Instance != null)
        {
            if (runOut)
                CricketMatchManager.Instance.ShowMessage("RUN OUT BY THE KEEPER!", Color.cyan);
            else if (caughtOut)
                CricketMatchManager.Instance.ShowMessage("CAUGHT BY THE KEEPER!", Color.cyan);
            else
                // Training feedback only: an untouched delivery gathered by
                // the keeper is a dot ball. Never show "CAUGHT" for it.
                CricketMatchManager.Instance.ShowMessage("DOT BALL", Color.white, 1.0f);
        }
        Debug.Log($"WicketKeeper: gathered the ball. caughtOut={caughtOut} runOut={runOut}");
    }

    static BallHitPhysics FindLiveBall()
    {
        GameObject activeBall = BallThrower.ActiveMatchBall;
        BallHitPhysics activePhysics = activeBall != null
            ? activeBall.GetComponent<BallHitPhysics>()
            : null;
        if (activePhysics != null && activePhysics.State != BallHitPhysics.BallState.Dead)
            return activePhysics;

        foreach (BallHitPhysics candidate in FindObjectsByType<BallHitPhysics>(FindObjectsInactive.Exclude))
        {
            if (candidate != null &&
                candidate.State != BallHitPhysics.BallState.Dead &&
                candidate.gameObject.activeInHierarchy)
                return candidate;
        }
        return null;
    }
}
