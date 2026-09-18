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
        private Animator animator;
        private Camera mainCamera;
        private Plane groundPlane;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInput>();
            animator = GetComponentInChildren<Animator>();
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

            bool isStationary = moveDirection.sqrMagnitude < 0.001f;
            float turnAmount = 0f;

            if (input.IsAiming)
            {
                turnAmount = FaceMouse();
            }
            else if (!isStationary)
            {
                FaceDirection(moveDirection);
            }

            float moveAngle = (input.IsAiming && !isStationary)
                ? Vector3.SignedAngle(transform.forward, moveDirection, Vector3.up)
                : 0f;

            if (animator != null)
            {
                float animSpeed = isStationary ? 0f : (canSprint ? 2f : 1f);
                animator.SetFloat("Speed", animSpeed);
                animator.SetFloat("Turn", isStationary ? turnAmount : 0f);
                animator.SetFloat("MoveAngle", moveAngle);
            }
        }

        /// <summary>Rotates toward the mouse cursor. Returns the turn amount (-1 left .. 1 right) for animation.</summary>
        private float FaceMouse()
        {
            if (mainCamera == null) return 0f;

            Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);
            if (!groundPlane.Raycast(ray, out float distance)) return 0f;

            Vector3 aimPoint = ray.GetPoint(distance);
            Vector3 lookDirection = aimPoint - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.001f) return 0f;

            float signedAngle = Vector3.SignedAngle(transform.forward, lookDirection, Vector3.up);
            FaceDirection(lookDirection);
            return Mathf.Clamp(signedAngle / 30f, -1f, 1f);
        }

        private void FaceDirection(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }
}
