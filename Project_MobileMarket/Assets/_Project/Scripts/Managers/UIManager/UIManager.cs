using System;
using UnityEngine;
using UnityEngine.InputSystem;
using MobileMarket.Data;
using MobileMarket.Environment;
using MobileMarket.Systems;
using MobileMarket.UI;

namespace MobileMarket.Managers
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        /// <summary>True while a screen that owns the input is up (world map) or a trip is loading; the player stops reading input.</summary>
        public static event Action<bool> OnGameplayInputBlocked;

        [SerializeField] private InventoryScreenUI inventoryScreen;
        [SerializeField] private Inventory playerInventory;
        [SerializeField] private DialoguePanel dialoguePanel;
        [SerializeField] private WorldMapUI worldMap;
        [Tooltip("Keypad for bunker code terminals (optional; only scenes with terminals need it).")]
        [SerializeField] private CodePanelUI codePanel;

        private bool mapOpen;
        private bool travelling;
        private bool codeOpen;

        private void Update()
        {
            if (mapOpen || travelling || codeOpen) return;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                inventoryScreen.Toggle(playerInventory);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            LootBox.OnBoxOpened += HandleBoxOpened;
            MerchantNPC.OnTalkRequested += HandleTalkRequested;
            TruckInteractable.OnTravelRequested += HandleTravelRequested;
            SceneLoader.OnTravelStateChanged += HandleTravelStateChanged;
            if (worldMap != null)
            {
                worldMap.OnVisibilityChanged += HandleMapVisibilityChanged;
                worldMap.OnDepartRequested += HandleDepartRequested;
            }
            CodeTerminal.OnCodeRequested += HandleCodeRequested;
            if (codePanel != null) codePanel.OnVisibilityChanged += HandleCodePanelVisibilityChanged;
        }

        private void OnDisable()
        {
            LootBox.OnBoxOpened -= HandleBoxOpened;
            MerchantNPC.OnTalkRequested -= HandleTalkRequested;
            TruckInteractable.OnTravelRequested -= HandleTravelRequested;
            SceneLoader.OnTravelStateChanged -= HandleTravelStateChanged;
            if (worldMap != null)
            {
                worldMap.OnVisibilityChanged -= HandleMapVisibilityChanged;
                worldMap.OnDepartRequested -= HandleDepartRequested;
            }
            CodeTerminal.OnCodeRequested -= HandleCodeRequested;
            if (codePanel != null) codePanel.OnVisibilityChanged -= HandleCodePanelVisibilityChanged;
        }

        private void HandleBoxOpened(LootBox box, GameObject interactor)
        {
            var inventory = interactor.GetComponent<Inventory>();
            if (inventory != null) inventoryScreen.Open(inventory, box);
        }

        private void HandleTalkRequested(MerchantNPC merchant, GameObject interactor)
        {
            dialoguePanel.Open(merchant, interactor);
        }

        private void HandleTravelRequested(TruckInteractable truck, GameObject interactor)
        {
            var loader = SceneLoader.Instance;
            if (worldMap == null || loader == null || loader.Regions.Count == 0) return;
            if (inventoryScreen.IsOpen) inventoryScreen.Close();
            worldMap.Open(loader.Regions, loader.CurrentRegion);
        }

        private void HandleDepartRequested(RegionData destination)
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.TravelTo(destination);
        }

        private void HandleMapVisibilityChanged(bool open)
        {
            mapOpen = open;
            OnGameplayInputBlocked?.Invoke(mapOpen || travelling || codeOpen);
        }

        private void HandleCodeRequested(CodeTerminal terminal)
        {
            if (codePanel == null) return;
            if (inventoryScreen.IsOpen) inventoryScreen.Close();
            codePanel.Open(terminal);
        }

        private void HandleCodePanelVisibilityChanged(bool open)
        {
            codeOpen = open;
            OnGameplayInputBlocked?.Invoke(mapOpen || travelling || codeOpen);
        }

        private void HandleTravelStateChanged(bool isTravelling)
        {
            travelling = isTravelling;
            OnGameplayInputBlocked?.Invoke(mapOpen || travelling || codeOpen);
        }
    }
}
