using UnityEngine;
using MobileMarket.Enemy;

namespace MobileMarket.Player
{
    /// <summary>
    /// Moves the player on the XZ plane with gravity and faces the mouse cursor (quarter-view).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float aimMultiplier = 0.7f;
        [SerializeField] private float turnSpeed = 15f;

        [Tooltip("Downward acceleration (CharacterController has no gravity of its own).")]
        [SerializeField] private float gravity = -20f;
        [Tooltip("Small downward speed kept while grounded so the player stays stuck to slopes and steps down off ledges.")]
        [SerializeField] private float groundedStickSpeed = -2f;

        [Tooltip("Within this distance of the cursor the barrel correction is skipped (the gun's side offset would make it spin).")]
        [SerializeField] private float minAimCorrectionDistance = 1.2f;
        [SerializeField] private float barrelOffsetSmoothing = 3f;
        [SerializeField] private float maxBarrelYaw = 20f;
        [SerializeField] private float maxBarrelSideOffset = 0.5f;

        [Tooltip("Muzzle height range above the player root while aiming (root sits at the capsule center).")]
        [SerializeField] private float minBarrelHeight = 0.3f;
        [SerializeField] private float maxBarrelHeight = 0.9f;

        [Tooltip("How far the cursor ray looks for an enemy to lock the aim onto.")]
        [SerializeField] private float cursorTargetDistance = 100f;

        private float verticalSpeed;
        private float barrelYaw;
        private float barrelSideOffset;
        private float barrelHeight = 0.65f;

        /// <summary>Point under the cursor at weapon height while aiming; bullets fly toward it.</summary>
        public Vector3 AimPoint { get; private set; }
        public bool HasAimPoint { get; private set; }

        private CharacterController controller;
        private PlayerInput input;
        private PlayerCombat combat;
        private PlayerHealth health;
        private Animator animator;
        private Camera mainCamera;
        private Plane groundPlane;
        private FactionMember faction;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInput>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<PlayerHealth>();
            faction = GetComponent<FactionMember>();
            animator = GetComponentInChildren<Animator>();
            mainCamera = Camera.main;
            groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void OnEnable()
        {
            if (health != null) health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDied -= HandleDied;
        }

        private void HandleDied() => enabled = false;

        private void Update()
        {
            Vector2 move = input.MoveInput;
            Vector3 moveDirection = new Vector3(move.x, 0f, move.y).normalized;

            bool canSprint = input.IsSprinting && !input.IsAiming;
            float multiplier = canSprint ? sprintMultiplier : (input.IsAiming ? aimMultiplier : 1f);
            float speed = moveSpeed * multiplier;

            // Fall when nothing is underfoot; otherwise keep a light push down so the player follows the ground.
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = groundedStickSpeed;
            else verticalSpeed += gravity * Time.deltaTime;

            Vector3 velocity = moveDirection * speed;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);

            bool isStationary = moveDirection.sqrMagnitude < 0.001f;
            float turnAmount = 0f;

            HasAimPoint = false;
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
                animator.SetBool("IsArmed", combat != null && combat.IsArmed);
                animator.SetBool("IsAiming", input.IsAiming);

                bool isArmedWithWeapon = combat != null && combat.IsArmed && combat.EquippedWeapon != null;
                int weaponType = isArmedWithWeapon ? (int)combat.EquippedWeapon.weaponType : 0;
                animator.SetInteger("WeaponType", weaponType);
            }
        }

        /// <summary>
        /// Rotates toward the mouse cursor. With a weapon drawn, the cursor is projected at the muzzle's height and the body
        /// turns so the barrel line (held off to the side and slightly angled by the aim pose) passes through it.
        /// Returns the turn amount (-1 left .. 1 right) for animation.
        /// </summary>
        private float FaceMouse()
        {
            if (mainCamera == null) return 0f;

            var muzzle = combat != null ? combat.Muzzle : null;
            if (muzzle != null) UpdateBarrelOffset(muzzle);
            // Cursor projected at the (smoothed) muzzle height so it lines up with the barrel as seen by the angled camera.
            groundPlane.SetNormalAndPosition(Vector3.up, new Vector3(0f, muzzle != null ? transform.position.y + barrelHeight : 0f, 0f));

            Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);
            if (!groundPlane.Raycast(ray, out float distance)) return 0f;

            Vector3 aimPoint = ray.GetPoint(distance);
            // Cursor on an enemy: aim at the spot of its body under the cursor (not its center — no aim assist), since the
            // angled camera would otherwise only line the barrel up with its upper body.
            if (TryGetCursorTargetPoint(ray, out Vector3 targetPoint))
                aimPoint = new Vector3(targetPoint.x, aimPoint.y, targetPoint.z);
            AimPoint = aimPoint;
            HasAimPoint = true;

            Vector3 lookDirection = aimPoint - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.001f) return 0f;

            if (muzzle != null)
            {
                float distanceToAim = lookDirection.magnitude;
                if (distanceToAim > minAimCorrectionDistance)
                {
                    // The barrel runs barrelSideOffset to the right of the body center, turned barrelYaw degrees.
                    // Turn the body so that line passes through the cursor: yaw = bearing - asin(offset / distance) - barrelYaw.
                    float offsetAngle = Mathf.Asin(Mathf.Clamp(barrelSideOffset / distanceToAim, -1f, 1f)) * Mathf.Rad2Deg;
                    lookDirection = Quaternion.AngleAxis(-offsetAngle - barrelYaw, Vector3.up) * lookDirection;
                }
            }

            float signedAngle = Vector3.SignedAngle(transform.forward, lookDirection, Vector3.up);
            float aimSpeed = combat != null && combat.EquippedWeapon != null ? combat.EquippedWeapon.aimSpeed : turnSpeed;
            FaceDirection(lookDirection, aimSpeed);
            return Mathf.Clamp(signedAngle / 30f, -1f, 1f);
        }

        /// <summary>Where the cursor touches the first thing under it, if that is a living fighter hostile to the player.</summary>
        private bool TryGetCursorTargetPoint(Ray ray, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(ray, out RaycastHit hit, cursorTargetDistance, ~0, QueryTriggerInteraction.Ignore)) return false;

            var member = hit.collider.GetComponent<FactionMember>();
            if (member == null || member == faction || !member.IsAlive) return false;
            if (faction != null && !faction.IsHostileTo(member)) return false;

            point = hit.point;
            return true;
        }

        /// <summary>
        /// Tracks, in body space, how far the barrel line sits to the side of the body center and how much it is turned.
        /// Smoothed and clamped so turn animations swinging the gun can't feed back into the aim rotation.
        /// </summary>
        private void UpdateBarrelOffset(Transform muzzle)
        {
            Vector3 localPos = transform.InverseTransformPoint(muzzle.position);
            Vector3 localDir = transform.InverseTransformDirection(muzzle.forward);
            localDir.y = 0f;
            if (localDir.sqrMagnitude < 0.0001f) return;

            float yaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -maxBarrelYaw, maxBarrelYaw);
            float yawRad = yaw * Mathf.Deg2Rad;
            // Perpendicular distance from the body center to the barrel line (positive = right of center).
            float side = Mathf.Clamp(localPos.x * Mathf.Cos(yawRad) - localPos.z * Mathf.Sin(yawRad), -maxBarrelSideOffset, maxBarrelSideOffset);

            float t = 1f - Mathf.Exp(-barrelOffsetSmoothing * Time.deltaTime);
            barrelYaw = Mathf.Lerp(barrelYaw, yaw, t);
            barrelSideOffset = Mathf.Lerp(barrelSideOffset, side, t);
            barrelHeight = Mathf.Lerp(barrelHeight, Mathf.Clamp(localPos.y, minBarrelHeight, maxBarrelHeight), t);
        }

        private void FaceDirection(Vector3 direction)
        {
            FaceDirection(direction, turnSpeed);
        }

        private void FaceDirection(Vector3 direction, float speed)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, speed * Time.deltaTime);
        }
    }
}
