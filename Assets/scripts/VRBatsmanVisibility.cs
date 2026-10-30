using UnityEngine;

/// <summary>
/// VR batsman rule: the VR player IS the batsman. There is no second
/// playable batsman model standing at the crease.
/// USER RULE: only bowler, fielders and keeper stay on the field,
/// plus the VR player as batsman. The scene batsman model has no work:
/// hide its renderers ALWAYS (PC test and VR both), not only in VR.
/// Its transform, bat logic and position stay, so runs and scoring
/// still work. The non-striker clone is NOT hidden here: its
/// PlayerMovement is disabled, so it is never picked up here.
/// NOTE: a headset alone only gives a head camera. Hands + bat in VR
/// come only from XR Controller objects in the scene.
/// </summary>
public static class VRBatsmanVisibility
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("VRBatsmanVisibility");
        go.AddComponent<VRBatsmanWatcher>();
    }

    sealed class VRBatsmanWatcher : MonoBehaviour
    {
        float timer;
        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            // Always hide: the VR player is the batsman, the scene
            // model is redundant. Only the enabled PlayerMovement is
            // the scene batsman; the non-striker clone has its
            // PlayerMovement disabled, so it is never returned here.
            foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            {
                if (pm == null) continue;
                foreach (var rend in pm.GetComponentsInChildren<Renderer>(true))
                    rend.enabled = false;
            }
        }

    }
}
