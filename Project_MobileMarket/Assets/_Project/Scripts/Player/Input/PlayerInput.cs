using UnityEngine;
using UnityEngine.InputSystem;

namespace MobileMarket.Player
{
    /// <summary>
    /// Reads raw WASD + mouse input. No movement logic here (see PlayerMovement).
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 MouseScreenPosition { get; private set; }
        public bool IsSprinting { get; private set; }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            MoveInput = new Vector2(x, y);

            MouseScreenPosition = mouse.position.ReadValue();
            IsSprinting = keyboard.shiftKey.isPressed;
        }
    }
}
