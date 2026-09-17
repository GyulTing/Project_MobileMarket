using UnityEngine;
using UnityEngine.InputSystem;
using MobileMarket.Environment;
using MobileMarket.Systems;
using MobileMarket.UI;

namespace MobileMarket.Managers
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private LootPanel lootPanel;
        [SerializeField] private InventoryPanelUI bagPanel;
        [SerializeField] private Inventory playerInventory;
        [SerializeField] private DialoguePanel dialoguePanel;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                bagPanel.Toggle(playerInventory);
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
        }

        private void OnDisable()
        {
            LootBox.OnBoxOpened -= HandleBoxOpened;
            MerchantNPC.OnTalkRequested -= HandleTalkRequested;
        }

        private void HandleBoxOpened(LootBox box, GameObject interactor)
        {
            lootPanel.Open(box, interactor);
        }

        private void HandleTalkRequested(MerchantNPC merchant, GameObject interactor)
        {
            dialoguePanel.Open(merchant, interactor);
        }
    }
}
