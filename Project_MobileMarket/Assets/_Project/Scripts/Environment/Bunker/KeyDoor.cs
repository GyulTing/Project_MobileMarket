using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Environment
{
    /// <summary>
    /// A door that opens with F when the player carries the required key item (the bunker's quest key, or an optional
    /// room key). Without it, the player is told what is missing.
    /// </summary>
    public class KeyDoor : MonoBehaviour, IInteractable, IResolvable
    {
        [SerializeField] private ItemData requiredKey;
        [SerializeField] private bool consumeKey;
        [Tooltip("Collider that blocks the way while closed (disabled when open).")]
        [SerializeField] private Collider blocker;
        [SerializeField] private string lockedText = "잠겨 있다. 맞는 열쇠가 필요하다.";
        [SerializeField] private string openedText = "";

        private DoorLeafMotion motion;

        public bool IsOpen { get; private set; }
        public bool IsResolved => IsOpen;

        private void Awake() => motion = GetComponentInChildren<DoorLeafMotion>();

        public void Interact(GameObject interactor)
        {
            if (IsOpen) return;
            var inventory = interactor.GetComponent<Inventory>();
            bool hasKey = requiredKey != null && inventory != null && inventory.Contains(requiredKey);
            if (!hasKey)
            {
                InteractionFeedback.Show(lockedText);
                return;
            }
            if (consumeKey) inventory.RemoveItem(requiredKey);
            IsOpen = true;
            InteractionFeedback.Show(openedText);
            if (blocker != null) blocker.enabled = false;
            if (motion != null) motion.Open();
        }
    }
}
