using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Environment
{
    /// <summary>A single item lying in the world (a fuse, a keycard). F puts it in the player's inventory.</summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item;
        [SerializeField] private string fullText = "가방에 자리가 없다.";

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<Inventory>();
            if (item == null || inventory == null) return;
            if (!inventory.TryAddItem(item))
            {
                InteractionFeedback.Show(fullText);
                return;
            }
            InteractionFeedback.Show(item.itemName + " 획득");
            gameObject.SetActive(false);
        }
    }
}
