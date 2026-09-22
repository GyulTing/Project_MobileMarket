using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MobileMarket.Data;

namespace MobileMarket.UI
{
    /// <summary>A draggable read-out of one inventory slot, used to drag items (e.g. weapons) onto an EquipSlotUI.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BagSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Text label;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private Vector2 originalPosition;

        public ItemData Item { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
        }

        public void SetItem(ItemData item)
        {
            Item = item;
            if (label != null) label.text = item != null ? item.itemName : "";
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null) return;
            originalPosition = rectTransform.anchoredPosition;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Item == null) return;
            rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            rectTransform.anchoredPosition = originalPosition;
        }
    }
}
