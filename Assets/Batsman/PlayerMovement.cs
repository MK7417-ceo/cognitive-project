using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    void Update()
    {
        float move = 0f;

        // 1. Safe New Input System Keyboard check (prevents null reference on standalone VR / mobile)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                move = -1f;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                move = 1f;
        }

        // 2. Safe Gamepad / VR controller stick fallback
        if (move == 0f && Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.2f)
                move = -Mathf.Sign(stickX);
        }

        // 3. Move along local front/back (pitch breadth)
        if (move != 0f)
        {
            transform.Translate(-transform.forward * move * moveSpeed * Time.deltaTime, Space.World);
        }
    }
}
