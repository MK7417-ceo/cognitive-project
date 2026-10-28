using UnityEngine;

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
        // Mouse movement → direction decide kare
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        targetX -= mouseY * directionSensitivity;
        targetY += mouseX * directionSensitivity;

        targetX = Mathf.Clamp(targetX, -maxXAngle, maxXAngle);
        targetY = Mathf.Clamp(targetY, -maxYAngle, maxYAngle);

        // Mouse click → swing start
        if (Input.GetMouseButtonDown(0))
        {
            isSwinging = true;
        }

        // Swing logic
        if (isSwinging)
        {
            Quaternion targetRot = Quaternion.Euler(targetX, targetY, 0);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRot,
                Time.deltaTime * swingSpeed
            );

            // Jab almost pahunch jaye → return
            if (Quaternion.Angle(transform.localRotation, targetRot) < 2f)
            {
                isSwinging = false;
            }
        }
        else
        {
            // Bat idle / return to normal
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                Quaternion.identity,
                Time.deltaTime * returnSpeed
            );
        }
    }
}
