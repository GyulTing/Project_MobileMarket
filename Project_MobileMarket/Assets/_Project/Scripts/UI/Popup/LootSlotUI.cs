using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MobileMarket.Data;

namespace MobileMarket.UI
{
    /// <summary>A single draggable loot slot inside a LootPanel.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LootSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Text label;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private Vector2 originalPosition;

        public int SlotIndex { get; set; }
        public LootPanel Panel { get; set; }
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
            gameObject.SetActive(item != null);
            if (item != null && label != null) label.text = item.itemName;
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
