using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VR batsman rule: the VR player IS the batsman. There is no second
/// playable batsman model standing at the crease.
/// When a VR headset is active, hide only the scene batsman model's
/// renderers (its transform, bat logic and position stay, so runs and
/// scoring still work). The non-striker clone is NOT hidden: its
/// PlayerMovement is disabled, so it is never picked up here.
/// When no headset is present (PC test), the model stays visible.
/// NOTE: a headset alone only gives a head camera. Hands + bat in VR
/// come only from XR Controller objects in the scene - wearing the
/// headset does NOT automatically create hands or a body.
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
            bool vrActive = IsVRActive();
            // Only the enabled PlayerMovement is the real batsman.
            // The non-striker clone has its PlayerMovement disabled,
            // so FindObjectsInactive.Exclude never returns it.
            foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            {
                if (pm == null) continue;
                foreach (var rend in pm.GetComponentsInChildren<Renderer>(true))
                    rend.enabled = !vrActive;
            }
        }

        static bool IsVRActive()
        {
            var cam = Camera.main;
            if (cam != null && cam.stereoEnabled) return true;
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, devices);
            return devices.Count > 0;
        }
    }
}
