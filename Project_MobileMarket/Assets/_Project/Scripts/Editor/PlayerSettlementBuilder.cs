using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Lays out the player's settlement (Scenes/Main/Player_Settlement) after its preview art: a rounded perimeter of the
    /// VARCO wall variants with a north gate, the player's container-and-plank house and water tower in the north-west, a farm in the south-east,
    /// a road from the gate down to the open lot in the west (left empty for the player's truck), desert terrain and scrub
    /// around it, and a NavMesh. Landmarks (gate, house, windmills, greenhouses...) come from the P_Settle_* prefabs
    /// that SettlementPropBuilder makes.
    /// Everything lives under the "PlayerSettlement" root, which is rebuilt each run; nothing else in the scene is touched.
    /// Positions below are world units; the interior is about 66 x 48 units (x -33..33, z -24..24), north = +Z.
    /// Placed buildings/props are shrunk by PropScale; loot boxes and farm plots keep their real size (the player handles
    /// them directly).
    /// </summary>
    public static class PlayerSettlementBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main/Player_Settlement.unity";
        private const string TestScenePath = "Assets/_Project/Scenes/Test/TestScene.unity";
        private const string RootName = "PlayerSettlement";
        private const string TerrainDataPath = "Assets/_Project/Scenes/Main/TD_PlayerSettlement.asset";
        private const string NavMeshDataPath = "Assets/_Project/Scenes/Main/NavMesh_PlayerSettlement.asset";
        private const string MatDir = "Assets/_Project/Materials/";
        private const string SettleModels = "Assets/_Project/Models/Settlement/";
        private const string EnvPrefabs = "Assets/_Project/Prefabs/Environment/";
        private const string SettlementPrefabs = "Assets/_Project/Prefabs/PlayerSettlement/";
        private const string DE = "Assets/@ExternalAssets/Low Poly Desert Environment/Prefabs/";

        /// <summary>Scale of the placed buildings/props (walls included) relative to their prefabs.</summary>
        private const float PropScale = 0.7071f;

        // Perimeter: rounded rectangle, gate gap on the north side (as wide as the container gate's opening).
        private const float HalfW = 33f, HalfD = 24f, CornerR = 8f;
        private const float GateCenterX = 10f, GateGap = 16f * PropScale;
        private const float GateMinX = GateCenterX - GateGap * 0.5f, GateMaxX = GateCenterX + GateGap * 0.5f;
        private const float WallSegment = 6f * PropScale;

        // Terrain around the settlement.
        private const float TerrainW = 400f, TerrainD = 300f, TerrainH = 16f;
        private static readonly Vector3 TerrainOrigin = new Vector3(-200f, 0f, -150f);

        /// <summary>Gate -> open lot, painted into the terrain as a dirt track (the terrain is also flattened along it).</summary>
        private static readonly Vector3[] RoadPath = { new Vector3(GateCenterX, 0f, 34f), new Vector3(GateCenterX, 0f, 20f), new Vector3(2f, 0f, 8f), new Vector3(-10f, 0f, -2f), new Vector3(-18f, 0f, -7f) };
        private const float RoadWidth = 7f * PropScale;

        /// <summary>Open lot west of the road, kept clear for the player's truck (placed by hand).</summary>
        private static readonly Rect TruckLot = Rect.MinMaxRect(-31f, -15f, -16f, 1f);

        // Farm plot block (unscaled plots, 2 units apart), centered on FarmCenter.
        private const int PlotColumns = 9, PlotRows = 4;
        private const float PlotSpacing = 2f;
        private static readonly Vector3 FarmCenter = new Vector3(12f, 0f, -9f);

        private static Transform root;
        private static System.Random rng;
        private static readonly List<Rect> occupied = new List<Rect>();

        [MenuItem("MobileMarket/Player Settlement/Build Layout")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("[PlayerSettlementBuilder] Open " + ScenePath + " first."); return; }

            rng = new System.Random(4242);
            occupied.Clear();
            foreach (var go in scene.GetRootGameObjects()) if (go.name == RootName) Object.DestroyImmediate(go);
            root = new GameObject(RootName).transform;

            occupied.Add(TruckLot);

            var terrain = BuildTerrain();
            BuildPerimeter();
            BuildNorthWest();
            BuildGateArea();
            BuildFarm();
            BuildOutside();
            BuildVegetation();
            SnapToTerrain(terrain);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.NavigationStatic);

            BakeNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PlayerSettlementBuilder] Built with {root.GetComponentsInChildren<Transform>().Length} objects.");
        }

        /// <summary>
        /// One-time: copies the player, camera, light, UI canvas, event system and managers from TestScene, keeping their
        /// prefab links and the references between them (duplicated together, then moved). Replaces this scene's bare camera/light.
        /// </summary>
        [MenuItem("MobileMarket/Player Settlement/Import Gameplay Systems From TestScene")]
        public static void ImportGameplaySystems()
        {
            var target = SceneManager.GetActiveScene();
            if (target.path != ScenePath) { Debug.LogError("[PlayerSettlementBuilder] Open " + ScenePath + " first."); return; }
            ImportGameplaySystemsInto(target, new Vector3(-30f, 0f, -8f));
        }

        /// <summary>Copies TestScene's player, camera, light, UI, event system and managers into target (see above).</summary>
        public static void ImportGameplaySystemsInto(Scene target, Vector3 playerPosition)
        {
            foreach (var go in target.GetRootGameObjects())
                if (go.GetComponent<MobileMarket.Player.PlayerHealth>() != null) { Debug.LogWarning("[PlayerSettlementBuilder] Player already present."); return; }

            string[] names = { "Main Camera", "Directional Light", "Player", "SaveManager", "EventSystem", "Canvas", "UIManager", "TimeManager", "PoolManager", "EnemySpawnManager", "P_SceneLoader" };
            var source = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);
            var picks = new List<Object>();
            foreach (var go in source.GetRootGameObjects()) if (System.Array.IndexOf(names, go.name) >= 0) picks.Add(go);

            Selection.objects = picks.ToArray();
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var copies = Selection.gameObjects;

            foreach (var go in target.GetRootGameObjects())
                if (go.name == "Main Camera" || go.name == "Directional Light") Object.DestroyImmediate(go);
            foreach (var go in copies)
            {
                go.name = go.name.Replace(" (1)", "");
                SceneManager.MoveGameObjectToScene(go, target);
                if (go.GetComponent<MobileMarket.Player.PlayerHealth>() != null)
                    go.transform.SetPositionAndRotation(new Vector3(playerPosition.x, go.transform.position.y + playerPosition.y, playerPosition.z), Quaternion.identity);
            }
            Selection.objects = new Object[0];
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(target);
            EditorSceneManager.MarkSceneDirty(target);
        }

        // ---------------------------------------------------------------- layout

        private static void BuildPerimeter()
        {
            var g = Group("PerimeterWall");
            string[] variants = { "P_Settle_Wall_Wood", "P_Settle_Wall_Scrap", "P_Settle_Wall_Barricade" };
            float[] weights = { 0.4f, 0.35f, 0.25f };

            var path = RoundedRect(HalfW, HalfD, CornerR, 10);
            float total = 0f;
            for (int i = 0; i < path.Count; i++) total += Vector3.Distance(path[i], path[(i + 1) % path.Count]);
            int count = Mathf.RoundToInt(total / WallSegment);
            float step = total / count;

            for (int k = 0; k < count; k++)
            {
                Vector3 a = PointAt(path, step * k), b = PointAt(path, step * (k + 1));
                Vector3 mid = (a + b) * 0.5f;
                if (mid.z > HalfD - 2f && mid.x > GateMinX - 1f && mid.x < GateMaxX + 1f) continue; // gate opening

                // Decorated face (prefab -Z) looks into the settlement.
                Vector3 inward = (-mid).normalized; inward.y = 0f;
                Vector3 along = (b - a).normalized;
                var rot = Quaternion.LookRotation(-Vector3.ProjectOnPlane(inward, along).normalized, Vector3.up);
                var go = PlacePrefab(variants[WeightedPick(weights)], g, mid, rot);
                if (go != null) go.name = "Wall_" + k.ToString("00");
            }
        }

        private static void BuildNorthWest()
        {
            var g = Group("NorthWest");
            PlacePrefab("P_Settle_PlayerHouse", g, new Vector3(-21f, 0f, 14f), Quaternion.identity);
            PlaceModel("Settle_WaterTank", g, new Vector3(-6.5f, 0f, 19f), 6f, 6f, 0f);
            PlacePrefab("P_Settle_Windmill_Big", g, new Vector3(-3f, 0f, 14f), Quaternion.identity);
        }

        private static void BuildGateArea()
        {
            var g = Group("Gate");
            // Container gate: only its two pillars have colliders, so the opening between them stays walkable.
            PlacePrefab("P_Settle_ContainerGate", g, new Vector3(GateCenterX, 0f, HalfD), Quaternion.identity);

            PlaceModel("Settle_Watchtower", g, new Vector3(-1.2f, 0f, 20.5f), 4f, 4f, 180f);
            PlaceModel("Settle_Watchtower", g, new Vector3(21.2f, 0f, 20.5f), 4f, 4f, 180f);

            // Workbench yard east of the gate, with the conveyor along the east wall.
            var yard = Group("Workyard", g);
            PlacePrefab("P_Settle_Conveyor", yard, new Vector3(30f, 0f, 10.5f), Quaternion.Euler(0f, 90f, 0f));
            PlaceModel("Settle_Stall", yard, new Vector3(25f, 0f, 18f), 5f, 3f, 180f);
            PlacePrefab("P_LootBox_MilitaryCrate", yard, new Vector3(22.5f, 0f, 14f), Quaternion.identity, false);
            PlacePrefab("P_LootBox_WoodCrate", yard, new Vector3(25.5f, 0f, 13.5f), Quaternion.Euler(0f, 15f, 0f), false);
            PlacePrefab("P_LootBox_OilDrum", yard, new Vector3(27.8f, 0f, 15.5f), Quaternion.identity, false);
            PlacePrefab("P_Settle_Shanty_Small", yard, new Vector3(25f, 0f, 7f), Quaternion.Euler(0f, 90f, 0f));
        }

        private static void BuildFarm()
        {
            var g = Group("Farm");

            // All plots in one block; greenhouses, water towers, windmill and tractor around it.
            var plots = Group("Plots", g);
            var plot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/P_FarmPlot.prefab");
            for (int x = 0; plot != null && x < PlotColumns; x++)
                for (int z = 0; z < PlotRows; z++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(plot, plots);
                    go.transform.position = FarmCenter + new Vector3((x - (PlotColumns - 1) * 0.5f) * PlotSpacing, 0f, (z - (PlotRows - 1) * 0.5f) * PlotSpacing);
                }
            float bw = PlotColumns * PlotSpacing, bd = PlotRows * PlotSpacing;
            occupied.Add(new Rect(FarmCenter.x - bw * 0.5f - 1f, FarmCenter.z - bd * 0.5f - 1f, bw + 2f, bd + 2f));

            PlacePrefab("P_Settle_Greenhouse", g, new Vector3(4f, 0f, -18.5f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefab("P_Settle_Greenhouse", g, new Vector3(15f, 0f, -18.5f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefab("P_Settle_Greenhouse", g, new Vector3(26.5f, 0f, -9f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefab("P_Settle_FarmWaterTower", g, new Vector3(24f, 0f, -1f), Quaternion.identity);
            PlacePrefab("P_Settle_FarmWaterTower", g, new Vector3(27.5f, 0f, -1f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefab("P_Settle_Windmill_Small", g, new Vector3(12f, 0f, -1f), Quaternion.identity);
            PlacePrefab("P_Settle_Tractor", g, new Vector3(27f, 0f, -19f), Quaternion.Euler(0f, 30f, 0f));

            PlacePrefab("P_Settle_Shanty_Awning", g, new Vector3(-9f, 0f, -19f), Quaternion.Euler(0f, 180f, 0f));
            PlacePrefab("P_Settle_Shanty_Container", g, new Vector3(30f, 0f, -16.5f), Quaternion.Euler(0f, 90f, 0f));
        }

        private static void BuildOutside()
        {
            var g = Group("Outside");
            // Sunk so the fuselage rests in the sand (the drooping wing tips are the lowest point of the model).
            PlacePrefab("P_Settle_PlaneWreck", g, new Vector3(50f, -1.41f, 36f), Quaternion.Euler(0f, 30f, 0f));
        }

        // ---------------------------------------------------------------- terrain & vegetation

        private static Terrain BuildTerrain()
        {
            var g = Group("Ground");
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainDataPath);
            }
            td.heightmapResolution = 257;
            td.alphamapResolution = 512;
            td.baseMapResolution = 512;
            td.size = new Vector3(TerrainW, TerrainH, TerrainD);
            td.terrainLayers = new[]
            {
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(MatDir + "TL_Settle_Sand.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(MatDir + "TL_Settle_PackedSand.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(MatDir + "TL_Settle_Dirt.terrainlayer"),
            };

            float Noise(float x, float z, float f) => Mathf.PerlinNoise(x * f + 311.7f, z * f + 57.3f);

            int hr = td.heightmapResolution;
            var heights = new float[hr, hr];
            for (int iz = 0; iz < hr; iz++)
                for (int ix = 0; ix < hr; ix++)
                {
                    float x = TerrainOrigin.x + TerrainW * ix / (hr - 1);
                    float z = TerrainOrigin.z + TerrainD * iz / (hr - 1);
                    float d = Mathf.Min(CompoundDistance(x, z) - 20f, RoadDistance(x, z) - 8f);
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 40f));
                    float dunes = Noise(x, z, 0.014f) * 0.65f + Noise(x, z, 0.04f) * 0.35f;
                    heights[iz, ix] = rise * dunes * Mathf.Lerp(1f, 4f, Mathf.Clamp01((d - 40f) / 120f)) / TerrainH;
                }
            td.SetHeights(0, 0, heights);

            int ar = td.alphamapResolution;
            var alphas = new float[ar, ar, 3];
            for (int iz = 0; iz < ar; iz++)
                for (int ix = 0; ix < ar; ix++)
                {
                    float x = TerrainOrigin.x + TerrainW * (ix + 0.5f) / ar;
                    float z = TerrainOrigin.z + TerrainD * (iz + 0.5f) / ar;
                    float wobble = (Noise(x, z, 0.07f) - 0.5f) * 6f;
                    float interior = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 8f, CompoundDistance(x, z) + wobble));
                    float mottle = Noise(x, z, 0.09f) * 0.6f + Noise(x, z, 0.3f) * 0.4f;
                    float dirtShare = Mathf.Lerp(0.3f, 0.85f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, mottle)));
                    float farm = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 4f, FarmAreaDistance(x, z) + wobble * 0.5f));
                    dirtShare = Mathf.Max(dirtShare, farm);
                    float dirt = interior * dirtShare;
                    float packed = interior - dirt;
                    float sand = 1f - interior;

                    // Dirt track along the road path with a ragged edge, framed by light packed-sand shoulders so it reads
                    // against the mottled dirt of the compound floor.
                    float rd = RoadDistance(x, z) + wobble * 0.25f;
                    float road = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.5f, 1f, rd));
                    float shoulder = Mathf.Max(0f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 4f, rd)) - road);
                    float rest = 1f - road - shoulder;
                    alphas[iz, ix, 0] = sand * rest;
                    alphas[iz, ix, 1] = packed * rest + shoulder;
                    alphas[iz, ix, 2] = dirt * rest + road;
                }
            td.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(g, false);
            go.transform.position = TerrainOrigin;
            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 300f;
            return terrain;
        }

        private static void BuildVegetation()
        {
            var g = Group("Vegetation");
            var shrub = AssetDatabase.LoadAssetAtPath<GameObject>(SettleModels + "Settle_Shrub.fbx");
            for (int i = 0, placed = 0; shrub != null && i < 1500 && placed < 320; i++)
            {
                var p = new Vector3(Rand(-195f, 195f), 0f, Rand(-145f, 145f));
                bool inside = CompoundDistance(p.x, p.z) < 2f;
                if (inside && rng.NextDouble() < 0.88) continue;
                if (!IsFree(p) || RoadDistance(p.x, p.z) < 5f) continue;
                var w = Wrap(shrub, g);
                var size = Bounds(w.gameObject).size;
                w.localScale = Vector3.one * (Rand(1.2f, 2.6f) / Mathf.Max(size.x, size.z));
                w.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                var b = Bounds(w.gameObject);
                w.position += new Vector3(p.x - b.center.x, -b.min.y, p.z - b.center.z);
                placed++;
            }

            string[] rocks = { DE + "Rock_01.prefab", DE + "Rock_02.prefab", DE + "Rock_03.prefab", DE + "Rock_04.prefab", DE + "Rock_05.prefab" };
            string[] cacti = { DE + "Cactus_01.prefab", DE + "Cactus_02.prefab", DE + "Cactus_03.prefab", DE + "Tree_01.prefab" };
            void Scatter(string[] set, int count, float minS, float maxS)
            {
                for (int i = 0, placed = 0; i < count * 5 && placed < count; i++)
                {
                    var p = new Vector3(Rand(-195f, 195f), 0f, Rand(-145f, 145f));
                    if (CompoundDistance(p.x, p.z) < 12f || !IsFree(p) || RoadDistance(p.x, p.z) < 6f) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(set[rng.Next(set.Length)]);
                    if (prefab == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
                    go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
                    go.transform.localScale *= Rand(minS, maxS);
                    placed++;
                }
            }
            Scatter(rocks, 90, 1f, 3f);
            Scatter(cacti, 70, 0.8f, 1.4f);
        }

        private static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh");
            go.transform.SetParent(root, false);
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            var old = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavMeshDataPath);
            if (old != null) AssetDatabase.DeleteAsset(NavMeshDataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
        }

        // ---------------------------------------------------------------- shape helpers

        private static List<Vector3> RoundedRect(float hw, float hd, float r, int arcSteps)
        {
            var pts = new List<Vector3>();
            Vector3[] centers = { new Vector3(hw - r, 0f, hd - r), new Vector3(-hw + r, 0f, hd - r), new Vector3(-hw + r, 0f, -hd + r), new Vector3(hw - r, 0f, -hd + r) };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= arcSteps; s++)
                {
                    float a = (c * 90f + 90f * s / arcSteps) * Mathf.Deg2Rad;
                    pts.Add(centers[c] + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
            return pts;
        }

        private static Vector3 PointAt(List<Vector3> loop, float distance)
        {
            for (int i = 0; ; i = (i + 1) % loop.Count)
            {
                Vector3 a = loop[i], b = loop[(i + 1) % loop.Count];
                float len = Vector3.Distance(a, b);
                if (distance <= len) return Vector3.Lerp(a, b, len > 0f ? distance / len : 0f);
                distance -= len;
            }
        }

        /// <summary>Signed distance to the rounded perimeter (negative inside).</summary>
        private static float CompoundDistance(float x, float z)
        {
            float qx = Mathf.Abs(x) - (HalfW - CornerR), qz = Mathf.Abs(z) - (HalfD - CornerR);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qz), 0f) - CornerR;
        }

        private static float FarmAreaDistance(float x, float z)
        {
            var r = Rect.MinMaxRect(-4f, -23f, 31f, 3f);
            float dx = Mathf.Max(r.xMin - x, x - r.xMax), dz = Mathf.Max(r.yMin - z, z - r.yMax);
            return Mathf.Max(dx, dz);
        }

        private static float RoadDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < RoadPath.Length - 1; i++)
            {
                var a = new Vector2(RoadPath[i].x, RoadPath[i].z); var b = new Vector2(RoadPath[i + 1].x, RoadPath[i + 1].z);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
            }
            return best - RoadWidth * 0.5f;
        }

        // ---------------------------------------------------------------- placement helpers

        private static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : root, false);
            return t;
        }

        /// <summary>
        /// Places a prefab (from Prefabs/PlayerSettlement, else Prefabs/Environment) at a world position, shrunk by PropScale
        /// unless scaleProp is false.
        /// </summary>
        private static GameObject PlacePrefab(string name, Transform parent, Vector3 pos, Quaternion rot, bool scaleProp = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettlementPrefabs + name + ".prefab")
                         ?? AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabs + name + ".prefab");
            if (prefab == null) { Debug.LogWarning("[PlayerSettlementBuilder] Missing prefab " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            if (scaleProp) go.transform.localScale *= PropScale;
            var b = Bounds(go);
            occupied.Add(new Rect(b.min.x - 1f, b.min.z - 1f, b.size.x + 2f, b.size.z + 2f));
            return go;
        }

        /// <summary>
        /// A VARCO settlement model fitted to a width x depth footprint (uniform scale, times PropScale), grounded at a world
        /// center, with a box collider.
        /// </summary>
        private static GameObject PlaceModel(string model, Transform parent, Vector3 center, float width, float depth, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettleModels + model + ".fbx");
            if (prefab == null) { Debug.LogWarning("[PlayerSettlementBuilder] Missing model " + model); return null; }
            width *= PropScale;
            depth *= PropScale;
            var w = Wrap(prefab, parent);
            var native = Bounds(w.gameObject).size;
            bool swap = (width >= depth) != (native.x >= native.z);
            float s = Mathf.Min(width / (swap ? native.z : native.x), depth / (swap ? native.x : native.z));
            w.localScale = Vector3.one * s;
            w.rotation = Quaternion.Euler(0f, yaw + (swap ? 90f : 0f), 0f);
            var b = Bounds(w.gameObject);
            w.position += new Vector3(center.x - b.center.x, -b.min.y, center.z - b.center.z);

            b = Bounds(w.gameObject);
            var col = w.gameObject.AddComponent<BoxCollider>();
            col.center = w.InverseTransformPoint(b.center);
            col.size = new Vector3(b.size.x / s, b.size.y / s, b.size.z / s);
            if (swap) col.size = new Vector3(col.size.z, col.size.y, col.size.x);
            occupied.Add(new Rect(b.min.x - 1f, b.min.z - 1f, b.size.x + 2f, b.size.z + 2f));
            return w.gameObject;
        }

        private static Transform Wrap(GameObject prefab, Transform parent)
        {
            var wrapper = new GameObject(prefab.name).transform;
            wrapper.SetParent(parent, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper);
            inst.transform.localPosition = Vector3.zero;
            return wrapper;
        }

        private static void SnapToTerrain(Terrain terrain)
        {
            foreach (string group in new[] { "Vegetation" })
            {
                var g = root.Find(group);
                if (g == null) continue;
                foreach (Transform t in g)
                {
                    var p = t.position;
                    p.y += terrain.SampleHeight(p) + terrain.transform.position.y;
                    t.position = p;
                }
            }
        }

        private static bool IsFree(Vector3 p)
        {
            foreach (var r in occupied) if (r.Contains(new Vector2(p.x, p.z))) return false;
            return true;
        }

        private static int WeightedPick(float[] weights)
        {
            float total = 0f; foreach (var w in weights) total += w;
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++) { roll -= weights[i]; if (roll <= 0f) return i; }
            return weights.Length - 1;
        }

        private static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++) { if (i == 0) b = renderers[i].bounds; else b.Encapsulate(renderers[i].bounds); }
            return b;
        }
    }
}
