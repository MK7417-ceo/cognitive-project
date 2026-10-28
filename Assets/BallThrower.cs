using UnityEngine;

public class BallThrower : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Ball Life Settings")]
    [Tooltip("LEGACY, kept only so the existing ball prefab does not lose a serialized field. It is intentionally NOT used anymore: BallHitPhysics owns the ball's lifetime, so a ball can never vanish mid-flight to the boundary just because a fixed timer expired (Phase 1 / bowler-timing fix).")]
    public float lifeTime = 5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("BallThrower: Rigidbody missing!");
        }
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

        // Phase 1 fix: NO Destroy(gameObject, lifeTime) here anymore.
        // That fixed 5-second timer was the boundary-vanish bug: a ball hit
        // late, or rolling slowly to the boundary, was deleted mid-flight
        // when the timer expired, regardless of what the ball was doing.
        // BallHitPhysics (added at runtime by CricketMatchManager) now owns
        // cleanup: a live ball lives until it is dead, caught, bowled, or
        // has crossed the boundary and settled, with a stale-ball fallback.
    }
}
