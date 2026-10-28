using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BatMouseSwingPro : MonoBehaviour
{
    public float swingSpeed = 6f;

    public float maxXAngle = 60f;   // up-down swing
    public float maxYAngle = 50f;   // left-right direction

    private float rotX;
    private float rotY;

    void Update()
    {
        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        // Unity New Input System check
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            mouseX = delta.x * 0.1f;
            mouseY = delta.y * 0.1f;
        }
        else
#endif
        {
            // Legacy Input Manager fallback
            try
            {
                mouseX = Input.GetAxisRaw("Mouse X");
                mouseY = Input.GetAxisRaw("Mouse Y");
            }
            catch
            {
                // Ignored if legacy input is completely disabled in player settings
            }
        }

        // Calculate rotation angles
        rotX -= mouseY * swingSpeed;
        rotY += mouseX * swingSpeed;

        // Clamp rotation angles
        rotX = Mathf.Clamp(rotX, -maxXAngle, maxXAngle);
        rotY = Mathf.Clamp(rotY, -maxYAngle, maxYAngle);

        // Apply local rotation
        transform.localRotation = Quaternion.Euler(rotX, rotY, 0f);
    }
}
