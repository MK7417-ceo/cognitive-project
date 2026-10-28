using UnityEngine;

/// <summary>
/// The wicket-keeper: crouches behind the stumps, shuffles sideways to follow
/// the ball, steps out to gather wides/short balls, and catches edges or balls
/// the batsman leaves. Built at runtime by CricketMatchManager — no scene
/// edits, no model files needed.
/// </summary>
public class WicketKeeper : MonoBehaviour
{
    const float CatchRadius = 1.1f;   // glove reach, metres
    const float TrackRange = 2.5f;    // how far sideways he shuffles
    const float TrackSpeed = 4.5f;    // shuffle speed m/s

    Transform gloveAnchor;
    Vector3 homePos;
    BallHitPhysics heldBall;
    float holdTimer;

    public static WicketKeeper Build(Vector3 stumpPos, Vector3 awayFromBowler)
    {
        var existing = FindAnyObjectByType<WicketKeeper>();
        if (existing != null) return existing;

        Vector3 pos = stumpPos + awayFromBowler * 1.6f;
        pos.y = 0f;

        var root = new GameObject("WicketKeeper");
        root.transform.position = pos;
        // Face the bowler.
        root.transform.rotation = Quaternion.LookRotation(-awayFromBowler, Vector3.up);

        var wk = root.AddComponent<WicketKeeper>();
        wk.homePos = pos;
        wk.Construct();
        Debug.Log($"WicketKeeper: standing at {pos}");
        return wk;
    }

    void Construct()
    {
        // Tint the primitives' own default materials instead of Shader.Find:
        // always works, no dependency on which shaders the project ships.

        // Crouched body.
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f);
        body.GetComponent<Renderer>().material.color = new Color(0.08f, 0.14f, 0.45f); // dark blue kit
        Destroy(body.GetComponent<Collider>());

        // Head.
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 1.15f, 0.1f);
        head.transform.localScale = Vector3.one * 0.28f;
        head.GetComponent<Renderer>().material.color = new Color(0.85f, 0.62f, 0.47f);
        Destroy(head.GetComponent<Collider>());

        // Gloves: the catch point, held out in front.
        gloveAnchor = new GameObject("Gloves").transform;
        gloveAnchor.SetParent(transform, false);
        gloveAnchor.localPosition = new Vector3(0f, 0.6f, 0.55f);

        for (int i = -1; i <= 1; i += 2)
        {
            var glove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glove.transform.SetParent(gloveAnchor, false);
            glove.transform.localPosition = new Vector3(i * 0.16f, 0f, 0f);
            glove.transform.localScale = Vector3.one * 0.22f;
            glove.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.9f);
            Destroy(glove.GetComponent<Collider>());
        }
    }

    void Update()
    {
        // Holding a caught ball: keep it in the gloves, then clear it.
        if (heldBall != null)
        {
            holdTimer -= Time.deltaTime;
            if (holdTimer <= 0f)
            {
                if (heldBall != null) Destroy(heldBall.gameObject);
                heldBall = null;
            }
            return;
        }

        var ball = FindAnyObjectByType<BallHitPhysics>();

        Vector3 target = homePos;
        float speed = 2f;

        if (ball != null && ball.State == BallHitPhysics.BallState.Flying)
        {
            Vector3 bp = ball.transform.position;
            Vector3 toBall = bp - homePos;
            toBall.y = 0f;

            // React once the ball is in the keeper's half (past the pitch).
            if (toBall.magnitude < 14f)
            {
                // Shuffle sideways with the ball; step OUT (forward) for
                // short/wide balls instead of staying glued to the spot.
                Vector3 offset = Vector3.ClampMagnitude(toBall, TrackRange);
                target = homePos + offset;
                speed = TrackSpeed;
            }

            // Catch anything that reaches the gloves.
            if (Vector3.Distance(gloveAnchor.position, bp) < CatchRadius)
            {
                Catch(ball);
                return;
            }
        }

        target.y = 0f;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // Always face the ball when it's live.
        if (ball != null && ball.State == BallHitPhysics.BallState.Flying)
        {
            Vector3 look = ball.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(look, Vector3.up), 8f * Time.deltaTime);
        }
    }

    void Catch(BallHitPhysics ball)
    {
        ball.MarkCaught();
        ball.transform.SetParent(gloveAnchor, true);
        heldBall = ball;
        holdTimer = 2f;

        var ui = CricketMatchManager.Instance;
        if (ui != null) ui.ShowMessage("CAUGHT BEHIND!", Color.cyan);
        Debug.Log("WicketKeeper: CAUGHT BEHIND!");
    }
}
