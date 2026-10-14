using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Data;

namespace MobileMarket.UI
{
    /// <summary>One clickable place on the world map: a dot, its name, "NOT AVAILABLE" when locked, a ring where the player is.</summary>
    public class RegionNodeUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image dot;
        [SerializeField] private Image currentRing;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private Text nameText;
        [SerializeField] private Text statusText;

        [SerializeField] private Color homeColor = new Color32(240, 200, 110, 255);
        [SerializeField] private Color settlementColor = new Color32(225, 225, 215, 255);
        [SerializeField] private Color combatColor = new Color32(225, 95, 70, 255);
        [SerializeField] private Color lockedColor = new Color32(120, 120, 115, 255);

        public RegionData Region { get; private set; }

        private WorldMapUI map;

        public void Bind(WorldMapUI owner, RegionData region)
        {
            map = owner;
            Region = region;
            nameText.text = region.DisplayName;
            statusText.gameObject.SetActive(region.IsLocked);

            var color = region.IsLocked ? lockedColor
                : region.Type == RegionType.Home ? homeColor
                : region.Type == RegionType.Combat ? combatColor
                : settlementColor;
            dot.color = color;
            nameText.color = region.IsLocked ? lockedColor : settlementColor;

            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        public void SetCurrent(bool isCurrent) => currentRing.enabled = isCurrent;
        public void SetSelected(bool isSelected) => selectedFrame.enabled = isSelected;

        private void HandleClicked() => map.Select(Region);
    }
}
