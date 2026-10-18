using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Player;

namespace MobileMarket.UI
{
    /// <summary>
    /// Bottom-left health and stamina bars. Health follows PlayerHealth; stamina has no system yet, so its bar just stays full.
    /// Updates only when PlayerHealth reports a change.
    /// </summary>
    public class PlayerVitalsHUD : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("Filled (horizontal) image showing the remaining health.")]
        [SerializeField] private Image healthFill;
        [Tooltip("Filled (horizontal) image for stamina. Not driven by anything until stamina exists.")]
        [SerializeField] private Image staminaFill;
        [SerializeField] private Color healthColor = new Color(0.93f, 0.93f, 0.91f);
        [Tooltip("Health bar tint at or below lowHealthRatio.")]
        [SerializeField] private Color lowHealthColor = new Color(0.86f, 0.25f, 0.2f);
        [SerializeField] private float lowHealthRatio = 0.25f;

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged += RefreshHealth;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= RefreshHealth;
        }

        private void Start()
        {
            if (playerHealth != null) RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            if (staminaFill != null) staminaFill.fillAmount = 1f;
        }

        private void RefreshHealth(float current, float max)
        {
            if (healthFill == null) return;
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            healthFill.fillAmount = ratio;
            healthFill.color = ratio <= lowHealthRatio ? lowHealthColor : healthColor;
        }
    }
}
