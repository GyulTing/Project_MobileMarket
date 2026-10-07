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
        public bool IsAiming { get; private set; }
        public bool IsInteractPressed { get; private set; }
        public bool IsFirePressed { get; private set; }
        public bool IsFireJustPressed { get; private set; }
        /// <summary>Weapon key (R): tap = reload, hold = draw/holster. PlayerCombat tells the two apart.</summary>
        public bool IsWeaponKeyHeld { get; private set; }
        public bool IsWeaponKeyReleased { get; private set; }

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
            IsAiming = mouse.rightButton.isPressed;
            IsInteractPressed = keyboard.fKey.wasPressedThisFrame;
            IsFirePressed = mouse.leftButton.isPressed;
            IsFireJustPressed = mouse.leftButton.wasPressedThisFrame;
            IsWeaponKeyHeld = keyboard.rKey.isPressed;
            IsWeaponKeyReleased = keyboard.rKey.wasReleasedThisFrame;
        }
    }
}
