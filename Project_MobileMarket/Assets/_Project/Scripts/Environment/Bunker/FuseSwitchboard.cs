using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The 2F power-room switchboard (first half of P1): one fuse slot is empty. F with a fuse in the inventory puts it in.
    /// The generator only starts once the fuse is in.
    /// </summary>
    public class FuseSwitchboard : MonoBehaviour, IInteractable, IResolvable
    {
        [SerializeField] private ItemData fuse;
        [Tooltip("Shown once the fuse is in the slot.")]
        [SerializeField] private GameObject installedFuseVisual;
        [SerializeField] private string missingText = "배전반의 퓨즈 슬롯 하나가 비어 있다. 맞는 퓨즈를 찾아야 한다.";
        [SerializeField] private string installedText = "퓨즈를 끼워 넣었다.";
        [SerializeField] private string alreadyText = "퓨즈가 제자리에 끼워져 있다.";

        public bool FuseInstalled { get; private set; }
        public bool IsResolved => FuseInstalled;

        private void Awake()
        {
            if (installedFuseVisual != null) installedFuseVisual.SetActive(false);
        }

        public void Interact(GameObject interactor)
        {
            if (FuseInstalled)
            {
                InteractionFeedback.Show(alreadyText);
                return;
            }
            var inventory = interactor.GetComponent<Inventory>();
            if (fuse == null || inventory == null || !inventory.RemoveItem(fuse))
            {
                InteractionFeedback.Show(missingText);
                return;
            }
            FuseInstalled = true;
            if (installedFuseVisual != null) installedFuseVisual.SetActive(true);
            InteractionFeedback.Show(installedText);
        }
    }
}
