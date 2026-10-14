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
    /// variants can be mixed in a row; shanties keep their proportions at real size; the player-settlement landmarks are
    /// sized to the footprints PlayerSettlementBuilder lays out and saved to Prefabs/PlayerSettlement instead.
    /// </summary>
    public static class SettlementPropBuilder
    {
        private const string ModelDir = "Assets/_Project/Models/Settlement/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/";
        private const string PlayerSettlementPrefabDir = "Assets/_Project/Prefabs/PlayerSettlement/";

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

        /// <summary>
        /// Player-settlement landmark: model name, target size in world units (longest horizontal side, or height when byHeight),
        /// the yaw that turns its front to -Z, and for gate-like models the share of the length each end pillar takes
        /// (0 = one box collider; otherwise only the two pillars block, so the opening stays walkable).
        /// </summary>
        private static readonly (string name, float size, bool byHeight, float yaw, float pillar)[] Landmarks =
        {
            ("Settle_ContainerGate", 24f, false, 0f, 0.2f),
            ("Settle_Windmill_Big", 14f, true, 180f, 0f),
            ("Settle_Windmill_Small", 8f, true, 0f, 0f),
            ("Settle_Conveyor", 18f, false, 0f, 0f),
            ("Settle_Greenhouse", 12f, false, 0f, 0f),
            ("Settle_FarmWaterTower", 7f, true, 0f, 0f),
            ("Settle_Tractor", 4.4f, false, 180f, 0f),
            ("Settle_PlaneWreck", 24f, false, 0f, 0f),
            ("Settle_MainBuilding", 30f, false, 0f, 0f),
            ("Settle_PlayerHouse", 14f / 0.7071f, false, 180f, 0f), // 14 units wide once PlayerSettlementBuilder's PropScale applies
        };

        /// <summary>
        /// Player-settlement work stations at real size: model name, real size in meters (longest horizontal side, or height
        /// when byHeight) and the yaw that turns the working side to -Z. Saved to Prefabs/PlayerSettlement.
        /// </summary>
        private static readonly (string name, float meters, bool byHeight, float yaw)[] Stations =
        {
            ("Settle_Station_Canner", 2f, false, 180f),
            ("Settle_Station_Workbench", 2f, false, 180f),
            ("Settle_Station_Furnace", 2.5f, true, 180f),
        };

        // Wall patch module (world units).
        private const float PatchWidth = 3f;
        private const float PatchHeight = 2.5f;
        private const float PatchDepth = 0.25f;

        /// <summary>Wall patch model name and the yaw that turns its front face to -Z.</summary>
        private static readonly (string name, float yaw)[] Patches =
        {
            ("Settle_Patch_Concrete", 0f),
            ("Settle_Patch_Planks", 0f),
            ("Settle_Patch_Corrugated", 0f),
            ("Settle_Patch_Blocks", 0f),
            ("Settle_Patch_ReinforcedWood", 180f),
            ("Settle_Patch_ReinforcedMetal", 0f),
        };

        // Roof patch module (world units): lies flat, RoofPatchSize square.
        private const float RoofPatchSize = 3f;

        /// <summary>Roof patch model name and a yaw to line its main direction (planks, sheets) up with X.</summary>
        private static readonly (string name, float yaw)[] RoofPatches =
        {
            ("Settle_RoofPatch_Concrete", 0f),
            ("Settle_RoofPatch_Planks", 0f),
            ("Settle_RoofPatch_Corrugated", 0f),
            ("Settle_RoofPatch_Metal", 0f),
            ("Settle_RoofPatch_Tarp", 0f),
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

            BuildLandmarks();
            BuildPatches();
            BuildRoofPatches();
            BuildStations();
        }

        [MenuItem("MobileMarket/Environment/Build Work Stations")]
        public static void BuildStations()
        {
            foreach (var (name, meters, byHeight, yaw) in Stations)
            {
                var model = LoadModel(name);
                if (model == null) continue;
                var root = new GameObject("P_" + name);
                var body = Place(model, root.transform, yaw);
                var bounds = WorldBounds(body);
                float current = byHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
                if (current > 0f) body.transform.localScale *= meters * WorldScale / current;
                Finish(root, body, 0f, true, PlayerSettlementPrefabDir);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Roof patches that cover holes in VARCO building roofs: stretched (through an unrotated holder) to a RoofPatchSize
        /// square, top up, pivot at the bottom center. Scale the instance's root on X / Z to fit a hole. A thin collider only.
        /// </summary>
        [MenuItem("MobileMarket/Environment/Build Roof Patches")]
        public static void BuildRoofPatches()
        {
            foreach (var (name, yaw) in RoofPatches)
            {
                var model = LoadModel(name);
                if (model == null) continue;
                var root = new GameObject("P_" + name);
                var holder = new GameObject(name).transform;
                holder.SetParent(root.transform, false);
                var model3d = Place(model, holder, yaw);
                var bounds = WorldBounds(model3d);
                // Height keeps the model's own proportion so weights resting on top (bricks, sandbags, tires) aren't flattened.
                float s = RoofPatchSize / Mathf.Max(bounds.size.x, bounds.size.z);
                holder.localScale = new Vector3(RoofPatchSize / bounds.size.x, s, RoofPatchSize / bounds.size.z);
                Finish(root, holder.gameObject, 0f, false);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Wall patches that cover holes in VARCO buildings: stretched (through an unrotated holder) to a PatchWidth x
        /// PatchHeight x PatchDepth module, front to -Z, pivot at the bottom center. Scale the instance's root non-uniformly to
        /// fit a hole. A thin collider only; the building they sit on already carves the NavMesh.
        /// </summary>
        [MenuItem("MobileMarket/Environment/Build Wall Patches")]
        public static void BuildPatches()
        {
            foreach (var (name, yaw) in Patches)
            {
                var model = LoadModel(name);
                if (model == null) continue;
                var root = new GameObject("P_" + name);
                var holder = new GameObject(name).transform;
                holder.SetParent(root.transform, false);
                var model3d = Place(model, holder, yaw);
                var bounds = WorldBounds(model3d);
                holder.localScale = new Vector3(PatchWidth / bounds.size.x, PatchHeight / bounds.size.y, PatchDepth / bounds.size.z);
                Finish(root, holder.gameObject, 0f, false);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MobileMarket/Environment/Build Settlement Landmarks")]
        public static void BuildLandmarks()
        {
            foreach (var (name, size, byHeight, yaw, pillar) in Landmarks)
            {
                var model = LoadModel(name);
                if (model == null) continue;
                var root = new GameObject("P_" + name);
                var body = Place(model, root.transform, yaw);
                var bounds = WorldBounds(body);
                float current = byHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
                if (current > 0f) body.transform.localScale *= size / current;
                if (pillar > 0f) FinishGate(root, body, pillar, PlayerSettlementPrefabDir);
                else Finish(root, body, 0f, true, PlayerSettlementPrefabDir);
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
        private static void Finish(GameObject root, GameObject body, float minDepth, bool navObstacle = true, string dir = PrefabDir)
        {
            var bounds = WorldBounds(body);
            body.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = WorldBounds(body);
            var size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(bounds.size.z, minDepth));

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = size;

            if (!navObstacle)
            {
                root.isStatic = true;
                PrefabUtility.SaveAsPrefabAsset(root, dir + root.name + ".prefab");
                Object.DestroyImmediate(root);
                return;
            }

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, dir + root.name + ".prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>Like Finish, but only the two end pillars (each `pillar` of the length along X) get a collider and obstacle.</summary>
        private static void FinishGate(GameObject root, GameObject body, float pillar, string dir)
        {
            var bounds = WorldBounds(body);
            body.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = WorldBounds(body);
            float w = bounds.size.x * pillar;
            foreach (float side in new[] { -1f, 1f })
            {
                var p = new GameObject(side < 0f ? "PillarW" : "PillarE");
                p.transform.SetParent(root.transform, false);
                var center = new Vector3(side * (bounds.extents.x - w * 0.5f), bounds.center.y, bounds.center.z);
                var size = new Vector3(w, bounds.size.y, bounds.size.z);
                var collider = p.AddComponent<BoxCollider>();
                collider.center = center;
                collider.size = size;
                var obstacle = p.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = center;
                obstacle.size = size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                p.isStatic = true;
            }
            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, dir + root.name + ".prefab");
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
