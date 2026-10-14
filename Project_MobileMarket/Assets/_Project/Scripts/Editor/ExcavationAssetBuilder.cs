using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using MobileMarket.Environment;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Turns the Excavation site VARCO exports (Models/Excavation/EX_*) into prefabs (Prefabs/Excavation/P_EX_*):
    /// props at real size (front to -Z, base on the ground, a BoxCollider, a carving NavMeshObstacle for the big ones),
    /// building-kit modules stretched to exact sizes, and the enterable buildings assembled from the kit: floor tiles, walls
    /// with real door openings (cut from wall pieces), windows, interior props and a roof that RoofHider hides while the
    /// player is inside.
    /// </summary>
    public static class ExcavationAssetBuilder
    {
        public const string ModelDir = "Assets/_Project/Models/Excavation/";
        public const string PrefabDir = "Assets/_Project/Prefabs/Excavation/";

        // Real-world meters to world units (the 2.21-unit player stands for 1.75 m).
        public const float WorldScale = 2.21f / 1.75f;

        // Building kit module, in world units.
        public const float Module = 4f * WorldScale;          // wall length / floor & roof tile size
        public const float WallHeight = 3.2f * WorldScale;
        public const float WallThickness = 0.25f * WorldScale;
        public const float DoorWidth = 1.6f * WorldScale;
        public const float DoorHeight = 2.4f * WorldScale;
        public const float FloorThickness = 0.15f * WorldScale;

        /// <summary>Prop: model name, real size in meters (longest horizontal side, or height when byHeight), yaw turning its front to -Z, obstacle.</summary>
        private static readonly (string name, float meters, bool byHeight, float yaw, bool obstacle)[] Props =
        {
            ("EX_Mil_Bunker", 30f, false, 180f, true),
            ("EX_Mil_Watchtower", 9f, true, 0f, true),
            ("EX_Mil_Gate", 12f, false, 0f, false),
            ("EX_Mil_Sandbags", 4f, false, 0f, true),
            ("EX_Mil_Floodlight", 7f, true, 180f, true),
            ("EX_Mil_RadioMast", 12f, true, 0f, true),
            ("EX_Mil_CamoNet", 6f, false, 0f, true),
            ("EX_Mil_Generator", 3f, false, 180f, true),
            ("EX_Mil_AmmoCrates", 2f, false, 0f, true),
            ("EX_Mil_FuelDrums", 1.4f, false, 0f, true),
            ("EX_Mil_BunkBed", 2f, false, 180f, false),
            ("EX_Mil_Lockers", 1.8f, true, 180f, false),
            ("EX_Mil_RadioDesk", 1.8f, false, 180f, false),
            ("EX_Mil_WeaponRack", 2f, false, 180f, false),
            ("EX_Mil_MessTable", 2.2f, false, 0f, false),
            ("EX_Mil_Shelf", 2f, true, 0f, false),
            ("EX_Mil_FuelTank", 6f, true, 90f, true),
            ("EX_Mil_TruckWreck", 8f, false, 90f, true),
        };

        /// <summary>Stretched modules: model name, exact size (x, y, z) in world units, yaw turning its face to -Z (walls) / leaving it flat.</summary>
        private static readonly (string name, Vector3 size, float yaw)[] Modules =
        {
            ("EX_Mil_Fence", new Vector3(6f * WorldScale, 3f * WorldScale, 0.4f * WorldScale), 0f),
            ("EX_Mil_Barrier", new Vector3(6f * WorldScale, 2.4f * WorldScale, 1.2f * WorldScale), 0f),
            ("EX_Mil_Wall", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Mil_WallWindow", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Mil_Floor", new Vector3(Module, FloorThickness, Module), 0f),
            ("EX_Mil_Roof", new Vector3(Module, 0.2f * WorldScale, Module), 0f),
        };

        /// <summary>Ruin excavation and researchers' camp props (same layout as Props).</summary>
        private static readonly (string name, float meters, bool byHeight, float yaw, bool obstacle)[] ExcavationProps =
        {
            ("EX_Exc_CaveEntrance", 34f, false, 180f, true),
            ("EX_Exc_Scaffolding", 12f, false, 90f, true),
            ("EX_Exc_Crane", 14f, true, 90f, true),
            ("EX_Exc_RuinWall", 10f, false, 180f, true),
            ("EX_Exc_Statue", 6f, false, 180f, true),
            ("EX_Exc_Sifting", 4f, false, 0f, true),
            ("EX_Exc_ArtifactCrates", 2.5f, false, 0f, true),
            ("EX_Exc_WorkLight", 3f, true, 90f, true),
            ("EX_Camp_Container", 6f, false, 90f, true),
            ("EX_Camp_WaterTank", 3f, true, 180f, true),
            ("EX_Camp_Campfire", 5f, false, 0f, true),
            ("EX_Camp_LabTable", 2f, false, 180f, false),
            ("EX_Camp_Cot", 2f, false, 90f, false),
            ("EX_Camp_Desk", 1.8f, false, 180f, false),
            ("EX_Camp_Shelf", 2f, true, 0f, false),
            ("EX_Camp_Cabinet", 1.4f, true, 180f, false),
        };

        /// <summary>Open-sided shelters the player walks into: no solid collider, the whole canopy hides while inside.</summary>
        private static readonly (string name, float meters, float yaw)[] ExcavationShelters =
        {
            ("EX_Camp_Tent", 8f, 0f),
        };

        private static readonly (string name, Vector3 size, float yaw)[] ExcavationModules =
        {
            ("EX_Camp_Wall", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Camp_WallWindow", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Camp_Floor", new Vector3(Module, FloorThickness, Module), 0f),
            ("EX_Camp_Roof", new Vector3(Module, 0.2f * WorldScale, Module), 0f),
        };

        /// <summary>Laboratory complex props (same layout as Props).</summary>
        private static readonly (string name, float meters, bool byHeight, float yaw, bool obstacle)[] LabProps =
        {
            ("EX_Lab_MainLab", 40f, false, 180f, true),
            ("EX_Lab_RadarDome", 12f, true, 180f, true),
            ("EX_Lab_SatDish", 5f, false, 180f, true),
            ("EX_Lab_Checkpoint", 12f, false, 0f, false),
            ("EX_Lab_Transformer", 4f, false, 0f, true),
            ("EX_Lab_LampPost", 6f, true, 0f, true),
            ("EX_Lab_HazardDrums", 1.6f, false, 0f, true),
            ("EX_Lab_Bench", 2.4f, false, 180f, false),
            ("EX_Lab_SpecimenTank", 2.2f, true, 180f, false),
            ("EX_Lab_ComputerDesk", 1.8f, false, 180f, false),
            ("EX_Lab_ServerRack", 2f, true, 180f, false),
            ("EX_Lab_Bed", 2f, false, 90f, false),
            ("EX_Lab_Locker", 1.8f, true, 180f, false),
            ("EX_Lab_StorageRack", 3f, false, 0f, false),
            ("EX_Lab_PlantBed", 3f, false, 90f, false),
        };

        private static readonly (string name, Vector3 size, float yaw)[] LabModules =
        {
            ("EX_Lab_Fence", new Vector3(6f * WorldScale, 3f * WorldScale, 0.4f * WorldScale), 0f),
            ("EX_Lab_Wall", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Lab_WallWindow", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Lab_Floor", new Vector3(Module, FloorThickness, Module), 0f),
            ("EX_Lab_Roof", new Vector3(Module, 0.2f * WorldScale, Module), 0f),
            ("EX_Lab_GlassWall", new Vector3(Module, WallHeight, WallThickness), 0f),
            ("EX_Lab_GlassRoof", new Vector3(Module, 0.2f * WorldScale, Module), 0f),
            // Flat enough (like a floor slab) to walk straight onto.
            ("EX_Lab_Helipad", new Vector3(18f * WorldScale, FloorThickness, 18f * WorldScale), 0f),
        };

        /// <summary>Open-ground scatter and the river bridges (same layout as Props).</summary>
        private static readonly (string name, float meters, bool byHeight, float yaw, bool obstacle)[] EnvProps =
        {
            ("EX_Env_Bridge", 40f, false, 90f, false),
            ("EX_Env_Boulder", 4f, false, 0f, true),
            ("EX_Env_RockCluster", 4f, false, 0f, true),
            ("EX_Env_RockSlab", 5f, false, 0f, true),
            ("EX_Env_DeadTree", 5f, true, 0f, true),
            ("EX_Env_Bush", 1.5f, false, 0f, false),
            ("EX_Env_TruckWreck", 5f, false, 90f, true),
            ("EX_Env_JunkPile", 3f, false, 0f, true),
            ("EX_Env_Pole", 8f, true, 0f, true),
            ("EX_Env_CaveOverhang", 30f, false, 0f, false),
        };

        /// <summary>Drive-through gates and walk-through plants: no blocking collider.</summary>
        private static readonly HashSet<string> Passable = new HashSet<string> { "EX_Mil_Gate", "EX_Lab_Checkpoint", "EX_Env_Bush" };

        /// <summary>Walked on, not around: a mesh collider follows the model (the bridge deck).</summary>
        private static readonly HashSet<string> MeshColliders = new HashSet<string> { "EX_Env_Bridge" };

        /// <summary>Only the trunk blocks; the branches or wires above are free.</summary>
        private static readonly HashSet<string> TrunkColliders = new HashSet<string> { "EX_Env_DeadTree", "EX_Env_Pole" };

        [MenuItem("MobileMarket/Excavation Site/Build Military Prefabs")]
        public static void BuildMilitary()
        {
            foreach (var (name, meters, byHeight, yaw, obstacle) in Props) BuildProp(name, meters, byHeight, yaw, obstacle);
            BuildShelter("EX_Mil_Tent", 8f, 0f);
            foreach (var (name, size, yaw) in Modules) BuildModule(name, size, yaw);
            AssetDatabase.SaveAssets();
            foreach (var spec in MilitaryBuildings) BuildBuilding(spec);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MobileMarket/Excavation Site/Build Laboratory Prefabs")]
        public static void BuildLab()
        {
            foreach (var (name, meters, byHeight, yaw, obstacle) in LabProps) BuildProp(name, meters, byHeight, yaw, obstacle);
            foreach (var (name, size, yaw) in LabModules) BuildModule(name, size, yaw);
            AssetDatabase.SaveAssets();
            foreach (var spec in LabBuildings) BuildBuilding(spec);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MobileMarket/Excavation Site/Build Environment Prefabs")]
        public static void BuildEnvironment()
        {
            foreach (var (name, meters, byHeight, yaw, obstacle) in EnvProps) BuildProp(name, meters, byHeight, yaw, obstacle);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MobileMarket/Excavation Site/Build Excavation Prefabs")]
        public static void BuildExcavation()
        {
            foreach (var (name, meters, byHeight, yaw, obstacle) in ExcavationProps) BuildProp(name, meters, byHeight, yaw, obstacle);
            foreach (var (name, meters, yaw) in ExcavationShelters) BuildShelter(name, meters, yaw);
            foreach (var (name, size, yaw) in ExcavationModules) BuildModule(name, size, yaw);
            AssetDatabase.SaveAssets();
            foreach (var spec in CampBuildings) BuildBuilding(spec);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- props & modules

        private static GameObject LoadModel(string name)
        {
            string path = ModelDir + name + ".fbx";
            if (AssetImporter.GetAtPath(path) == null) { Debug.LogWarning("[ExcavationAssetBuilder] Missing " + path); return null; }
            VarcoCharacterTools.SetupStaticModel(path, name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void BuildProp(string name, float meters, bool byHeight, float yaw, bool obstacle)
        {
            var model = LoadModel(name);
            if (model == null) return;
            var root = new GameObject("P_" + name);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * body.transform.localRotation;
            var b = Bounds(body);
            float current = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (current > 0f) body.transform.localScale *= meters * WorldScale / current;
            Ground(body);
            if (MeshColliders.Contains(name))
            {
                foreach (var mf in body.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            else if (TrunkColliders.Contains(name))
            {
                var tb = Bounds(body);
                float trunk = Mathf.Min(tb.size.x, tb.size.z) * 0.15f;
                AddCollider(root, new Bounds(new Vector3(0f, tb.size.y * 0.5f, 0f), new Vector3(trunk, tb.size.y, trunk)), obstacle);
            }
            else if (!Passable.Contains(name)) AddCollider(root, Bounds(body), obstacle);
            Save(root);
        }

        /// <summary>Stretched (through an unrotated holder, so the FBX's own rotation can't swap axes) to an exact size, centred, base at y = 0.</summary>
        private static void BuildModule(string name, Vector3 size, float yaw)
        {
            var model = LoadModel(name);
            if (model == null) return;
            var root = new GameObject("P_" + name);
            var holder = new GameObject(name).transform;
            holder.SetParent(root.transform, false);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * body.transform.localRotation;
            var b = Bounds(body);
            holder.localScale = new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z);
            Ground(holder.gameObject);
            AddCollider(root, Bounds(holder.gameObject), false);
            Save(root);
        }

        private static void BuildShelter(string name, float meters, float yaw)
        {
            var model = LoadModel(name);
            if (model == null) return;
            var root = new GameObject("P_" + name);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * body.transform.localRotation;
            var b = Bounds(body);
            float current = Mathf.Max(b.size.x, b.size.z);
            if (current > 0f) body.transform.localScale *= meters * WorldScale / current;
            Ground(body);

            // The area under the canopy is the trigger; RoofHider hides the canopy while the player stands in it.
            b = Bounds(body);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = b.center;
            trigger.size = new Vector3(b.size.x * 0.85f, b.size.y, b.size.z * 0.85f);
            var hider = root.AddComponent<RoofHider>();
            var so = new SerializedObject(hider);
            var list = so.FindProperty("roofRenderers");
            var renderers = body.GetComponentsInChildren<Renderer>();
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root);
        }

        private static void Ground(GameObject body)
        {
            var b = Bounds(body);
            body.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static void AddCollider(GameObject root, Bounds b, bool obstacle)
        {
            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            if (!obstacle) return;
            var nav = root.AddComponent<NavMeshObstacle>();
            nav.shape = NavMeshObstacleShape.Box;
            nav.center = b.center;
            nav.size = b.size;
            nav.carving = true;
            nav.carveOnlyStationary = true;
        }

        private static void Save(GameObject root)
        {
            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + root.name + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + name + ".prefab");

        // ---------------------------------------------------------------- enterable buildings

        public enum Side { South, North, West, East }

        /// <summary>
        /// A kit building: cols x rows modules, door on one side at a module index (doorWidth in modules: 1 = a person door,
        /// 2+ = a wide vehicle opening), windows as (side, index), and interior props as (prefab, position in module units from
        /// the building's south-west inner corner, yaw).
        /// </summary>
        public class BuildingSpec
        {
            public string name;
            public string wall, window, floor, roof;
            public int cols, rows;
            public Side doorSide;
            public int doorIndex;
            public int doorModules = 1;
            public (Side side, int index)[] windows = new (Side, int)[0];
            public (string prefab, Vector2 pos, float yaw)[] props = new (string, Vector2, float)[0];
        }

        private static readonly BuildingSpec[] MilitaryBuildings =
        {
            new BuildingSpec
            {
                name = "P_EX_Mil_Barracks", wall = "P_EX_Mil_Wall", window = "P_EX_Mil_WallWindow", floor = "P_EX_Mil_Floor", roof = "P_EX_Mil_Roof",
                cols = 3, rows = 2, doorSide = Side.South, doorIndex = 1,
                windows = new[] { (Side.South, 0), (Side.South, 2), (Side.North, 0), (Side.North, 2), (Side.East, 0), (Side.West, 1) },
                // Yaw: 0 = front to the south (against a north wall), 180 = to the north, 90 = to the west, 270 = to the east.
                props = new[]
                {
                    ("P_EX_Mil_BunkBed", new Vector2(0.3f, 1.65f), 90f), ("P_EX_Mil_BunkBed", new Vector2(0.85f, 1.65f), 90f),
                    ("P_EX_Mil_BunkBed", new Vector2(1.4f, 1.65f), 90f), ("P_EX_Mil_BunkBed", new Vector2(1.95f, 1.65f), 90f),
                    ("P_EX_Mil_Lockers", new Vector2(2.88f, 1.6f), 90f), ("P_EX_Mil_Lockers", new Vector2(2.88f, 1.2f), 90f),
                    ("P_EX_Mil_MessTable", new Vector2(2.2f, 0.7f), 0f), ("P_EX_Mil_Shelf", new Vector2(0.12f, 0.6f), 270f),
                    ("P_EX_Mil_WeaponRack", new Vector2(0.6f, 0.15f), 180f), ("P_EX_Mil_AmmoCrates", new Vector2(2.7f, 0.2f), 0f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Mil_CommandPost", wall = "P_EX_Mil_Wall", window = "P_EX_Mil_WallWindow", floor = "P_EX_Mil_Floor", roof = "P_EX_Mil_Roof",
                cols = 3, rows = 2, doorSide = Side.East, doorIndex = 0,
                windows = new[] { (Side.South, 0), (Side.South, 1), (Side.South, 2), (Side.North, 1), (Side.West, 0) },
                props = new[]
                {
                    ("P_EX_Mil_RadioDesk", new Vector2(0.55f, 1.8f), 0f), ("P_EX_Mil_RadioDesk", new Vector2(1.45f, 1.8f), 0f),
                    ("P_EX_Mil_WeaponRack", new Vector2(2.35f, 1.85f), 0f), ("P_EX_Mil_Lockers", new Vector2(2.88f, 1.4f), 90f),
                    ("P_EX_Mil_MessTable", new Vector2(1.3f, 0.75f), 0f), ("P_EX_Mil_Shelf", new Vector2(0.12f, 0.6f), 270f),
                    ("P_EX_Mil_AmmoCrates", new Vector2(2.0f, 0.2f), 0f), ("P_EX_Mil_FuelDrums", new Vector2(0.3f, 0.2f), 0f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Mil_Garage", wall = "P_EX_Mil_Wall", window = "P_EX_Mil_WallWindow", floor = "P_EX_Mil_Floor", roof = "P_EX_Mil_Roof",
                cols = 4, rows = 3, doorSide = Side.South, doorIndex = 1, doorModules = 2,
                windows = new[] { (Side.North, 1), (Side.North, 2), (Side.West, 1) },
                props = new[]
                {
                    ("P_EX_Mil_Shelf", new Vector2(0.12f, 2.2f), 270f), ("P_EX_Mil_Shelf", new Vector2(0.12f, 1.4f), 270f),
                    ("P_EX_Mil_FuelDrums", new Vector2(3.75f, 2.75f), 0f), ("P_EX_Mil_FuelDrums", new Vector2(3.4f, 2.75f), 0f),
                    ("P_EX_Mil_Generator", new Vector2(3.6f, 0.6f), 90f), ("P_EX_Mil_Lockers", new Vector2(3.88f, 1.6f), 90f),
                    ("P_EX_Mil_AmmoCrates", new Vector2(2.4f, 2.75f), 0f), ("P_EX_Mil_AmmoCrates", new Vector2(1.9f, 2.75f), 0f),
                    ("P_EX_Mil_WeaponRack", new Vector2(1.0f, 2.85f), 0f),
                },
            },
        };

        private static readonly BuildingSpec[] CampBuildings =
        {
            new BuildingSpec
            {
                name = "P_EX_Camp_Dorm", wall = "P_EX_Camp_Wall", window = "P_EX_Camp_WallWindow", floor = "P_EX_Camp_Floor", roof = "P_EX_Camp_Roof",
                cols = 3, rows = 2, doorSide = Side.South, doorIndex = 1,
                windows = new[] { (Side.South, 0), (Side.South, 2), (Side.North, 0), (Side.North, 2), (Side.East, 0), (Side.West, 1) },
                // Yaw: 0 = front to the south (against a north wall), 180 = to the north, 90 = to the west, 270 = to the east.
                props = new[]
                {
                    ("P_EX_Camp_Cot", new Vector2(0.3f, 1.6f), 90f), ("P_EX_Camp_Cot", new Vector2(0.8f, 1.6f), 90f),
                    ("P_EX_Camp_Cot", new Vector2(1.3f, 1.6f), 90f), ("P_EX_Camp_Cot", new Vector2(1.8f, 1.6f), 90f),
                    ("P_EX_Camp_Cabinet", new Vector2(2.88f, 1.5f), 90f), ("P_EX_Camp_Shelf", new Vector2(0.12f, 0.6f), 270f),
                    ("P_EX_Camp_Desk", new Vector2(2.4f, 0.2f), 180f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Camp_Lab", wall = "P_EX_Camp_Wall", window = "P_EX_Camp_WallWindow", floor = "P_EX_Camp_Floor", roof = "P_EX_Camp_Roof",
                cols = 3, rows = 2, doorSide = Side.East, doorIndex = 0,
                windows = new[] { (Side.South, 0), (Side.South, 1), (Side.South, 2), (Side.North, 0), (Side.North, 2), (Side.West, 1) },
                props = new[]
                {
                    ("P_EX_Camp_LabTable", new Vector2(0.6f, 1.8f), 0f), ("P_EX_Camp_LabTable", new Vector2(1.5f, 1.8f), 0f),
                    ("P_EX_Camp_Shelf", new Vector2(2.4f, 1.88f), 0f), ("P_EX_Camp_Shelf", new Vector2(0.12f, 0.9f), 270f),
                    ("P_EX_Camp_Cabinet", new Vector2(0.12f, 0.3f), 270f), ("P_EX_Exc_ArtifactCrates", new Vector2(1.3f, 0.3f), 0f),
                    ("P_EX_Camp_Desk", new Vector2(2.3f, 0.2f), 180f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Camp_Office", wall = "P_EX_Camp_Wall", window = "P_EX_Camp_WallWindow", floor = "P_EX_Camp_Floor", roof = "P_EX_Camp_Roof",
                cols = 2, rows = 2, doorSide = Side.South, doorIndex = 1,
                windows = new[] { (Side.South, 0), (Side.North, 1), (Side.West, 1), (Side.East, 0) },
                props = new[]
                {
                    ("P_EX_Camp_Desk", new Vector2(0.6f, 1.8f), 0f), ("P_EX_Camp_Cabinet", new Vector2(1.6f, 1.88f), 0f),
                    ("P_EX_Camp_Shelf", new Vector2(1.88f, 0.8f), 90f), ("P_EX_Exc_ArtifactCrates", new Vector2(0.4f, 0.3f), 0f),
                },
            },
        };

        private static readonly BuildingSpec[] LabBuildings =
        {
            new BuildingSpec
            {
                name = "P_EX_Lab_AuxLab", wall = "P_EX_Lab_Wall", window = "P_EX_Lab_WallWindow", floor = "P_EX_Lab_Floor", roof = "P_EX_Lab_Roof",
                cols = 3, rows = 2, doorSide = Side.South, doorIndex = 1,
                windows = new[] { (Side.South, 0), (Side.South, 2), (Side.North, 0), (Side.North, 1), (Side.East, 0), (Side.West, 1) },
                // Yaw: 0 = front to the south (against a north wall), 180 = to the north, 90 = to the west, 270 = to the east.
                props = new[]
                {
                    ("P_EX_Lab_Bench", new Vector2(0.6f, 1.75f), 0f), ("P_EX_Lab_Bench", new Vector2(1.5f, 1.75f), 0f),
                    ("P_EX_Lab_SpecimenTank", new Vector2(2.6f, 1.7f), 0f), ("P_EX_Lab_SpecimenTank", new Vector2(2.75f, 1.1f), 90f),
                    ("P_EX_Lab_ComputerDesk", new Vector2(0.5f, 0.25f), 180f), ("P_EX_Lab_ServerRack", new Vector2(0.12f, 1.0f), 270f),
                    ("P_EX_Lab_HazardDrums", new Vector2(2.6f, 0.3f), 0f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Lab_Dormitory", wall = "P_EX_Lab_Wall", window = "P_EX_Lab_WallWindow", floor = "P_EX_Lab_Floor", roof = "P_EX_Lab_Roof",
                cols = 3, rows = 2, doorSide = Side.South, doorIndex = 1,
                windows = new[] { (Side.South, 0), (Side.South, 2), (Side.North, 0), (Side.North, 2), (Side.East, 0), (Side.West, 1) },
                props = new[]
                {
                    ("P_EX_Lab_Bed", new Vector2(0.3f, 1.6f), 90f), ("P_EX_Lab_Bed", new Vector2(0.85f, 1.6f), 90f),
                    ("P_EX_Lab_Bed", new Vector2(1.4f, 1.6f), 90f), ("P_EX_Lab_Bed", new Vector2(1.95f, 1.6f), 90f),
                    ("P_EX_Lab_Locker", new Vector2(2.88f, 1.5f), 90f), ("P_EX_Lab_Locker", new Vector2(2.88f, 0.9f), 90f),
                    ("P_EX_Lab_ComputerDesk", new Vector2(2.3f, 0.2f), 180f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Lab_Warehouse", wall = "P_EX_Lab_Wall", window = "P_EX_Lab_WallWindow", floor = "P_EX_Lab_Floor", roof = "P_EX_Lab_Roof",
                cols = 4, rows = 3, doorSide = Side.South, doorIndex = 1, doorModules = 2,
                windows = new[] { (Side.North, 1), (Side.North, 2), (Side.East, 1) },
                props = new[]
                {
                    ("P_EX_Lab_StorageRack", new Vector2(0.6f, 2.8f), 0f), ("P_EX_Lab_StorageRack", new Vector2(1.6f, 2.8f), 0f),
                    ("P_EX_Lab_StorageRack", new Vector2(2.6f, 2.8f), 0f), ("P_EX_Lab_StorageRack", new Vector2(3.5f, 2.8f), 0f),
                    ("P_EX_Lab_StorageRack", new Vector2(0.15f, 1.5f), 270f),
                    ("P_EX_Lab_HazardDrums", new Vector2(3.6f, 0.5f), 0f), ("P_EX_Lab_HazardDrums", new Vector2(3.6f, 1.3f), 0f),
                    ("P_EX_Mil_AmmoCrates", new Vector2(0.6f, 0.5f), 0f),
                },
            },
            new BuildingSpec
            {
                name = "P_EX_Lab_Greenhouse", wall = "P_EX_Lab_GlassWall", window = "P_EX_Lab_GlassWall", floor = "P_EX_Lab_Floor", roof = "P_EX_Lab_GlassRoof",
                cols = 4, rows = 2, doorSide = Side.South, doorIndex = 1,
                props = new[]
                {
                    ("P_EX_Lab_PlantBed", new Vector2(0.8f, 1.5f), 0f), ("P_EX_Lab_PlantBed", new Vector2(2.0f, 1.5f), 0f),
                    ("P_EX_Lab_PlantBed", new Vector2(3.2f, 1.5f), 0f),
                    ("P_EX_Lab_PlantBed", new Vector2(0.6f, 0.5f), 0f), ("P_EX_Lab_PlantBed", new Vector2(3.2f, 0.5f), 0f),
                },
            },
        };

        public static void BuildBuilding(BuildingSpec spec)
        {
            var wall = LoadPrefab(spec.wall);
            var window = LoadPrefab(spec.window);
            var floor = LoadPrefab(spec.floor);
            var roof = LoadPrefab(spec.roof);
            if (wall == null || window == null || floor == null || roof == null) { Debug.LogWarning("[ExcavationAssetBuilder] Kit missing for " + spec.name); return; }

            float w = spec.cols * Module, d = spec.rows * Module;
            var root = new GameObject(spec.name);
            var floors = Child(root.transform, "Floor");
            var walls = Child(root.transform, "Walls");
            var roofs = Child(root.transform, "Roof");
            var props = Child(root.transform, "Props");

            // One floor slab and one roof slab stretched over the footprint (tiles repeat the floor's drain grate and multiply
            // the polygon count). Building origin = centre of the footprint at ground level.
            Place(floor, floors, Vector3.zero, 0f, new Vector3(spec.cols, 1f, spec.rows));
            var r = Place(roof, roofs, Vector3.up * WallHeight, 0f, new Vector3(spec.cols, 1f, spec.rows));
            // The roof is only seen, never walked on or shot at.
            foreach (var col in r.GetComponentsInChildren<Collider>()) col.enabled = false;

            foreach (Side side in System.Enum.GetValues(typeof(Side)))
            {
                int count = side == Side.South || side == Side.North ? spec.cols : spec.rows;
                for (int k = 0; k < count; k++)
                {
                    bool door = side == spec.doorSide && k >= spec.doorIndex && k < spec.doorIndex + spec.doorModules;
                    if (door)
                    {
                        if (k == spec.doorIndex) DoorOpening(walls, wall, w, d, side, k, spec.doorModules);
                        continue;
                    }
                    bool isWindow = System.Array.Exists(spec.windows, x => x.side == side && x.index == k);
                    WallPiece(walls, isWindow ? window : wall, w, d, side, k, 0f, 1f, 0f, 1f);
                }
            }

            foreach (var (prefabName, pos, yaw) in spec.props)
            {
                var prefab = LoadPrefab(prefabName);
                if (prefab == null) continue;
                Place(prefab, props, new Vector3(-w * 0.5f + pos.x * Module, FloorThickness, -d * 0.5f + pos.y * Module), yaw, Vector3.one);
            }

            // Interior trigger: the inside volume, a little short of the walls; RoofHider hides the roof while the player is in it.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, WallHeight * 0.5f, 0f);
            trigger.size = new Vector3(w - WallThickness * 2f, WallHeight, d - WallThickness * 2f);
            var hider = root.AddComponent<RoofHider>();
            var so = new SerializedObject(hider);
            var list = so.FindProperty("roofRenderers");
            var renderers = roofs.GetComponentsInChildren<Renderer>();
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root);
        }

        /// <summary>
        /// One wall module (or a cut-down piece of it) on a side: u0..u1 is the horizontal span within the module (0..1, from
        /// the module's left as seen from outside), v0..v1 the vertical span (0..1 of the wall height).
        /// </summary>
        private static void WallPiece(Transform parent, GameObject prefab, float w, float d, Side side, int k, float u0, float u1, float v0, float v1)
        {
            // Outward normal and the "along" direction (left to right as seen from outside) per side.
            Vector3 outward, along, start;
            switch (side)
            {
                case Side.South: outward = Vector3.back; along = Vector3.right; start = new Vector3(-w * 0.5f, 0f, -d * 0.5f); break;
                case Side.North: outward = Vector3.forward; along = Vector3.left; start = new Vector3(w * 0.5f, 0f, d * 0.5f); break;
                case Side.West: outward = Vector3.left; along = Vector3.back; start = new Vector3(-w * 0.5f, 0f, d * 0.5f); break;
                default: outward = Vector3.right; along = Vector3.forward; start = new Vector3(w * 0.5f, 0f, -d * 0.5f); break;
            }
            float uMid = (u0 + u1) * 0.5f;
            var pos = start + along * ((k + uMid) * Module) + outward * (-WallThickness * 0.5f) + Vector3.up * (v0 * WallHeight);
            // The module's face (-Z) looks outward.
            var rot = Quaternion.LookRotation(-outward, Vector3.up);
            Place(prefab, parent, pos, rot.eulerAngles.y, new Vector3(u1 - u0, v1 - v0, 1f));
        }

        /// <summary>A door (or vehicle) opening: two jambs and a lintel cut from plain wall modules.</summary>
        private static void DoorOpening(Transform parent, GameObject wall, float w, float d, Side side, int k, int modules)
        {
            float span = modules;                                        // in module units
            float opening = modules == 1 ? DoorWidth / Module : modules - 0.5f;
            float jamb = (span - opening) * 0.5f;
            float lintel = modules == 1 ? DoorHeight / WallHeight : 0.82f;
            WallPiece(parent, wall, w, d, side, k, 0f, jamb, 0f, 1f);
            WallPiece(parent, wall, w, d, side, k, span - jamb, span, 0f, 1f);
            WallPiece(parent, wall, w, d, side, k, jamb, span - jamb, lintel, 1f);
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 localPos, float yaw, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            return go;
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
