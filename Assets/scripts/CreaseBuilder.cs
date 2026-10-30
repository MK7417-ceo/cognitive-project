using UnityEngine;

/// <summary>Real-world creases (Law 7), built at runtime so no scene edit is needed.
/// Bowling crease 2.64 m wide, popping crease 1.22 m in front of it and
/// 3.66 m long, return creases at right angles. Built at both ends.</summary>
public static class CreaseBuilder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Build()
    {
        if (Object.FindAnyObjectByType<CreaseMarker>() != null) return;
        GameObject wicket = GameObject.Find("StumpTarget");
        if (wicket == null) wicket = GameObject.Find("MainWicketHitZone");
        GameObject bowler = GameObject.Find("Bowler");
        if (wicket == null || bowler == null) return;
        Vector3 wicketPos = wicket.transform.position; wicketPos.y = 0.01f;
        Vector3 bowlerPos = bowler.transform.position; bowlerPos.y = 0.01f;
        Vector3 pitchDir = (bowlerPos - wicketPos); pitchDir.y = 0f;
        if (pitchDir.sqrMagnitude < 1f) return;
        pitchDir.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, pitchDir).normalized;
        GameObject root = new GameObject("RealCreases");
        root.AddComponent<CreaseMarker>();
        BuildEnd(root.transform, wicketPos, pitchDir, lateral);
        BuildEnd(root.transform, bowlerPos, -pitchDir, lateral);
    }

    static void BuildEnd(Transform root, Vector3 centre, Vector3 towardOtherEnd, Vector3 lateral)
    {
        // Bowling crease: through the stumps, 2.64 m wide.
        Line(root, centre, lateral, 2.64f);
        // Popping crease: 1.22 m in front, 3.66 m long.
        Line(root, centre + towardOtherEnd * 1.22f, lateral, 3.66f);
        // Return creases: from popping crease back 1.32 m, at 1.32 m either side of centre.
        Line(root, centre + lateral * 1.32f + towardOtherEnd * 0.61f, towardOtherEnd, 1.32f);
        Line(root, centre - lateral * 1.32f + towardOtherEnd * 0.61f, towardOtherEnd, 1.32f);
    }

    static void Line(Transform root, Vector3 centre, Vector3 dir, float length)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "CreaseLine";
        go.transform.SetParent(root, false);
        go.transform.position = centre;
        go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        go.transform.localScale = new Vector3(0.05f, 0.005f, length);
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.material = new Material(Shader.Find("Standard")) { color = Color.white };
        Object.Destroy(go.GetComponent<Collider>());
    }
}

public sealed class CreaseMarker : MonoBehaviour { }
