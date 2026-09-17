using System;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    public class MerchantNPC : MonoBehaviour, IInteractable
    {
        public static event Action<MerchantNPC, GameObject> OnTalkRequested;

        [SerializeField] private string merchantName = "상인";
        [SerializeField] private string greeting = "어서 오게, 나그네.";

        public string MerchantName => merchantName;
        public string Greeting => greeting;

        public void Interact(GameObject interactor)
        {
            OnTalkRequested?.Invoke(this, interactor);
        }
    }
}
