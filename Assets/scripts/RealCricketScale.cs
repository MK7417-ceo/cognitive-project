using UnityEngine;

/// <summary>
/// REAL CRICKET DIMENSIONS (MCC Laws), applied at runtime, no scene edit.
/// - Pitch: 20.12 m, stumps to stumps (Law 6). The visible wicket
///   (StumpTarget) is kept as truth; the bowler is moved to exactly
///   20.12 m from it along the existing bowler-to-stumps line, and the
///   batsman (VR rig + hidden scene batsman) is put 0.6 m in front of
///   the stumps, toward the bowler. Before this, bowler-to-stumps was
///   only ~8.3 m and the batsman stood ~11 m off the bowling line, so
///   balls flew to the stumps and never reached the batsman.
/// - Eye height: 1.65 m. Before, XR Origin y 1.45 + Camera Offset
///   y 1.12 = 2.57 m eye height - a giant. Now Origin y 0, Offset 1.65.
/// - Boundary is in CricketMatchManager (70 m, ICC 59-82 m range).
/// - Ball 0.072 m, bat 0.965 m, creases Law 7, stumps Law 8 are in
///   their own files; this file only fixes pitch length + eye height.
/// </summary>
public static class RealCricketScale
{
    public const float PitchLength = 20.12f;
    public const float BatsmanInFrontOfStumps = 0.6f;
    public const float EyeHeight = 1.65f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        var wicket = GameObject.Find("StumpTarget");
        var bowlerGo = GameObject.Find("Bowler");
        if (wicket == null || bowlerGo == null) return;

        Vector3 stumps = wicket.transform.position; stumps.y = 0f;
        Vector3 bowlerPos = bowlerGo.transform.position; bowlerPos.y = 0f;
        Vector3 dir = stumps - bowlerPos; dir.y = 0f;
        if (dir.sqrMagnitude < 1f) return;
        dir.Normalize();

        // Bowler to exactly 20.12 m from the stumps.
        Vector3 newBowlerPos = stumps - dir * PitchLength;
        bowlerGo.transform.position = newBowlerPos;

        // Batsman: 0.6 m in front of the stumps, facing the bowler.
        Vector3 batsmanPos = stumps + dir * BatsmanInFrontOfStumps;
        var batsman = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (batsman != null)
        {
            Vector3 bp = batsman.transform.position;
            batsman.transform.position = new Vector3(batsmanPos.x, bp.y, batsmanPos.z);
        }

        // VR eye height 1.65 m, not 2.57 m.
        var xr = GameObject.Find("XR Origin (VR)");
        if (xr != null)
        {
            Vector3 xp = xr.transform.position;
            xr.transform.position = new Vector3(xp.x, 0f, xp.z);
            var offset = xr.transform.Find("Camera Offset");
            if (offset != null)
            {
                Vector3 op = offset.localPosition;
                offset.localPosition = new Vector3(op.x, EyeHeight, op.z);
            }
        }
        Debug.Log($"RealCricketScale: pitch {PitchLength} m, bowler at {newBowlerPos}, batsman at {batsmanPos}, eye {EyeHeight} m.");
    }

    /// <summary>Real batsman rig position, computed live (order-independent).
    /// Returns false if stumps/bowler are missing; caller falls back.</summary>
    public static bool TryGetBatsmanRigPosition(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        var wicket = GameObject.Find("StumpTarget");
        var bowlerGo = GameObject.Find("Bowler");
        if (wicket == null || bowlerGo == null) return false;
        Vector3 stumps = wicket.transform.position; stumps.y = 0f;
        Vector3 bowlerPos = bowlerGo.transform.position; bowlerPos.y = 0f;
        Vector3 dir = stumps - bowlerPos; dir.y = 0f;
        if (dir.sqrMagnitude < 1f) return false;
        dir.Normalize();
        pos = stumps + dir * BatsmanInFrontOfStumps;
        pos.y = 0f;
        // Face the bowler.
        rot = Quaternion.LookRotation(-dir, Vector3.up);
        return true;
    }
}
