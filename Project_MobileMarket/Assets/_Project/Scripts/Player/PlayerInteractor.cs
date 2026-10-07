using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Player
{
    /// <summary>Finds the nearest IInteractable in range and triggers it on F press.</summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        private PlayerInput input;
        private PlayerHealth health;
        private readonly Collider[] hits = new Collider[8];

        private void Awake()
        {
            input = GetComponent<PlayerInput>();
            health = GetComponent<PlayerHealth>();
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
            if (!input.IsInteractPressed) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position, interactRadius, hits, interactableLayer);
            float closestSqrDistance = float.MaxValue;
            IInteractable closest = null;

            for (int i = 0; i < count; i++)
            {
                var interactable = hits[i].GetComponent<IInteractable>();
                if (interactable == null) continue;

                float sqrDistance = (hits[i].transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = interactable;
                }
            }

            Debug.Log($"[PlayerInteractor] F pressed, colliders in range: {count}, target found: {closest != null}");
            closest?.Interact(gameObject);
        }
    }
}
