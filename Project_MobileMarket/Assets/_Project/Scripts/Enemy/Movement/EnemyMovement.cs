using UnityEngine;
using UnityEngine.AI;

namespace MobileMarket.Enemy
{
    /// <summary>Walks the enemy around on the NavMesh and feeds its speed to the Animator's locomotion blend.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyMovement : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float turnSpeed = 8f;
        [Tooltip("Speed multiplier while a hit reaction plays (1 = no slowdown).")]
        [Range(0f, 1f)]
        [SerializeField] private float hitSlowMultiplier = 1f;

        private NavMeshAgent agent;
        private Animator animator;
        private EnemyHealth health;
        private bool slowed;
        private float slowEndTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            health = GetComponent<EnemyHealth>();
            agent.speed = moveSpeed;
        }

        private void OnEnable()
        {
            // Reused from the pool: snap onto the NavMesh where we were placed.
            agent.enabled = true;
            agent.speed = moveSpeed;
            slowed = false;
            if (agent.isOnNavMesh) agent.Warp(transform.position);
            if (health != null) health.OnStaggered += HandleStaggered;
        }

        private void OnDisable()
        {
            if (health != null) health.OnStaggered -= HandleStaggered;
        }

        private void Update()
        {
            if (slowed && Time.time >= slowEndTime)
            {
                slowed = false;
                agent.speed = moveSpeed;
            }
            if (animator != null) animator.SetFloat(SpeedParam, agent.enabled ? agent.velocity.magnitude : 0f);
        }

        /// <summary>Slows the enemy down for as long as its hit reaction holds off attacks.</summary>
        private void HandleStaggered(float duration)
        {
            if (hitSlowMultiplier >= 1f) return;
            slowed = true;
            slowEndTime = Time.time + duration;
            agent.speed = moveSpeed * hitSlowMultiplier;
        }

        public void MoveTo(Vector3 destination)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        /// <summary>Turns in place toward a point (used while attacking).</summary>
        public void FaceTowards(Vector3 point)
        {
            agent.updateRotation = false;
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        /// <summary>Takes the body off the NavMesh (dead) so it no longer pushes other agents around.</summary>
        public void Disable()
        {
            Stop();
            agent.enabled = false;
        }
    }
}
