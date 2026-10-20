using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Player;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Edge of a collapsed floor: F drops the player through the hole onto the landing point on the floor below.
    /// One way only — the way back up has to be found elsewhere.
    /// </summary>
    public class DropDownPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform landingPoint;
        [SerializeField] private string dropText = "무너진 바닥 아래로 뛰어내렸다.";

        public void Interact(GameObject interactor)
        {
            if (landingPoint == null) return;
            var arrival = interactor.GetComponent<PlayerArrival>();
            if (arrival == null) return;
            arrival.PlaceAt(landingPoint.position, landingPoint.rotation);
            InteractionFeedback.Show(dropText);
        }
    }
}
