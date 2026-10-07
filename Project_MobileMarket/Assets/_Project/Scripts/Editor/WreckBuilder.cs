using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Turns the VARCO military vehicle exports (Models/Varco_Item/상자,배치물/{Wreck|Vehicle}_{Name}.fbx) into map props
    /// (Prefabs/Environment/P_{Wreck|Vehicle}_{Name}): scaled down from the real vehicle length, base on the ground, a MeshCollider
    /// so players, enemies and bullets are blocked, and a carving NavMeshObstacle so enemy AI walks around it without a re-bake.
    /// </summary>
    public static class WreckBuilder
    {
        private const string ModelDir = "Assets/_Project/Models/Varco_Item/상자,배치물/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/";

        // Real-world meters to world units (the 2.21-unit player stands for 1.75 m), then shrunk for gameplay
        // (first to 75%, then another 15% smaller).
        private const float WorldScale = 2.21f / 1.75f;
        private const float SizeFactor = 0.75f * 0.85f;

        /// <summary>Model/prefab base name (Models/.../{name}.fbx -> Prefabs/Environment/P_{name}) and the real vehicle length in meters.</summary>
        private static readonly (string name, float realLength)[] Props =
        {
            ("Wreck_Mi24", 17.5f),
            ("Wreck_Mi17", 18.3f),
            ("Wreck_BTR80", 7.65f),
            ("Wreck_BTR60", 7.56f),
            ("Wreck_T72", 9.5f),     // with the gun
            ("Vehicle_BTR80", 7.65f),
            ("Vehicle_Mi24", 17.5f),
        };

        public static string ModelPath(string name) => ModelDir + name + ".fbx";

        [MenuItem("MobileMarket/Environment/Build Wreck Props")]
        public static void BuildAll()
        {
            foreach (var (name, realLength) in Props)
            {
                string modelPath = ModelPath(name);
                if (AssetImporter.GetAtPath(modelPath) == null) { Debug.LogWarning("[WreckBuilder] Missing " + modelPath); continue; }

                VarcoCharacterTools.SetupStaticModel(modelPath, name, readable: true);
                Build(name, AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), realLength * SizeFactor * WorldScale);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Build(string name, GameObject model, float length)
        {
            var root = new GameObject("P_" + name);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);

            // Longest horizontal side becomes the vehicle length; centered on the root with its base at y = 0.
            var bounds = WorldBounds(body);
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest > 0f) body.transform.localScale *= length / longest;
            bounds = WorldBounds(body);
            body.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = WorldBounds(body);

            foreach (var filter in body.GetComponentsInChildren<MeshFilter>())
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = bounds.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "P_" + name + ".prefab");
            Object.DestroyImmediate(root);
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
