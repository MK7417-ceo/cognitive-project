using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VR HANDS + BAT (item 2, complete).
/// Problem before: XR Origin had a camera but NO controller objects, so
/// wearing a headset gave only a head view - no hands, no bat in hand.
/// BatVRGrab added an XRGrabInteractable, but with no interactor in the
/// scene it could never be grabbed. An XRI Action-based controller
/// cannot be created reliably from code alone (it needs an Input
/// Action Asset from the Editor), so this script does NOT pretend to
/// create one. Instead it uses the built-in XR InputDevices API, which
/// works with any OpenXR/SteamVR controller without an Action Asset:
///   - Left + Right hand visuals, tracked every frame from the real
///     controller position/rotation (in XR Origin space, converted to
///     world space through the XR Origin transform).
///   - Right hand = bat hand. The first time the right controller is
///     tracked, the bat's holder is switched from the hidden scene
///     batsman's hand (BatHolder) to the tracked right hand, via the
///     existing SnapSceneBatToHand (which already scales the bat to
///     0.965 m and points the blade down). BatSwingTracker then measures
///     the REAL controller swing, so ball physics is unchanged and
///     works exactly as the mouse swing did, only with a real hand.
///   - Left hand = free hand, tracked, no bat, so the player sees both
///     hands, not a floating bat.
///   - PC fallback (minute detail, do not break): if NO controller is
///     tracked, both hand visuals hide and the bat holder is restored
///     to the original BatHolder, so mouse/keyboard testing works
///     exactly as before. Hands are children of XR Origin, so they
///     follow the rig to the pavilion and back to the batsman spot.
///   - Hand visual (minute detail): palm + 4 fingers + thumb, in real
///     proportions - palm 0.09 m wide, fingers 0.015 m radius, 0.07 m
///     long, thumb at 45 degrees. No colliders on hands (they must
///     never block the ball), no shadows, skin-tone material.
///   - Bat is NEVER dropped in VR: once held, it stays in the right
///     hand even if the grip button is released, because a cricketer
///     does not drop his bat between balls.
/// </summary>
public static class VRHands
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("VRHands");
        go.AddComponent<VRHandWatcher>();
    }

    sealed class VRHandWatcher : MonoBehaviour
    {
        Transform xrOrigin;
        Transform leftHand;
        Transform rightHand;
        Transform originalBatHolder;
        SnapSceneBatToHand snap;
        bool batOnRightHand;
        float rightLostTimer;
        const float RestoreDelay = 2f;

        void Start()
        {
            xrOrigin = GameObject.Find("XR Origin (VR)") != null
                ? GameObject.Find("XR Origin (VR)").transform : null;
            if (xrOrigin == null) return;
            leftHand = BuildHand("VRLeftHand", xrOrigin);
            rightHand = BuildHand("VRRightHand", xrOrigin);
            snap = FindAnyObjectByType<SnapSceneBatToHand>();
            if (snap != null) originalBatHolder = snap.batHolder;
            SetHandVisible(leftHand, false);
            SetHandVisible(rightHand, false);
        }

        static Transform BuildHand(string name, Transform parent)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(parent, false);
            hand.transform.localPosition = Vector3.zero;
            hand.transform.localRotation = Quaternion.identity;
            var skin = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.65f, 0.5f) };
            // Palm: 0.09 m wide, 0.03 m thick, 0.13 m long.
            AddPart(hand.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.03f, 0.13f), skin);
            // 4 fingers: 0.015 m wide, 0.07 m long, across the front.
            for (int i = 0; i < 4; i++)
            {
                float x = -0.033f + i * 0.022f;
                AddPart(hand.transform, PrimitiveType.Capsule, new Vector3(x, 0f, 0.095f), new Vector3(0.015f, 0.035f, 0.015f), skin);
            }
            // Thumb: on the side, 45 degrees.
            var thumb = AddPart(hand.transform, PrimitiveType.Capsule, new Vector3(0.055f, 0f, 0.03f), new Vector3(0.016f, 0.03f, 0.016f), skin);
            thumb.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            return hand.transform;
        }

        static Transform AddPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "HandPart";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) { rend.material = mat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return go.transform;
        }

        static void SetHandVisible(Transform hand, bool visible)
        {
            if (hand == null) return;
            foreach (var r in hand.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
        }

        void Update()
        {
            if (xrOrigin == null) return;
            bool leftOk = TrackHand(InputDeviceCharacteristics.Left, leftHand);
            bool rightOk = TrackHand(InputDeviceCharacteristics.Right, rightHand);
            SetHandVisible(leftHand, leftOk);
            SetHandVisible(rightHand, rightOk);

            if (rightOk)
            {
                rightLostTimer = 0f;
                if (!batOnRightHand && snap != null && rightHand != null)
                {
                    snap.batHolder = rightHand;
                    snap.FixBatAndAttachToHand();
                    batOnRightHand = true;
                    Debug.Log("VRHands: bat now in tracked right hand.");
                }
            }
            else if (batOnRightHand)
            {
                // Only restore after 2 s of loss, so a tracking blink
                // does not yank the bat back to the hidden model mid-swing.
                rightLostTimer += Time.deltaTime;
                if (rightLostTimer >= RestoreDelay && snap != null && originalBatHolder != null)
                {
                    snap.batHolder = originalBatHolder;
                    snap.FixBatAndAttachToHand();
                    batOnRightHand = false;
                    Debug.Log("VRHands: right controller lost, bat restored to scene hand (PC fallback).");
                }
            }
        }

        bool TrackHand(InputDeviceCharacteristics side, Transform hand)
        {
            if (hand == null) return false;
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side, devices);
            if (devices.Count == 0) return false;
            var device = devices[0];
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPos)) return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRot)) return false;
            // Device pose is in XR Origin (tracking) space.
            hand.position = xrOrigin.TransformPoint(localPos);
            hand.rotation = xrOrigin.rotation * localRot;
            return true;
        }
    }
}
