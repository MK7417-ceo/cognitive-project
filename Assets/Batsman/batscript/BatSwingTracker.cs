using UnityEngine;

/// <summary>
/// Measures the bat's real swing velocity in world metres/second.
/// Combines linear handle displacement with angular rotation speed,
/// ensuring that wrist snaps and rotational swings register true strike speed
/// even when the handle pivot stays stationary.
/// Works seamlessly for mouse swings, keyboard swings, and VR controller swings.
/// </summary>
public class BatSwingTracker : MonoBehaviour
{
    [Header("Detection Thresholds")]
    [Tooltip("Below this speed a contact is treated as a glance / leave (m/s)")]
    public float hitThreshold = 3.5f;

    [Tooltip("Between block and hit threshold the ball is defensively blocked (m/s)")]
    public float blockThreshold = 1.0f;

    [Tooltip("Estimated distance from bat pivot to sweet spot / blade tip (meters)")]
    public float bladeLength = 0.70f;

    private Vector3 prevRootPos;
    private Vector3 prevTipPos;
    private Quaternion prevRot;

    private Vector3 smoothVel;
    private float smoothSpeed;
    private float maxRecentSpeed;
    private float recentTimer;
    private const float RecentWindow = 0.25f;

    void Start()
    {
        prevRootPos = transform.position;
        prevTipPos = GetTipPosition();
        prevRot = transform.rotation;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt > 0.0001f)
        {
            Vector3 currentRootPos = transform.position;
            Vector3 currentTipPos = GetTipPosition();

            // 1. Linear root velocity
            Vector3 vRoot = (currentRootPos - prevRootPos) / dt;

            // 2. Tip velocity in world space
            Vector3 vTip = (currentTipPos - prevTipPos) / dt;

            // 3. Angular velocity contribution (omega * r)
            float angleDelta = Quaternion.Angle(transform.rotation, prevRot);
            float angularSpeedRad = (angleDelta * Mathf.Deg2Rad) / dt;
            float rotationalTipSpeed = angularSpeedRad * bladeLength;

            // Effective physical impact speed is whichever is higher: direct tip motion or angular+root
            float instantSpeed = Mathf.Max(vTip.magnitude, vRoot.magnitude + rotationalTipSpeed);

            // Responsive smoothing
            smoothSpeed = Mathf.Lerp(smoothSpeed, instantSpeed, 0.45f);

            // Effective directional vector
            Vector3 effectiveVel = (vTip.sqrMagnitude > 0.04f) ? vTip : (vRoot + transform.forward * rotationalTipSpeed);
            smoothVel = Vector3.Lerp(smoothVel, effectiveVel, 0.45f);

            if (smoothSpeed > maxRecentSpeed)
            {
                maxRecentSpeed = smoothSpeed;
            }

            prevRootPos = currentRootPos;
            prevTipPos = currentTipPos;
            prevRot = transform.rotation;
        }

        recentTimer += Time.deltaTime;
        if (recentTimer >= RecentWindow)
        {
            recentTimer = 0f;
            maxRecentSpeed = smoothSpeed;
        }
    }

    Vector3 GetTipPosition()
    {
        // Sample sweet-spot along local UP (matches standard cricket bat alignment in SnapSceneBatToHand)
        return transform.position + transform.up * bladeLength;
    }

    /// <summary>Peak swing speed over the last quarter second (m/s).</summary>
    public float GetSwingSpeed()
    {
        return Mathf.Max(smoothSpeed, maxRecentSpeed);
    }

    /// <summary>Swing direction in world space (full 3D).</summary>
    public Vector3 GetSwingDirection()
    {
        if (smoothVel.sqrMagnitude > 0.01f)
            return smoothVel.normalized;
        return transform.forward;
    }

    public bool IsRealSwing()
    {
        return GetSwingSpeed() >= hitThreshold;
    }

    public bool IsBlock()
    {
        float s = GetSwingSpeed();
        return s >= blockThreshold && s < hitThreshold;
    }
}
