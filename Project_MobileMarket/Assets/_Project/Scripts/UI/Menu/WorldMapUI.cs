using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MobileMarket.Data;

namespace MobileMarket.UI
{
    /// <summary>
    /// The truck's travel screen: the world map with a dot per region joined by dotted routes, a ring on the current
    /// location, and an info box with a depart button for the selected region. Esc or the close button shuts it.
    /// Markers and routes are built the first time it opens.
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        /// <summary>Raised with true when the screen opens and false when it closes.</summary>
        public event Action<bool> OnVisibilityChanged;
        /// <summary>Raised when the player confirms a destination.</summary>
        public event Action<RegionData> OnDepartRequested;

        [SerializeField] private RectTransform mapArea;
        [SerializeField] private RectTransform routeRoot;
        [SerializeField] private RegionNodeUI nodeTemplate;
        [SerializeField] private Image routeDotTemplate;
        [SerializeField] private float routeDotSpacing = 16f;
        [SerializeField] private float routeNodeGap = 18f;

        [Header("Info box")]
        [SerializeField] private Text infoName;
        [SerializeField] private Text infoType;
        [SerializeField] private Text infoStatus;
        [SerializeField] private Button departButton;
        [SerializeField] private Button closeButton;

        private readonly List<RegionNodeUI> nodes = new List<RegionNodeUI>();
        private RegionData current;
        private RegionData selected;
        private bool built;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            departButton.onClick.AddListener(HandleDepart);
            closeButton.onClick.AddListener(Close);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open(IReadOnlyList<RegionData> regions, RegionData currentRegion)
        {
            if (!built) Build(regions);
            current = currentRegion;
            foreach (var node in nodes) node.SetCurrent(node.Region == current);
            gameObject.SetActive(true);
            Select(current != null ? current : regions[0]);
            OnVisibilityChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            OnVisibilityChanged?.Invoke(false);
        }

        public void Select(RegionData region)
        {
            selected = region;
            foreach (var node in nodes) node.SetSelected(node.Region == region);

            infoName.text = region.DisplayName;
            infoType.text = region.Type == RegionType.Home ? "거주지" : region.Type == RegionType.Combat ? "전투 지역" : "정착지";
            bool here = region == current;
            infoStatus.text = here ? "현재 위치" : region.IsLocked ? "NOT AVAILABLE" : region.Type == RegionType.Combat ? "탈출하면 거주지로 돌아옵니다" : "";
            departButton.interactable = !here && !region.IsLocked;
        }

        private void HandleDepart()
        {
            if (selected == null || selected == current || selected.IsLocked) return;
            var destination = selected;
            Close();
            OnDepartRequested?.Invoke(destination);
        }

        private void Build(IReadOnlyList<RegionData> regions)
        {
            built = true;
            nodeTemplate.gameObject.SetActive(false);
            routeDotTemplate.gameObject.SetActive(false);

            var drawn = new HashSet<(RegionData, RegionData)>();
            foreach (var region in regions)
            {
                if (region.Connections == null) continue;
                foreach (var other in region.Connections)
                {
                    if (other == null || drawn.Contains((other, region))) continue;
                    drawn.Add((region, other));
                    DrawRoute(MapPoint(region), MapPoint(other));
                }
            }

            foreach (var region in regions)
            {
                var node = Instantiate(nodeTemplate, mapArea);
                node.gameObject.SetActive(true);
                node.name = "Node_" + region.name;
                ((RectTransform)node.transform).anchoredPosition = MapPoint(region);
                node.Bind(this, region);
                nodes.Add(node);
            }
        }

        private Vector2 MapPoint(RegionData region)
        {
            var size = mapArea.rect.size;
            return new Vector2((region.MapPosition.x - 0.5f) * size.x, (region.MapPosition.y - 0.5f) * size.y);
        }

        private void DrawRoute(Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to) - routeNodeGap * 2f;
            if (length <= 0f) return;
            var dir = (to - from).normalized;
            var start = from + dir * routeNodeGap;
            for (float d = 0f; d <= length; d += routeDotSpacing)
            {
                var dot = Instantiate(routeDotTemplate, routeRoot);
                dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = start + dir * d;
            }
        }
    }
}
