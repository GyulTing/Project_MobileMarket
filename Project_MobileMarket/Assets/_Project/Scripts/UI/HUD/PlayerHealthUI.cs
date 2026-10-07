using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Player;

namespace MobileMarket.UI
{
    /// <summary>
    /// Temporary bottom-left HP bar + "HP 75 / 100" readout until the real HUD exists.
    /// Updates only when PlayerHealth reports a change.
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("Stretched horizontally from the left edge to show the remaining health.")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private Text label;

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged += Refresh;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= Refresh;
        }

        private void Start()
        {
            if (playerHealth != null) Refresh(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void Refresh(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (fill != null) fill.anchorMax = new Vector2(ratio, 1f);
            if (label != null) label.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
