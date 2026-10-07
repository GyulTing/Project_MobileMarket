using UnityEngine;

namespace MobileMarket.Data
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "MobileMarket/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string id;
        public string itemName;
        public float weight = 1f;
        public Sprite icon;
    }
}
