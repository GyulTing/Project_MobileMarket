using System;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Environment;

namespace MobileMarket.Player
{
    /// <summary>
    /// Finds the nearest IInteractable in range and triggers it on F press. Also keeps track of that nearest one between
    /// presses and announces it, so the HUD can show an "[F] ..." prompt over it.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInteractor : MonoBehaviour
    {
        /// <summary>The interactable the player would use with F changed: its transform and prompt label, or null when none.</summary>
        public static event Action<Transform, string> OnFocusChanged;

        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private LayerMask interactableLayer = ~0;
        [Tooltip("Seconds between prompt focus checks.")]
        [SerializeField] private float focusInterval = 0.1f;

        private PlayerInput input;
        private PlayerHealth health;
        private readonly Collider[] hits = new Collider[8];
        private Collider focused;
        private bool focusShown;
        private float nextFocusCheck;

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
            SetFocus(null);
        }

        private void HandleDied() => enabled = false;

        private void Update()
        {
            if (Time.time >= nextFocusCheck)
            {
                nextFocusCheck = Time.time + focusInterval;
                SetFocus(FindClosest(out _));
            }

            if (!input.IsInteractPressed) return;

            FindClosest(out IInteractable closest);
            closest?.Interact(gameObject);
            nextFocusCheck = 0f; // the target may have changed state (door opened, note read)
        }

        private Collider FindClosest(out IInteractable closest)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactRadius, hits, interactableLayer);
            float closestSqrDistance = float.MaxValue;
            closest = null;
            Collider closestCollider = null;

            for (int i = 0; i < count; i++)
            {
                var interactable = hits[i].GetComponent<IInteractable>();
                if (interactable == null) continue;

                float sqrDistance = (hits[i].transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = interactable;
                    closestCollider = hits[i];
                }
            }
            return closestCollider;
        }

        private void SetFocus(Collider target)
        {
            InteractionPrompt prompt = null;
            if (target != null) target.TryGetComponent(out prompt);
            bool shown = target != null && (prompt == null || prompt.IsShown);
            if (target == focused && shown == focusShown) return;
            focused = target;
            focusShown = shown;
            OnFocusChanged?.Invoke(shown ? target.transform : null, prompt != null ? prompt.Label : InteractionPrompt.DefaultLabel);
        }
    }
}
