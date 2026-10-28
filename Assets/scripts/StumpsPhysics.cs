using UnityEngine;

/// <summary>
/// Builds realistic stumps (3 stumps + 2 bails) behind the batsman at runtime
/// and knocks them flying when the ball hits — like a real "bowled" dismissal.
/// </summary>
public class StumpsPhysics : MonoBehaviour
{
    Rigidbody[] pieces;
    bool knocked;

    /// <summary>Build stumps once, placed behind the batsman away from the bowler.</summary>
    public static StumpsPhysics Build(Vector3 batsmanPos, Vector3 bowlerPos)
    {
        var existing = FindAnyObjectByType<StumpsPhysics>();
        if (existing != null) return existing;

        Vector3 away = batsmanPos - bowlerPos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        away.Normalize();

        Vector3 rootPos = batsmanPos + away * 1.0f;
        rootPos.y = 0f;

        var root = new GameObject("Stumps");
        root.transform.position = rootPos;
        // Row of stumps runs perpendicular to the ball's path.
        root.transform.rotation = Quaternion.LookRotation(away, Vector3.up);

        var sp = root.AddComponent<StumpsPhysics>();
        sp.Construct();
        Debug.Log($"StumpsPhysics: stumps built at {rootPos}");
        return sp;
    }

    void Construct()
    {
        // Tint the primitives' own default materials instead of Shader.Find:
        // always works, no dependency on which shaders the project ships.
        pieces = new Rigidbody[5];
        float stumpH = 0.71f; // real stump height (m)

        // Unity's cylinder primitive is 2 units tall, 1 unit in diameter.
        float[] xs = { -0.114f, 0f, 0.114f }; // 0.228 m total width, like real stumps
        for (int i = 0; i < 3; i++)
        {
            var st = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            st.name = "Stump" + (i + 1);
            st.transform.SetParent(transform, false);
            st.transform.localPosition = new Vector3(xs[i], stumpH / 2f, 0f);
            st.transform.localScale = new Vector3(0.038f, stumpH / 2f, 0.038f);
            st.GetComponent<Renderer>().material.color = Color.white;
            ReplaceColliderWithCapsule(st, 0.5f, 2f);

            var rb = st.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.isKinematic = true; // standing until hit
            pieces[i] = rb;
        }

        // Two bails resting across the grooves.
        for (int i = 0; i < 2; i++)
        {
            var bail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bail.name = "Bail" + (i + 1);
            bail.transform.SetParent(transform, false);
            bail.transform.localPosition = new Vector3(i == 0 ? -0.057f : 0.057f, stumpH + 0.012f, 0f);
            bail.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bail.transform.localScale = new Vector3(0.02f, 0.054f, 0.02f);
            bail.GetComponent<Renderer>().material.color = Color.white;
            ReplaceColliderWithCapsule(bail, 0.5f, 2f);

            var rb = bail.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.isKinematic = true;
            pieces[3 + i] = rb;
        }

        // Generous trigger volume = the stumps' physical presence.
        var zone = new GameObject("WicketZone");
        zone.transform.SetParent(transform, false);
        zone.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var trigger = zone.AddComponent<BoxCollider>();
        trigger.size = new Vector3(0.8f, 1.2f, 0.6f);
        trigger.isTrigger = true;
        zone.AddComponent<WicketZone>();
    }

    // Primitive colliders come in odd shapes; use a clean capsule in the
    // primitive's native local units (radius 0.5, height 2) so it scales
    // together with the visual.
    static void ReplaceColliderWithCapsule(GameObject go, float localRadius, float localHeight)
    {
        var old = go.GetComponent<Collider>();
        if (old != null) Destroy(old);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.radius = localRadius;
        cap.height = localHeight;
        cap.center = Vector3.zero;
    }

    /// <summary>
    /// Knock the stumps flying, like a real bowled dismissal.
    /// Bails are lighter, so they fly further — just like the real thing.
    /// </summary>
    public void KnockDown(Vector3 ballVelocity)
    {
        if (knocked) return;
        knocked = true;

        Vector3 dir = ballVelocity;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        foreach (var rb in pieces)
        {
            rb.isKinematic = false;
            bool isBail = rb.mass < 0.05f;
            rb.velocity = dir * (isBail ? 7f : 5f) + Vector3.up * (isBail ? 4f : 2f);
            rb.angularVelocity = new Vector3(
                Random.Range(-6f, 6f), Random.Range(-4f, 4f), Random.Range(-6f, 6f));
        }

        var zone = GetComponentInChildren<WicketZone>();
        if (zone != null) zone.GetComponent<Collider>().enabled = false;

        Debug.Log("StumpsPhysics: BOWLED! Stumps knocked down.");
    }
}

/// <summary>
/// Marks the generous "wicket zone" trigger around the stumps.
/// Any ball entering it is treated as hitting the stumps.
/// </summary>
public class WicketZone : MonoBehaviour { }
