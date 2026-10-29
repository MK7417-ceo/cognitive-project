using System.Collections;
using UnityEngine;

/// <summary>
/// Adds hit detection to the scene's main yellow V-shaped wicket. The scene
/// supplies the visible wicket; this component adds only the wicket trigger.
/// </summary>
public class StumpsPhysics : MonoBehaviour
{
    bool knocked;

    /// <summary>Add hit detection at the scene's main wicket target.</summary>
    public static StumpsPhysics Build(Vector3 wicketPosition, Vector3 bowlerPos)
    {
        var existing = FindAnyObjectByType<StumpsPhysics>();
        if (existing != null) return existing;

        Vector3 away = wicketPosition - bowlerPos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.back;
        away.Normalize();

        var root = new GameObject("MainWicketHitZone");
        root.transform.position = wicketPosition;
        root.transform.rotation = Quaternion.LookRotation(away, Vector3.up);

        var stumps = root.AddComponent<StumpsPhysics>();
        stumps.Construct();
        Debug.Log($"StumpsPhysics: main wicket hit zone attached at {wicketPosition}");
        return stumps;
    }

    void Construct()
    {
        // The yellow V-shaped scene mesh is the visible wicket. This trigger
        // makes that same location the wicket used by the ball and keeper logic.
        var zone = new GameObject("WicketZone");
        zone.transform.SetParent(transform, false);
        zone.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var trigger = zone.AddComponent<BoxCollider>();
        trigger.size = new Vector3(0.8f, 1.2f, 0.6f);
        trigger.isTrigger = true;
        zone.AddComponent<WicketZone>();
    }

    /// <summary>Disable the wicket trigger after a bowled dismissal.</summary>
    public void KnockDown(Vector3 ballVelocity)
    {
        if (knocked) return;
        knocked = true;

        var zone = GetComponentInChildren<WicketZone>();
        if (zone != null)
        {
            var zoneCollider = zone.GetComponent<Collider>();
            if (zoneCollider != null) zoneCollider.enabled = false;
        }

        Debug.Log("StumpsPhysics: BOWLED! Main wicket hit.");
        StartCoroutine(RestoreWicket());
    }

    IEnumerator RestoreWicket()
    {
        // The yellow wicket mesh is static in this scene. Re-arm its hit zone
        // after the dismissal so later deliveries can still take wickets.
        yield return new WaitForSeconds(2f);
        knocked = false;
        var zone = GetComponentInChildren<WicketZone>();
        if (zone != null)
        {
            var zoneCollider = zone.GetComponent<Collider>();
            if (zoneCollider != null) zoneCollider.enabled = true;
        }
    }
}

/// <summary>Marks the trigger volume around the main wicket.</summary>
public class WicketZone : MonoBehaviour { }
