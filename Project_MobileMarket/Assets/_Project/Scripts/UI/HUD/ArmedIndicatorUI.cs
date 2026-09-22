using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Player;

namespace MobileMarket.UI
{
    /// <summary>Temporary debug indicator: red dot when armed, blue when not.</summary>
    public class ArmedIndicatorUI : MonoBehaviour
    {
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private Image dot;

        private void OnEnable()
        {
            if (playerCombat != null) playerCombat.OnArmedChanged += Refresh;
            Refresh(playerCombat != null && playerCombat.IsArmed);
        }

        private void OnDisable()
        {
            if (playerCombat != null) playerCombat.OnArmedChanged -= Refresh;
        }

        private void Refresh(bool armed)
        {
            dot.color = armed ? Color.red : Color.blue;
        }
    }
}
