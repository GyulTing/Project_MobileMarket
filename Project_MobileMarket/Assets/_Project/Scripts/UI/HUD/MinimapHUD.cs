using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Environment;
using MobileMarket.Managers;

namespace MobileMarket.UI
{
    /// <summary>
    /// Top-right round minimap, north-up. The scene's baked map image (MinimapArea) scrolls under a circular mask so the
    /// player stays in the middle; the arrow turns with the player and the objective marker sticks to the rim when it's out of range.
    /// </summary>
    public class MinimapHUD : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [Tooltip("This scene's minimap picture and bounds. Without one the minimap is just the dark disc.")]
        [SerializeField] private MinimapArea area;
        [Tooltip("Circular masked viewport; its half width is the minimap radius.")]
        [SerializeField] private RectTransform viewport;
        [Tooltip("Image showing the map picture (child of the viewport, pivot bottom-left).")]
        [SerializeField] private Image mapImage;
        [SerializeField] private RectTransform playerArrow;
        [SerializeField] private RectTransform objectiveMarker;
        [Tooltip("World meters from the center to the rim.")]
        [SerializeField] private float visibleRadius = 18f;
        [Tooltip("Pixels the objective marker keeps inside the rim when clamped.")]
        [SerializeField] private float markerRimInset = 10f;

        private RectTransform mapRect;
        private float pixelsPerMeter;
        private float radius;

        private void Start()
        {
            radius = viewport != null ? viewport.rect.width * 0.5f : 0f;
            pixelsPerMeter = visibleRadius > 0f ? radius / visibleRadius : 0f;

            bool hasMap = area != null && area.MapImage != null && mapImage != null;
            if (mapImage != null)
            {
                mapImage.enabled = hasMap;
                mapRect = mapImage.rectTransform;
            }
            if (!hasMap) return;

            mapImage.sprite = area.MapImage;
            mapRect.sizeDelta = area.WorldSize * pixelsPerMeter;
        }

        private void LateUpdate()
        {
            if (player == null) return;

            Vector3 playerPos = player.position;
            if (mapRect != null && area != null && mapImage.enabled)
            {
                // The map's bottom-left pivot sits at the viewport center, so shifting it by the player's offset centers the player.
                Vector2 normalized = area.WorldToNormalized(playerPos);
                mapRect.anchoredPosition = -Vector2.Scale(normalized, mapRect.sizeDelta);
            }

            if (playerArrow != null) playerArrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            UpdateObjectiveMarker(playerPos);
        }

        private void UpdateObjectiveMarker(Vector3 playerPos)
        {
            if (objectiveMarker == null) return;

            var target = QuestManager.Instance != null ? QuestManager.Instance.ObjectiveTarget : null;
            bool show = target != null;
            if (objectiveMarker.gameObject.activeSelf != show) objectiveMarker.gameObject.SetActive(show);
            if (!show) return;

            Vector3 delta = target.position - playerPos;
            var offset = new Vector2(delta.x, delta.z) * pixelsPerMeter;
            float maxDistance = Mathf.Max(0f, radius - markerRimInset);
            objectiveMarker.anchoredPosition = Vector2.ClampMagnitude(offset, maxDistance);
        }
    }
}
