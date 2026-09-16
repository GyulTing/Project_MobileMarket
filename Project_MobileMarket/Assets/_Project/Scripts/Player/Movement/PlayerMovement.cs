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
            Move();
            FaceMouse();
        }

        private void Move()
        {
            Vector2 move = input.MoveInput;
            Vector3 direction = new Vector3(move.x, 0f, move.y).normalized;
            float speed = moveSpeed * (input.IsSprinting ? sprintMultiplier : 1f);
            controller.Move(direction * speed * Time.deltaTime);
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

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }
}
