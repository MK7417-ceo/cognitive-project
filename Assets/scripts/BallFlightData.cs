using UnityEngine;

/// <summary>
/// Tag attached to every bowled ball at spawn (by BowlingManager), describing
/// WHAT was bowled. BallHitPhysics reads it on the first pitch and makes the
/// ball react like the real thing: fast balls skid through, spinners grip and
/// turn, googlies turn the other way, bouncers climb, yorkers stay low.
/// Left-arm deliveries are mirrored via isLeftArm.
/// </summary>
public class BallFlightData : MonoBehaviour
{
    public enum SpinKind
    {
        Fast,      // seam / fast: skids through after pitching
        OffSpin,   // fingerspin turning one way (mirrored for left-arm orthodox)
        LegSpin,   // wristspin turning the other way (mirrored for left-arm wrist)
        Googly,    // wrong'un: turns opposite to the leg-spinner's stock ball
        Bouncer,   // climbs steeply off the pitch
        Yorker     // stays low, skids under the bat
    }

    public SpinKind spinKind = SpinKind.Fast;
    public string variationName = "";
    public bool isLeftArm = false;
    [HideInInspector] public Vector3 deliveryDirection;
}
