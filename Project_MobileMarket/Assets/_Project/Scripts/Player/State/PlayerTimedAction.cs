using System;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.UI;

namespace MobileMarket.Player
{
    /// <summary>
    /// Runs one timed action at a time (e.g. "여는 중..."), showing the label and a filling gauge above the player's head.
    /// Moving, getting hit or dying interrupts it.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerTimedAction : MonoBehaviour, ITimedActionRunner
    {
        [Tooltip("Move input above this magnitude cancels the action.")]
        [SerializeField] private float moveCancelThreshold = 0.1f;

        public bool IsBusy { get; private set; }

        private PlayerInput input;
        private PlayerHealth health;
        private ActionLabelUI label;
        private float duration;
        private float elapsed;
        private float healthAtStart;
        private Action onComplete;
        private Action onCancel;

        private void Awake()
        {
            input = GetComponent<PlayerInput>();
            health = GetComponent<PlayerHealth>();
            label = GetComponentInChildren<ActionLabelUI>(true);
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDied += Cancel;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnDied -= Cancel;
            }
            Cancel();
        }

        public bool BeginTimedAction(string text, float seconds, Action completed, Action cancelled)
        {
            if (IsBusy || !isActiveAndEnabled) return false;

            IsBusy = true;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            healthAtStart = health != null ? health.CurrentHealth : 0f;
            onComplete = completed;
            onCancel = cancelled;
            if (label != null)
            {
                label.Show(text);
                label.SetProgress(0f);
            }
            return true;
        }

        private void Update()
        {
            if (!IsBusy) return;
            if (input.MoveInput.sqrMagnitude > moveCancelThreshold * moveCancelThreshold)
            {
                Cancel();
                return;
            }

            elapsed += Time.deltaTime;
            if (label != null) label.SetProgress(elapsed / duration);
            if (elapsed < duration) return;

            var completed = onComplete;
            Finish();
            completed?.Invoke();
        }

        /// <summary>Only damage cancels (healing doesn't).</summary>
        private void HandleHealthChanged(float current, float max)
        {
            if (IsBusy && current < healthAtStart) Cancel();
        }

        private void Cancel()
        {
            if (!IsBusy) return;
            var cancelled = onCancel;
            Finish();
            cancelled?.Invoke();
        }

        private void Finish()
        {
            IsBusy = false;
            onComplete = null;
            onCancel = null;
            if (label != null) label.Hide();
        }
    }
}
