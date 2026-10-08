using System;
using UnityEngine;
using UnityEngine.Serialization;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Environment
{
    /// <summary>
    /// A lootable container with its own item grid, filled when it appears on the map. The first opening takes openDuration
    /// (run by the interactor's ITimedActionRunner — "여는 중..." with a gauge, interrupted by moving or getting hit); after
    /// that the open model stays on for good and the box opens instantly. Opening shows its grid next to the player's inventory.
    /// </summary>
    public class LootBox : MonoBehaviour, IInteractable
    {
        public static event Action<LootBox, GameObject> OnBoxOpened;

        [SerializeField] private string displayName = "상자";
        [SerializeField] private int columns = 3;
        [SerializeField] private int rows = 3;

        [SerializeField] private LootTable lootTable;
        [FormerlySerializedAs("slotCount")]
        [SerializeField] private int rollCount = 3;

        [Tooltip("If set, the box holds exactly these items instead of rolling the loot table.")]
        [SerializeField] private ItemData[] fixedItems;

        [Header("Opening")]
        [Tooltip("Seconds the first opening takes. 0 = opens instantly.")]
        [SerializeField] private float openDuration = 1.5f;
        [SerializeField] private string openingText = "여는 중...";
        [Tooltip("Model shown until the box is opened for the first time.")]
        [SerializeField] private GameObject closedVisual;
        [Tooltip("Model shown once the box has been opened.")]
        [SerializeField] private GameObject openVisual;

        [Header("Window")]
        [Tooltip("Drawing behind this box's grid in the inventory screen. Empty = the default crate drawing.")]
        [SerializeField] private Sprite panelArt;
        [Tooltip("Normalized area of panelArt (x, y from the bottom-left, width, height) the grid is fitted into.")]
        [SerializeField] private Rect panelGridArea = new Rect(0.2f, 0.2f, 0.6f, 0.6f);

        public string DisplayName => displayName;
        public Sprite PanelArt => panelArt;
        public Rect PanelGridArea => panelGridArea;
        public ItemGrid Grid { get; private set; }
        public bool IsOpened { get; private set; }

        private bool isOpening;
        private GameObject openingInteractor;
        private Action completeOpening;
        private Action cancelOpening;

        private void Awake()
        {
            completeOpening = HandleOpeningCompleted;
            cancelOpening = HandleOpeningCancelled;
            SetOpenedVisual(false);

            // Containers can hold backpacks that still have items in them.
            Grid = new ItemGrid(columns, rows, true);

            if (fixedItems != null && fixedItems.Length > 0)
            {
                foreach (var item in fixedItems) if (item != null) Grid.TryAdd(new ItemInstance(item));
                return;
            }

            if (lootTable == null) return;
            for (int i = 0; i < rollCount; i++)
            {
                var item = lootTable.RollItem();
                if (item != null) Grid.TryAdd(new ItemInstance(item));
            }
        }

        public void Interact(GameObject interactor)
        {
            if (isOpening) return;
            if (IsOpened)
            {
                OnBoxOpened?.Invoke(this, interactor);
                return;
            }

            var runner = interactor.GetComponent<ITimedActionRunner>();
            if (openDuration <= 0f || runner == null)
            {
                openingInteractor = interactor;
                HandleOpeningCompleted();
                return;
            }

            if (!runner.BeginTimedAction(openingText, openDuration, completeOpening, cancelOpening)) return;
            isOpening = true;
            openingInteractor = interactor;
        }

        private void HandleOpeningCompleted()
        {
            isOpening = false;
            IsOpened = true;
            SetOpenedVisual(true);

            var interactor = openingInteractor;
            openingInteractor = null;
            if (interactor != null) OnBoxOpened?.Invoke(this, interactor);
        }

        private void HandleOpeningCancelled()
        {
            isOpening = false;
            openingInteractor = null;
        }

        private void SetOpenedVisual(bool opened)
        {
            if (closedVisual != null) closedVisual.SetActive(!opened);
            if (openVisual != null) openVisual.SetActive(opened);
        }
    }
}
