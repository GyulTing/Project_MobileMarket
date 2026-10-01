using UnityEngine;

namespace MobileMarket.Data
{
    [CreateAssetMenu(fileName = "SeedData", menuName = "MobileMarket/Seed Data")]
    public class SeedData : ItemData
    {
        public ItemData harvestItem;
        public int harvestAmount = 3;
        public int daysToGrow = 3;

        [Tooltip("Visual per growth stage. First = just planted, last = ready to harvest.")]
        public GameObject[] stagePrefabs;
    }
}
