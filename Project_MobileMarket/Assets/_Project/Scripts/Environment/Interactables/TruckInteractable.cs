using System;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Managers;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The player's truck. At home or in a settlement, F asks for the world map (UIManager opens it). In a combat zone, F
    /// starts the extraction ("탈출 중...", interrupted by moving or getting hit) and, once it completes, drives home.
    /// </summary>
    public class TruckInteractable : MonoBehaviour, IInteractable
    {
        /// <summary>Raised when the player wants to pick a destination from this truck.</summary>
        public static event Action<TruckInteractable, GameObject> OnTravelRequested;

        [SerializeField] private float extractDuration = 5f;
        [SerializeField] private string extractText = "탈출 중...";

        private bool isExtracting;
        private Action completeExtraction;
        private Action cancelExtraction;

        private void Awake()
        {
            completeExtraction = HandleExtractionCompleted;
            cancelExtraction = HandleExtractionCancelled;
        }

        public void Interact(GameObject interactor)
        {
            var loader = SceneLoader.Instance;
            if (loader == null || loader.IsTravelling || isExtracting) return;

            var region = loader.CurrentRegion;
            if (region == null || region.Type != RegionType.Combat)
            {
                OnTravelRequested?.Invoke(this, interactor);
                return;
            }

            var runner = interactor.GetComponent<ITimedActionRunner>();
            if (runner == null)
            {
                HandleExtractionCompleted();
                return;
            }
            isExtracting = runner.BeginTimedAction(extractText, extractDuration, completeExtraction, cancelExtraction);
        }

        private void HandleExtractionCompleted()
        {
            isExtracting = false;
            if (SceneLoader.Instance != null) SceneLoader.Instance.ReturnHome();
        }

        private void HandleExtractionCancelled() => isExtracting = false;
    }
}
