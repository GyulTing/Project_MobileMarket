using System.Collections;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Managers;
using MobileMarket.Systems;
using MobileMarket.UI;

namespace MobileMarket.Environment
{
    /// <summary>Plant a seed from the interactor's inventory, grow it one step per in-game day, harvest when ready.</summary>
    public class FarmPlot : MonoBehaviour, IInteractable
    {
        [SerializeField] private float plantDuration = 2f;
        [SerializeField] private float cancelDistance = 2.5f;
        [SerializeField] private Transform cropAnchor;

        private SeedData plantedSeed;
        private int daysGrown;
        private bool isPlanting;
        private GameObject currentVisual;

        public bool IsReady => plantedSeed != null && daysGrown >= plantedSeed.daysToGrow;

        private void Awake()
        {
            if (cropAnchor == null) cropAnchor = transform;
        }

        private void OnEnable()
        {
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        public void Interact(GameObject interactor)
        {
            if (isPlanting) return;

            var inventory = interactor.GetComponent<Inventory>();
            if (inventory == null) return;

            if (plantedSeed == null)
            {
                var seed = inventory.FindFirst<SeedData>();
                if (seed == null)
                {
                    Debug.Log("[FarmPlot] No seed in inventory.");
                    return;
                }
                StartCoroutine(PlantRoutine(interactor, inventory, seed));
            }
            else if (IsReady)
            {
                Harvest(inventory);
            }
        }

        private IEnumerator PlantRoutine(GameObject interactor, Inventory inventory, SeedData seed)
        {
            isPlanting = true;
            var label = interactor.GetComponentInChildren<ActionLabelUI>();
            if (label != null) label.Show("심는 중...");

            float sqrCancelDistance = cancelDistance * cancelDistance;
            float elapsed = 0f;
            while (elapsed < plantDuration)
            {
                if ((interactor.transform.position - transform.position).sqrMagnitude > sqrCancelDistance)
                {
                    FinishPlanting(label);
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!inventory.RemoveItem(seed))
            {
                FinishPlanting(label);
                yield break;
            }
            plantedSeed = seed;
            daysGrown = 0;
            RefreshVisual();
            FinishPlanting(label);
        }

        private void FinishPlanting(ActionLabelUI label)
        {
            isPlanting = false;
            if (label != null) label.Hide();
        }

        private void HandleDayChanged(int day)
        {
            if (plantedSeed == null || IsReady) return;
            daysGrown++;
            RefreshVisual();
        }

        private void Harvest(Inventory inventory)
        {
            var crop = plantedSeed.harvestItem;
            if (crop == null) return;

            if (!inventory.HasFreeCells(plantedSeed.harvestAmount))
            {
                Debug.Log("[FarmPlot] Not enough free inventory cells to harvest.");
                return;
            }

            for (int i = 0; i < plantedSeed.harvestAmount; i++) inventory.TryAddItem(crop);

            plantedSeed = null;
            daysGrown = 0;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (currentVisual != null) Destroy(currentVisual);
            currentVisual = null;

            if (plantedSeed == null || plantedSeed.stagePrefabs == null || plantedSeed.stagePrefabs.Length == 0) return;

            int lastStage = plantedSeed.stagePrefabs.Length - 1;
            int stage = IsReady || lastStage == 0
                ? lastStage
                : Mathf.Min(lastStage - 1, daysGrown * lastStage / plantedSeed.daysToGrow);

            var prefab = plantedSeed.stagePrefabs[stage];
            if (prefab != null) currentVisual = Instantiate(prefab, cropAnchor);
        }
    }
}
