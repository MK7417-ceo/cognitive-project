using UnityEngine;

public class BallThrower : MonoBehaviour
{
    public static GameObject ActiveMatchBall { get; private set; }

    private Rigidbody rb;

    [Header("Ball Life Settings")]
    public float lifeTime = 5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("BallThrower: Rigidbody missing!");
        }
    }

    public static void TrackMatchBall(GameObject ball)
    {
        ActiveMatchBall = ball;
    }

    public static void ReleaseMatchBall(GameObject ball)
    {
        if (ActiveMatchBall == ball)
            ActiveMatchBall = null;
    }

    // Called ONLY on ball clone
    public void Launch(
        Vector3 startPos,
        Vector3 targetPos,
        float timeToTarget,
        float swingMeters,
        Vector3 spinTorque
    )
    {
        if (rb == null) return;

        TrackMatchBall(gameObject);

        // Reset velocities
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // ---------- trajectory calculation ----------
        Vector3 displacement = targetPos - startPos;
        Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
        float vertical = displacement.y;

        float horizontalSpeed = horizontal.magnitude / timeToTarget;

        float verticalSpeed =
            (vertical - 0.5f * Physics.gravity.y * timeToTarget * timeToTarget)
            / timeToTarget;

        Vector3 launchVelocity =
            horizontal.normalized * horizontalSpeed +
            Vector3.up * verticalSpeed;

        Vector3 swingDir = Vector3.Cross(Vector3.up, horizontal.normalized);
        Vector3 swingVelocity = swingDir.normalized * (swingMeters / timeToTarget);

        rb.linearVelocity = launchVelocity + swingVelocity;
        rb.angularVelocity = spinTorque;
        // --------------------------------------------

        // 🔥 DESTROY ONLY THIS BALL INSTANCE
        Destroy(gameObject, lifeTime);
    }

    private void OnDestroy()
    {
        ReleaseMatchBall(gameObject);
    }
}
