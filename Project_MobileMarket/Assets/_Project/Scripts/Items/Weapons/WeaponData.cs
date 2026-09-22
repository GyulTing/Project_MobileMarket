using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.Items
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "MobileMarket/Weapon Data")]
    public class WeaponData : ItemData
    {
        public float damage = 10f;
        public float fireRate = 3f;
        public float aimSpeed = 15f;
    }
}
