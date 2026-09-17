using UnityEngine;

namespace MobileMarket.Player
{
    /// <summary>
    /// Moves the player on the XZ plane and faces the mouse cursor (quarter-view).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float aimMultiplier = 0.7f;
        [SerializeField] private float turnSpeed = 15f;

        private CharacterController controller;
        private PlayerInput input;
        private Camera mainCamera;
        private Plane groundPlane;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInput>();
            mainCamera = Camera.main;
            groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Update()
        {
            Vector2 move = input.MoveInput;
            Vector3 moveDirection = new Vector3(move.x, 0f, move.y).normalized;

            bool canSprint = input.IsSprinting && !input.IsAiming;
            float multiplier = canSprint ? sprintMultiplier : (input.IsAiming ? aimMultiplier : 1f);
            float speed = moveSpeed * multiplier;
            controller.Move(moveDirection * speed * Time.deltaTime);

            if (input.IsAiming)
            {
                FaceMouse();
            }
            else if (moveDirection.sqrMagnitude > 0.001f)
            {
                FaceDirection(moveDirection);
            }
        }

        private void FaceMouse()
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);
            if (!groundPlane.Raycast(ray, out float distance)) return;

            Vector3 aimPoint = ray.GetPoint(distance);
            Vector3 lookDirection = aimPoint - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.001f) return;

            FaceDirection(lookDirection);
        }

        private void FaceDirection(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }
}
