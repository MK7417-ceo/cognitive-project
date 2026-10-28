using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BatMouseClickSwing : MonoBehaviour
{
    public float directionSensitivity = 4f;
    public float swingSpeed = 10f;
    public float returnSpeed = 6f;

    public float maxXAngle = 60f;   // up-down
    public float maxYAngle = 50f;   // left-right

    private float targetX;
    private float targetY;

    private bool isSwinging = false;

    void Update()
    {
        float mouseX = 0f;
        float mouseY = 0f;
        bool clickTriggered = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            mouseX = delta.x * 0.1f;
            mouseY = delta.y * 0.1f;
            clickTriggered = Mouse.current.leftButton.wasPressedThisFrame;
        }
        else
#endif
        {
            try
            {
                mouseX = Input.GetAxisRaw("Mouse X");
                mouseY = Input.GetAxisRaw("Mouse Y");
                clickTriggered = Input.GetMouseButtonDown(0);
            }
            catch
            {
                // Ignored if legacy input is disabled
            }
        }

        targetX -= mouseY * directionSensitivity;
        targetY += mouseX * directionSensitivity;

        targetX = Mathf.Clamp(targetX, -maxXAngle, maxXAngle);
        targetY = Mathf.Clamp(targetY, -maxYAngle, maxYAngle);

        if (clickTriggered)
        {
            isSwinging = true;
        }

        // Swing execution
        if (isSwinging)
        {
            Quaternion targetRot = Quaternion.Euler(targetX, targetY, 0);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRot,
                Time.deltaTime * swingSpeed
            );

            if (Quaternion.Angle(transform.localRotation, targetRot) < 2f)
            {
                isSwinging = false;
            }
        }
        else
        {
            // Return to stance
            Quaternion defaultRot = Quaternion.Euler(0, 0, 0);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                defaultRot,
                Time.deltaTime * returnSpeed
            );
        }
    }
}
