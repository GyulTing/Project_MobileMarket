using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Enemy decision making: Idle until a hostile fighter (other faction or the player) is seen within sightRange,
    /// then Chase it and Attack once in range with a clear line of sight; back to Idle when the target dies or is lost.
    /// Given a rally point (SetRallyPoint), it walks there instead of standing idle until it arrives or spots a target.
    /// Targets are re-evaluated a few times a second. A Patrol state (walking the map's routes) slots in later in place of Idle.
    /// </summary>
    [RequireComponent(typeof(FactionMember), typeof(EnemyMovement), typeof(EnemyHealth))]
    public class EnemyBrain : MonoBehaviour
    {
        public enum State { Idle, Rally, Chase, Attack, Dead }

        private static readonly RaycastHit[] SightHits = new RaycastHit[8];
        private const float RepathDistance = 0.5f;

        [SerializeField] private float sightRange = 12f;
        [Tooltip("A target already being chased is only dropped beyond this distance.")]
        [SerializeField] private float loseSightRange = 22f;
        [SerializeField] private float retargetInterval = 0.3f;
        [Tooltip("Within this distance of the rally point it counts as arrived and goes idle.")]
        [SerializeField] private float rallyArriveDistance = 1.5f;

        public State CurrentState { get; private set; }
        public FactionMember Target { get; private set; }

        private FactionMember self;
        private EnemyMovement movement;
        private EnemyHealth health;
        private EnemyAttack attack;
        private float nextRetargetTime;
        private Vector3 chaseDestination;
        private bool hasRallyPoint;
        private Vector3 rallyPoint;

        private void Awake()
        {
            self = GetComponent<FactionMember>();
            movement = GetComponent<EnemyMovement>();
            health = GetComponent<EnemyHealth>();
            attack = GetComponent<EnemyAttack>();
        }

        private void OnEnable()
        {
            CurrentState = State.Idle;
            Target = null;
            hasRallyPoint = false;   // reused from the pool: forget the last life's rally point
            // Spread the target checks of a freshly spawned group over different frames.
            nextRetargetTime = Time.time + Random.Range(0f, retargetInterval);
            health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            health.OnDied -= HandleDied;
        }

        private void Update()
        {
            if (CurrentState == State.Dead) return;

            if (Time.time >= nextRetargetTime)
            {
                nextRetargetTime = Time.time + retargetInterval;
                UpdateTarget();
            }

            if (Target == null)
            {
                if (hasRallyPoint) Rally();
                else EnterIdle();
                return;
            }

            float distance = FlatDistance(Target.transform.position);
            if (attack != null && distance <= attack.Range && HasLineOfSight(Target))
            {
                CurrentState = State.Attack;
                movement.Stop();
                movement.FaceTowards(Target.transform.position);
                attack.Tick(Target);
            }
            else
            {
                if (CurrentState != State.Chase) chaseDestination = Vector3.positiveInfinity;
                CurrentState = State.Chase;
                if (attack != null) attack.End();

                // Re-path only when the target has actually moved, not every frame.
                Vector3 targetPosition = Target.transform.position;
                if ((targetPosition - chaseDestination).sqrMagnitude > RepathDistance * RepathDistance)
                {
                    chaseDestination = targetPosition;
                    movement.MoveTo(targetPosition);
                }
            }
        }

        /// <summary>Walk to this point while no target is in sight (e.g. where the player was last seen); idle on arrival.</summary>
        public void SetRallyPoint(Vector3 point)
        {
            rallyPoint = point;
            hasRallyPoint = true;
            if (CurrentState == State.Rally) movement.MoveTo(rallyPoint);
        }

        private void Rally()
        {
            if (FlatDistance(rallyPoint) <= rallyArriveDistance)
            {
                hasRallyPoint = false;
                EnterIdle();
                return;
            }
            if (CurrentState == State.Rally) return;
            // Coming here from Idle or from losing a target: (re)issue the walk once.
            CurrentState = State.Rally;
            if (attack != null) attack.End();
            movement.MoveTo(rallyPoint);
        }

        private void EnterIdle()
        {
            if (CurrentState == State.Idle) return;
            CurrentState = State.Idle;
            movement.Stop();
            if (attack != null) attack.End();
        }

        /// <summary>Keeps the current target while it is alive and not too far; otherwise picks the nearest visible hostile in sight.</summary>
        private void UpdateTarget()
        {
            if (Target != null && Target.IsAlive && FlatDistance(Target.transform.position) <= loseSightRange) return;

            Target = null;
            float bestSqr = sightRange * sightRange;
            var members = FactionMember.Active;
            for (int i = 0; i < members.Count; i++)
            {
                var other = members[i];
                if (other == self || !other.IsAlive || !self.IsHostileTo(other)) continue;

                Vector3 offset = other.transform.position - transform.position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr > bestSqr || !HasLineOfSight(other)) continue;

                bestSqr = sqr;
                Target = other;
            }
        }

        /// <summary>True unless level geometry (anything that isn't a fighter) sits between our eyes and the target.</summary>
        private bool HasLineOfSight(FactionMember other)
        {
            Vector3 from = self.AimPoint;
            Vector3 to = other.AimPoint;
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(from, direction / distance, SightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (SightHits[i].collider.GetComponent<FactionMember>() == null) return false;
            }
            return true;
        }

        private float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void HandleDied(EnemyHealth _)
        {
            CurrentState = State.Dead;
            Target = null;
            if (attack != null) attack.End();
            movement.Disable();
        }
    }
}
