using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Turns the VARCO settlement exports (Models/Settlement/Settle_Wall_* and Settle_Shanty_*) into map props
    /// (Prefabs/Environment/P_{name}): front turned to -Z, base on the ground, a BoxCollider so players, enemies and bullets are
    /// blocked, and a carving NavMeshObstacle so enemy AI walks around it.
    /// Walls are stretched to the perimeter module the settlement layout uses (6 x 3.5 units, long side along X) so the
    /// variants can be mixed in a row; shanties keep their proportions at real size.
    /// </summary>
    public static class SettlementPropBuilder
    {
        private const string ModelDir = "Assets/_Project/Models/Settlement/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/";

        // Real-world meters to world units (the 2.21-unit player stands for 1.75 m).
        private const float WorldScale = 2.21f / 1.75f;

        // Perimeter wall module (same as SettlementBuilder's VARCO wall segments).
        private const float WallLength = 6f;
        private const float WallHeight = 3.5f;
        private const float MinWallColliderDepth = 0.8f;

        /// <summary>Wall model name and the yaw that turns its front to -Z.</summary>
        private static readonly (string name, float yaw)[] Walls =
        {
            ("Settle_Wall_Wood", 0f),
            ("Settle_Wall_Scrap", 0f),
            ("Settle_Wall_Barricade", 0f),
        };

        /// <summary>Shanty model name, real length of its longest horizontal side (m), and the yaw that turns its door to -Z.</summary>
        private static readonly (string name, float length, float yaw)[] Shanties =
        {
            ("Settle_Shanty_Small", 4f, 90f),
            ("Settle_Shanty_TwoStory", 5f, 180f),
            ("Settle_Shanty_Awning", 6f, 90f),
            ("Settle_Shanty_Container", 6f, 0f),
        };

        [MenuItem("MobileMarket/Environment/Build Settlement Props")]
        public static void BuildAll()
        {
            foreach (var (name, yaw) in Walls)
            {
                var model = LoadModel(name);
                if (model == null) continue;
                var root = new GameObject("P_" + name);
                // Stretched through an unrotated holder: the imported model's own rotation would otherwise swap the axes.
                var holder = new GameObject(name).transform;
                holder.SetParent(root.transform, false);
                var model3d = Place(model, holder, yaw);
                var bounds = WorldBounds(model3d);
                holder.localScale = new Vector3(WallLength / bounds.size.x, WallHeight / bounds.size.y, WallLength / bounds.size.x);
                Finish(root, holder.gameObject, MinWallColliderDepth);
            }

            foreach (var (name, length, yaw) in Shanties)
            {
                var model = LoadModel(name);
                if (model == null) continue;
                var root = new GameObject("P_" + name);
                var body = Place(model, root.transform, yaw);
                var bounds = WorldBounds(body);
                float longest = Mathf.Max(bounds.size.x, bounds.size.z);
                if (longest > 0f) body.transform.localScale *= length * WorldScale / longest;
                Finish(root, body, 0f);
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject LoadModel(string name)
        {
            string path = ModelDir + name + ".fbx";
            if (AssetImporter.GetAtPath(path) == null) { Debug.LogWarning("[SettlementPropBuilder] Missing " + path); return null; }
            VarcoCharacterTools.SetupStaticModel(path, name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject Place(GameObject model, Transform parent, float yaw)
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * body.transform.localRotation;
            return body;
        }

        /// <summary>Centers the body on the root with its base at y = 0, adds the collider / obstacle and saves the prefab.</summary>
        private static void Finish(GameObject root, GameObject body, float minDepth)
        {
            var bounds = WorldBounds(body);
            body.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = WorldBounds(body);
            var size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(bounds.size.z, minDepth));

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = size;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + root.name + ".prefab");
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
