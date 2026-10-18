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

    public enum FireMode
    {
        Single, // one shot per click
        Auto,   // keeps firing while the button is held
    }

    [CreateAssetMenu(fileName = "WeaponData", menuName = "MobileMarket/Weapon Data")]
    public class WeaponData : ItemData
    {
        public WeaponType weaponType = WeaponType.None;
        public FireMode fireMode = FireMode.Auto;
        public float damage = 10f;

        [Tooltip("Shots per second for Auto weapons (Single fires on every click). Set as 6 x (25 / (damage x pellets)) x sqrt(magazine / 30), min 2 for Auto.")]
        public float fireRate = 3f;
        public float aimSpeed = 15f;

        [Tooltip("Seconds to reload. The reload animation is sped up or slowed down to match (keep about 1.2 - 2.8s).")]
        public float reloadTime = 2f;

        [Tooltip("Visual kick per shot (weapon, upper body, camera). Started as damage x pellets / 50, clamped 0.4 - 1.")]
        [Range(0f, 2f)]
        public float recoil = 0.6f;
        public int magazineSize = 10;
        public int pelletCount = 1;
        public GameObject weaponModelPrefab;

        [Tooltip("White side-profile silhouette (muzzle left) for the in-game HUD. Without one the HUD draws the inventory icon as a silhouette.")]
        public Sprite hudIcon;

        [Tooltip("One shot (a single round, with its tail), played for every round fired.")]
        public AudioClip fireSound;
        [Range(0f, 1f)] public float fireVolume = 0.8f;

        [Tooltip("Muzzle position in the weapon model's local space (barrel along +Z). Filled by MobileMarket > Bake Weapon Muzzles.")]
        public Vector3 muzzleOffset;
    }
}
