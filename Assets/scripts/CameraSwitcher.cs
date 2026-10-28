using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls 9 distinct cricket broadcast and training camera angles:
///   1. Main Batsman POV (Primary Training Camera)
///   2. Ball Tracker (Dedicated camera that continuously follows the ball in real time)
///   3. Bowler POV (Behind the bowler looking down the pitch)
///   4. Wicket-Keeper / Stump Cam (Directly behind the stumps)
///   5. Side-On Pitch Cam (Square of the wicket, side profile of delivery and bounce)
///   6. Slip Cordon Cam (Behind off-side slips capturing edges)
///   7. Umpire POV (Standing behind non-striker crease)
///   8. High Stadium Spider Cam (Elevated tactical bird's eye view)
///   9. Mid-Off 45° Cam (Broadcast drive and front-foot angle)
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    public static CameraSwitcher Instance { get; private set; }

    [Header("9 Camera Rig References")]
    public Camera[] stadiumCameras = new Camera[9];

    [Header("Current Status")]
    public int currentCamIndex = 0;

    private readonly string[] cameraNames = new string[]
    {
        "1. Main Batsman POV",
        "2. Ball Tracker (Live)",
        "3. Bowler POV",
        "4. Wicket-Keeper Cam",
        "5. Side-On Pitch View",
        "6. Slip Cordon Cam",
        "7. Umpire POV",
        "8. High Spider Cam",
        "9. Mid-Off 45° Angle"
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        SetupNineCameras();
        ActivateCamera(0);
    }

    void Update()
    {
        HandleKeyboardInput();
    }

    void HandleKeyboardInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                CycleCamera();
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame) { ActivateCamera(0); return; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame) { ActivateCamera(1); return; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame) { ActivateCamera(2); return; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame) { ActivateCamera(3); return; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame) { ActivateCamera(4); return; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame) { ActivateCamera(5); return; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame) { ActivateCamera(6); return; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame) { ActivateCamera(7); return; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame) { ActivateCamera(8); return; }
        }
#endif

        try
        {
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    ActivateCamera(i);
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                CycleCamera();
            }
        }
        catch
        {
            // Ignored if legacy input is inactive
        }
    }

    public void CycleCamera()
    {
        currentCamIndex = (currentCamIndex + 1) % 9;
        ActivateCamera(currentCamIndex);
    }

    public void ActivateCamera(int index)
    {
        if (stadiumCameras == null || stadiumCameras.Length < 9) return;

        index = Mathf.Clamp(index, 0, 8);
        currentCamIndex = index;

        for (int i = 0; i < stadiumCameras.Length; i++)
        {
            if (stadiumCameras[i] != null)
            {
                bool isActive = (i == index);
                stadiumCameras[i].gameObject.SetActive(isActive);

                // Ensure only active camera processes audio listener
                var listener = stadiumCameras[i].GetComponent<AudioListener>();
                if (listener != null) listener.enabled = isActive;
            }
        }

        // Ensure strictly only 1 AudioListener is enabled across the whole scene
        var allListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        foreach (var al in allListeners)
        {
            if (al != null)
            {
                al.enabled = (stadiumCameras[index] != null && al.gameObject == stadiumCameras[index].gameObject);
            }
        }

        // Announce camera switch in HUD
        string camLabel = cameraNames[index];
        Debug.Log($"📷 Switched to Camera: {camLabel}");

        if (CricketMatchManager.Instance != null)
        {
            CricketMatchManager.Instance.ShowMessage($"CAM {camLabel}", Color.yellow, 1.8f);
        }
    }

    /// <summary>
    /// Configures or auto-generates the 9 standard cricket camera angles.
    /// </summary>
    void SetupNineCameras()
    {
        // 1. Gather scene coordinates
        var bowler = FindAnyObjectByType<BowlingManager>(FindObjectsInactive.Include);
        var batsman = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);

        Vector3 bowlerPos = bowler != null ? bowler.transform.position : new Vector3(2.23f, 0f, -6.93f);
        Vector3 batsmanPos = batsman != null ? batsman.transform.position : new Vector3(-13.3f, 0f, 2.1f);
        Vector3 pitchCentre = (bowlerPos + batsmanPos) * 0.5f;

        Vector3 pitchDir = (batsmanPos - bowlerPos);
        pitchDir.y = 0f;
        if (pitchDir.sqrMagnitude < 0.01f) pitchDir = Vector3.forward;
        pitchDir.Normalize();

        Vector3 lateralDir = Vector3.Cross(Vector3.up, pitchDir).normalized;

        if (stadiumCameras == null || stadiumCameras.Length != 9)
        {
            stadiumCameras = new Camera[9];
        }

        // Assign or Build Camera 0: Main Batsman POV
        if (stadiumCameras[0] == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null) stadiumCameras[0] = mainCam;
            else stadiumCameras[0] = CreateCameraObject("Cam0_MainBatsman", batsmanPos - pitchDir * 1.5f + Vector3.up * 1.6f, pitchDir);
        }

        // Assign or Build Camera 1: Ball Tracker (Dedicated live tracker)
        if (stadiumCameras[1] == null)
        {
            Vector3 trackerPos = pitchCentre + lateralDir * 6.0f + Vector3.up * 3.0f;
            stadiumCameras[1] = CreateCameraObject("Cam1_BallTracker", trackerPos, pitchCentre - trackerPos);
        }
        if (stadiumCameras[1].GetComponent<BallTrackingCamera>() == null)
        {
            var tracker = stadiumCameras[1].gameObject.AddComponent<BallTrackingCamera>();
            tracker.CalibrateIdlePosition(pitchCentre, pitchDir);
        }

        // Assign or Build Camera 2: Bowler End POV
        if (stadiumCameras[2] == null)
        {
            Vector3 pos = bowlerPos - pitchDir * 3.5f + Vector3.up * 2.2f;
            stadiumCameras[2] = CreateCameraObject("Cam2_BowlerPOV", pos, pitchDir);
        }

        // Assign or Build Camera 3: Wicket-Keeper / Stump Cam
        if (stadiumCameras[3] == null)
        {
            Vector3 pos = batsmanPos + pitchDir * 1.8f + Vector3.up * 0.9f;
            stadiumCameras[3] = CreateCameraObject("Cam3_WicketKeeperStumps", pos, -pitchDir);
        }

        // Assign or Build Camera 4: Side-On Pitch View
        if (stadiumCameras[4] == null)
        {
            Vector3 pos = pitchCentre + lateralDir * 14.0f + Vector3.up * 2.5f;
            stadiumCameras[4] = CreateCameraObject("Cam4_SideOnPitch", pos, pitchCentre - pos);
        }

        // Assign or Build Camera 5: Slip Cordon Cam
        if (stadiumCameras[5] == null)
        {
            Vector3 pos = batsmanPos + pitchDir * 3.0f + lateralDir * 3.5f + Vector3.up * 1.5f;
            stadiumCameras[5] = CreateCameraObject("Cam5_SlipCordon", pos, batsmanPos - pos);
        }

        // Assign or Build Camera 6: Umpire POV
        if (stadiumCameras[6] == null)
        {
            Vector3 pos = bowlerPos - pitchDir * 2.0f + Vector3.up * 1.8f;
            stadiumCameras[6] = CreateCameraObject("Cam6_UmpirePOV", pos, pitchDir);
        }

        // Assign or Build Camera 7: High Stadium Spider Cam
        if (stadiumCameras[7] == null)
        {
            Vector3 pos = pitchCentre + Vector3.up * 18.0f - pitchDir * 8.0f;
            stadiumCameras[7] = CreateCameraObject("Cam7_SpiderCam", pos, pitchCentre - pos);
        }

        // Assign or Build Camera 8: Mid-Off 45° Angle
        if (stadiumCameras[8] == null)
        {
            Vector3 pos = pitchCentre - pitchDir * 6.0f - lateralDir * 8.0f + Vector3.up * 2.8f;
            stadiumCameras[8] = CreateCameraObject("Cam8_MidOff45", pos, batsmanPos - pos);
        }
    }

    Camera CreateCameraObject(string name, Vector3 position, Vector3 forwardLook)
    {
        var existing = GameObject.Find(name);
        if (existing != null && existing.GetComponent<Camera>() != null)
        {
            return existing.GetComponent<Camera>();
        }

        var go = new GameObject(name);
        go.transform.position = position;
        if (forwardLook.sqrMagnitude > 0.001f)
        {
            go.transform.rotation = Quaternion.LookRotation(forwardLook.normalized, Vector3.up);
        }

        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        cam.fieldOfView = 60f;

        return cam;
    }

    public string GetCurrentCameraName()
    {
        if (currentCamIndex >= 0 && currentCamIndex < cameraNames.Length)
            return cameraNames[currentCamIndex];
        return "Camera";
    }
}
