using UnityEditor;
using UnityEngine;
using MobileMarket.Items;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Stores each weapon's muzzle position in WeaponData.muzzleOffset, so the game never has to read mesh vertices
    /// (the weapon FBXs are not Read/Write enabled, which makes vertex access fail in builds).
    /// Re-run after adding a weapon or swapping its model.
    /// </summary>
    public static class WeaponMuzzleBaker
    {
        private const string ItemDir = "Assets/_Project/Resources/Items";

        /// <summary>The muzzle sits on the center of the vertices within this distance of the model's front tip.</summary>
        private const float TipDepth = 0.02f;

        [MenuItem("MobileMarket/Items/Bake Weapon Muzzles")]
        public static void BakeAll()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { ItemDir }))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon == null || weapon.weaponModelPrefab == null) continue;

                weapon.muzzleOffset = FindMuzzle(weapon.weaponModelPrefab.transform);
                EditorUtility.SetDirty(weapon);
                count++;
                Debug.Log($"[WeaponMuzzleBaker] {weapon.name}: {weapon.muzzleOffset.ToString("F3")}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponMuzzleBaker] Baked {count} weapons.");
        }

        /// <summary>Front tip of the model (barrel along local +Z), in the model root's local space.</summary>
        public static Vector3 FindMuzzle(Transform model)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            float frontZ = float.MinValue;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                var toModel = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices) frontZ = Mathf.Max(frontZ, toModel.MultiplyPoint3x4(v).z);
            }
            if (frontZ == float.MinValue) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                var toModel = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = toModel.MultiplyPoint3x4(v);
                    if (p.z < frontZ - TipDepth) continue;
                    sum += p;
                    n++;
                }
            }
            return new Vector3(sum.x / n, sum.y / n, frontZ);
        }
    }
}
