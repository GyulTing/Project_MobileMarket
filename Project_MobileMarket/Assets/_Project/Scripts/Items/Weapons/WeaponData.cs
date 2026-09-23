using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.Items
{
    public enum WeaponType
    {
        None,
        Pistol,
        Rifle,
    }

    [CreateAssetMenu(fileName = "WeaponData", menuName = "MobileMarket/Weapon Data")]
    public class WeaponData : ItemData
    {
        public WeaponType weaponType = WeaponType.None;
        public float damage = 10f;
        public float fireRate = 3f;
        public float aimSpeed = 15f;
    }
}
