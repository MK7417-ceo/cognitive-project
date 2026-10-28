using UnityEngine;

/// <summary>
/// Dedicated ball-tracking camera that smoothly follows and frames the cricket ball.
/// Tracks the ball from bowler release, through pitch bounce, to bat strike and boundary flight.
/// When no ball is active, smoothly frames the pitch/bowler in anticipation of the next ball.
/// </summary>
public class BallTrackingCamera : MonoBehaviour
{
    [Header("Tracking Parameters")]
    [Tooltip("Smoothing speed for camera rotation to follow the ball")]
    public float lookDamping = 8.0f;

    [Tooltip("Smoothing speed for camera position following")]
    public float positionDamping = 4.0f;

    [Tooltip("Offset relative to the ball in tracking mode")]
    public Vector3 followOffset = new Vector3(3.0f, 2.5f, -4.0f);

    [Tooltip("Field of View when tracking the ball")]
    public float trackingFov = 45f;
    public float idleFov = 60f;

    private Camera cam;
    private Transform currentTarget;
    private Vector3 defaultIdlePosition;
    private Quaternion defaultIdleRotation;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultIdlePosition = transform.position;
        defaultIdleRotation = transform.rotation;
    }

    void LateUpdate()
    {
        // 1. Locate current active ball
        FindActiveBall();

        if (currentTarget != null)
        {
            TrackActiveBall();
        }
        else
        {
            IdleStance();
        }
    }

    void FindActiveBall()
    {
        // Check if existing target is still valid
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            return;

        currentTarget = null;

        // Find active BallHitPhysics or BallThrower
        var ballHit = FindAnyObjectByType<BallHitPhysics>();
        if (ballHit != null && ballHit.gameObject.activeInHierarchy)
        {
            currentTarget = ballHit.transform;
            return;
        }

        var ballThrower = FindAnyObjectByType<BallThrower>();
        if (ballThrower != null && ballThrower.gameObject.activeInHierarchy)
        {
            currentTarget = ballThrower.transform;
        }
    }

    void TrackActiveBall()
    {
        Vector3 ballPos = currentTarget.position;

        // Smoothly orient camera towards the ball
        Vector3 directionToBall = (ballPos - transform.position).normalized;
        if (directionToBall.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToBall, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookDamping);
        }

        // Adjust position dynamically to keep both ball and context visible
        Vector3 desiredPosition = ballPos + followOffset;
        if (desiredPosition.y < 0.8f) desiredPosition.y = 0.8f; // Stay above ground

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * positionDamping);

        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, trackingFov, Time.deltaTime * 3f);
        }
    }

    void IdleStance()
    {
        // Return smoothly to the default pitch observation position
        transform.position = Vector3.Lerp(transform.position, defaultIdlePosition, Time.deltaTime * 2.0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, defaultIdleRotation, Time.deltaTime * 2.0f);

        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, idleFov, Time.deltaTime * 2.0f);
        }
    }

    /// <summary>
    /// Calibrate default observation position based on the pitch centre.
    /// </summary>
    public void CalibrateIdlePosition(Vector3 pitchCentre, Vector3 bowlerToBatsman)
    {
        Vector3 right = Vector3.Cross(Vector3.up, bowlerToBatsman).normalized;
        defaultIdlePosition = pitchCentre + right * 7.0f + Vector3.up * 3.5f;
        defaultIdleRotation = Quaternion.LookRotation(pitchCentre - defaultIdlePosition, Vector3.up);
    }
}
