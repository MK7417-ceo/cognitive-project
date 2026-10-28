using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraSwitcher : MonoBehaviour
{
    public Camera[] stadiumCameras;
    private int currentCamIndex = 0;

    void Start()
    {
        if (stadiumCameras == null || stadiumCameras.Length == 0)
        {
            Debug.LogError("CameraSwitcher: no cameras assigned. Assign stadiumCameras in the Inspector.");
            enabled = false;
            return;
        }
        ActivateCamera(0);
    }

    void Update()
    {
        if (stadiumCameras == null || stadiumCameras.Length == 0) return;

#if ENABLE_INPUT_SYSTEM
        // 1. New Input System keyboard check
        if (Keyboard.current != null)
        {
            // 'C' key switches to next camera
            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                currentCamIndex = (currentCamIndex + 1) % stadiumCameras.Length;
                ActivateCamera(currentCamIndex);
                return;
            }

            // Direct digit keys 1-9
            if (Keyboard.current.digit1Key.wasPressedThisFrame) { ActivateCamera(0); return; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame) { ActivateCamera(1); return; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame) { ActivateCamera(2); return; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame) { ActivateCamera(3); return; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame) { ActivateCamera(4); return; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame) { ActivateCamera(5); return; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame) { ActivateCamera(6); return; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame) { ActivateCamera(7); return; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame) { ActivateCamera(8); return; }
            return;
        }
#endif

        // 2. Legacy Input Manager fallback
        try
        {
            int keyCount = Mathf.Min(stadiumCameras.Length, 9);
            for (int i = 0; i < keyCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    ActivateCamera(i);
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                currentCamIndex = (currentCamIndex + 1) % stadiumCameras.Length;
                ActivateCamera(currentCamIndex);
            }
        }
        catch
        {
            // Ignored if legacy input is disabled
        }
    }

    public void ActivateCamera(int index)
    {
        if (stadiumCameras == null || stadiumCameras.Length == 0) return;
        index = Mathf.Clamp(index, 0, stadiumCameras.Length - 1);
        for (int i = 0; i < stadiumCameras.Length; i++)
        {
            if (stadiumCameras[i] != null)
                stadiumCameras[i].gameObject.SetActive(i == index);
        }
        currentCamIndex = index;
    }
}
