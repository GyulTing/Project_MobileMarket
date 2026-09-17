using UnityEngine;

namespace MobileMarket.Items
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "MobileMarket/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName;
        public float damage = 10f;
    }
}
