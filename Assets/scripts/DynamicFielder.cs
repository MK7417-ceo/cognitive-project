using UnityEngine;

/// <summary>A human fielder who moves toward and gathers a nearby live ball.</summary>
public class DynamicFielder : MonoBehaviour
{
    public const float CatchRadius = 1.25f;
    public const float RunSpeed = 8f;

    Vector3 homePosition;
    Transform handTarget;
    BallHitPhysics targetBall;
    float returnSpeed = 5f;

    public bool IsAvailable => targetBall == null || targetBall.State == BallHitPhysics.BallState.Dead;

    public void Initialize(Vector3 home)
    {
        homePosition = home;
        handTarget = new GameObject("FielderHands").transform;
        handTarget.SetParent(transform, false);
        handTarget.localPosition = new Vector3(0f, 1.0f, 0.32f);
    }

    public void AssignBall(BallHitPhysics ball)
    {
        targetBall = ball;
    }

    public void ClearBall()
    {
        targetBall = null;
    }

    void Update()
    {
        if (targetBall == null || targetBall.State == BallHitPhysics.BallState.Dead)
        {
            targetBall = null;
            transform.position = Vector3.MoveTowards(transform.position, homePosition, returnSpeed * Time.deltaTime);
            return;
        }

        Vector3 ballPosition = targetBall.transform.position;
        Rigidbody ballBody = targetBall.GetComponent<Rigidbody>();
        Vector3 velocity = ballBody != null ? ballBody.linearVelocity : Vector3.zero;
        Vector3 predicted = ballPosition + velocity * 0.2f;
        predicted.y = 0f;

        Vector3 toBall = predicted - transform.position;
        toBall.y = 0f;
        if (toBall.sqrMagnitude > 0.04f)
        {
            Quaternion facing = Quaternion.LookRotation(toBall, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, 8f * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(transform.position, predicted, RunSpeed * Time.deltaTime);

        float catchHeight = targetBall.State == BallHitPhysics.BallState.Hit ? 2.5f : 2.1f;
        if (Vector3.Distance(handTarget.position, ballPosition) <= CatchRadius && ballPosition.y <= catchHeight)
            FieldBall(targetBall);
    }

    void FieldBall(BallHitPhysics ball)
    {
        if (ball == null || ball.State == BallHitPhysics.BallState.Dead) return;

        // Same Law 33 rule as the keeper: capture before MarkCaught().
        bool caughtOut = ball.WouldBeCaughtOut;
        bool runOut = ball.MarkCaught();
        ball.transform.SetParent(handTarget, true);

        if (CricketMatchManager.Instance != null)
        {
            if (runOut)
                CricketMatchManager.Instance.ShowMessage("RUN OUT!", Color.green);
            else if (caughtOut)
                CricketMatchManager.Instance.ShowMessage("CAUGHT!", Color.green);
            else
                CricketMatchManager.Instance.ShowMessage("FIELDED!", Color.green);
        }
        Debug.Log($"DynamicFielder: {gameObject.name} gathered the ball. caughtOut={caughtOut} runOut={runOut}");
        targetBall = null;
    }
}
