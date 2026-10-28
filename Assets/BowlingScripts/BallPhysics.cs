using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[System.Obsolete("BallPhysics is superseded by BallThrower (launch trajectory) and BallHitPhysics (impact & match physics).")]
public class BallPhysics : MonoBehaviour
{
    public enum DeliveryType { 
        NormalFast, InSwing, OutSwing, Cutter, Bouncer, Yorker, Slower,
        NormalSpin, Googly, Doosra, TopSpin 
    }
    public enum Arm { Right, Left }

    [Header("Current Ball Setup")]
    public DeliveryType currentDelivery;
    public Arm bowlerArm;
    public Transform rightHandBatsmanStumps; 

    [Header("Physics Speeds")]
    public float fastSpeed = 30f;
    public float spinSpeed = 18f;
    public float slowerSpeed = 22f;

    private Rigidbody rb;
    private bool isBallInAir = false;
    private bool hasBounced = false;
    // Stable lateral axis for swing/turn — computed per delivery, does not rotate with the ball
    private Vector3 lateralAxis = Vector3.right;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("BallPhysics: Rigidbody missing on " + gameObject.name + ". Add a Rigidbody component.");
            enabled = false;
            return;
        }
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Auto-Find Stumps
        if (rightHandBatsmanStumps == null)
        {
            GameObject target = GameObject.Find("StumpTarget");
            if (target != null)
                rightHandBatsmanStumps = target.transform;
            else
                Debug.LogWarning("BallPhysics: rightHandBatsmanStumps not assigned and no 'StumpTarget' found in scene.");
        }
    }

    public void BowlRealistically(Vector3 releasePosition)
    {
        if (rb == null)
        {
            Debug.LogError("BallPhysics: cannot bowl, Rigidbody is missing.");
            return;
        }
        if (rightHandBatsmanStumps == null)
        {
            Debug.LogError("BallPhysics: cannot bowl, no stumps target assigned or found.");
            return;
        }

        rb.isKinematic = false;
        isBallInAir = true;
        hasBounced = false;

        // 1. Target Calculate karna
        Vector3 finalTarget = CalculateTarget(releasePosition);

        // 2. Fekne ki disha nikalna (gravity-compensated projectile)
        Vector3 toTarget = finalTarget - releasePosition;
        Vector3 horizontal = new Vector3(toTarget.x, 0f, toTarget.z);
        if (horizontal.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("BallPhysics: release position is directly above/below the target.");
            return;
        }

        // Stable lateral axis for swing/turn (same convention as CalculateTarget's rightSide)
        lateralAxis = Vector3.Cross(Vector3.up, horizontal.normalized).normalized;

        // Delivery ke hisaab se speed, us se flight time — gravity ka hisaab (BallThrower jaisa)
        float speedToApply = SetBallSpeed();
        float flightTime = horizontal.magnitude / speedToApply;
        float verticalSpeed =
            (toTarget.y - 0.5f * Physics.gravity.y * flightTime * flightTime) / flightTime;

        Vector3 launchVelocity =
            horizontal.normalized * speedToApply +
            Vector3.up * verticalSpeed;

        if (float.IsNaN(launchVelocity.x + launchVelocity.y + launchVelocity.z))
        {
            Debug.LogError("BallPhysics: launch velocity is NaN. Check release/target positions.");
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = launchVelocity;
    }

    private Vector3 CalculateTarget(Vector3 releasePosition)
    {
        if (rightHandBatsmanStumps == null) return new Vector3(0, 0, 0);

        Vector3 targetPos = rightHandBatsmanStumps.position;
        
        // Stumps aur ball ke beech ka theek raasta nikalna (Bina rotation ke)
        Vector3 straightLine = (targetPos - releasePosition).normalized;
        Vector3 rightSide = Vector3.Cross(Vector3.up, straightLine).normalized;

        // Length (Aage-Peeche)
        if (currentDelivery == DeliveryType.Bouncer) 
            targetPos -= straightLine * 7f; 
        else if (currentDelivery == DeliveryType.Yorker) 
            targetPos -= straightLine * 1f; 
        else 
            targetPos -= straightLine * Random.Range(3f, 5f); 

        // Accuracy (Daayein-Baayein)
        int wideChance = Random.Range(0, 100);
        if (wideChance < 10) 
        {
            float wideError = Random.Range(1.5f, 2.5f);
            targetPos += rightSide * ((Random.Range(0, 2) == 0) ? wideError : -wideError);
        }
        else 
        {
            // Accuracy aur tight kar di hai (0.2 meters ke andar)
            targetPos += rightSide * Random.Range(-0.2f, 0.2f); 
        }

        // DEBUG LASER: Scene view mein dekho ki nishana kahan hai
        Debug.DrawLine(releasePosition, targetPos, Color.red, 5f);

        return targetPos;
    }

    private float SetBallSpeed()
    {
        if (currentDelivery == DeliveryType.Slower) return slowerSpeed;
        if (currentDelivery == DeliveryType.NormalSpin || currentDelivery == DeliveryType.Googly || currentDelivery == DeliveryType.Doosra || currentDelivery == DeliveryType.TopSpin) return spinSpeed;
        return fastSpeed;
    }

    void FixedUpdate()
    {
        if (rb == null || !isBallInAir || hasBounced) return;

        float swingDir = 0f;
        if (currentDelivery == DeliveryType.InSwing) swingDir = (bowlerArm == Arm.Right) ? -1f : 1f;
        if (currentDelivery == DeliveryType.OutSwing) swingDir = (bowlerArm == Arm.Right) ? 1f : -1f;

        if (swingDir != 0f) rb.AddForce(lateralAxis * (6f * swingDir), ForceMode.Force);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (rb == null) return;
        if (collision.gameObject.CompareTag("Pitch") && !hasBounced)
        {
            isBallInAir = false;
            hasBounced = true;

            float turnDir = 0f;
            if (currentDelivery == DeliveryType.NormalSpin) turnDir = (bowlerArm == Arm.Right) ? -1f : 1f; 
            // Googly stock ball se ULTI direction me ghoomti hai (pehle same thi — copy-paste bug tha)
            if (currentDelivery == DeliveryType.Googly) turnDir = (bowlerArm == Arm.Right) ? 1f : -1f; 
            if (currentDelivery == DeliveryType.Doosra) turnDir = (bowlerArm == Arm.Right) ? 1f : -1f; 
            if (currentDelivery == DeliveryType.Cutter) turnDir = (Random.Range(0, 2) == 0) ? -0.5f : 0.5f;

            if (turnDir != 0f) rb.AddForce(lateralAxis * (8f * turnDir), ForceMode.Impulse);
        }
    }
}
