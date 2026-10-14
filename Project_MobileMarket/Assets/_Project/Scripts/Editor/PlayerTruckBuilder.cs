using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Turns the VARCO player truck export (Models/Varco_Item/상자,배치물/Vehicle_PlayerTruck.fbx — a cab-over mini truck with a
    /// closed shop container) into Prefabs/Player/P_PlayerTruck: real size, front turned to -Z, base on the ground, a
    /// BoxCollider, a carving NavMeshObstacle and the TruckInteractable. The material stays opaque (windows are painted into
    /// the texture). SceneLoader spawns it on each scene's ParkingSpot.
    /// </summary>
    public static class PlayerTruckBuilder
    {
        private const string Name = "Vehicle_PlayerTruck";
        private const string ModelPath = "Assets/_Project/Models/Varco_Item/상자,배치물/" + Name + ".fbx";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/P_PlayerTruck.prefab";

        // Real-world meters to world units (the 2.21-unit player stands for 1.75 m).
        private const float WorldScale = 2.21f / 1.75f;
        private const float RealLength = 5.1f;

        /// <summary>Yaw that turns the exported model's front (cab) to -Z (the export faces +Z).</summary>
        private const float Yaw = 180f;

        [MenuItem("MobileMarket/Environment/Build Player Truck")]
        public static void Build()
        {
            if (AssetImporter.GetAtPath(ModelPath) == null) { Debug.LogWarning("[PlayerTruckBuilder] Missing " + ModelPath); return; }
            VarcoCharacterTools.SetupStaticModel(ModelPath, Name);

            var root = new GameObject("P_PlayerTruck");
            var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
            body.transform.localRotation = Quaternion.Euler(0f, Yaw, 0f) * body.transform.localRotation;

            var bounds = WorldBounds(body);
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest > 0f) body.transform.localScale *= RealLength * WorldScale / longest;
            bounds = WorldBounds(body);
            body.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = WorldBounds(body);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = bounds.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            // F at the truck: world map in settlements, extraction in combat zones.
            root.AddComponent<MobileMarket.Environment.TruckInteractable>();

            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
