using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Builds the "Excavation site" combat map (Scenes/Main/Excavation site): a 600 x 400 wasteland floor ringed by canyon
    /// walls, a river gorge with two bridges, a sinkhole pond, dirt roads, and three areas — the military compound (north-east,
    /// bunker), the ruin excavation (west, a huge cave cut into the canyon wall with the researchers' camp) and the laboratory
    /// complex (south-east). Everything lives under the "ExcavationSite" root, rebuilt each run. North = +Z.
    /// Stage 1 lays out the ground and grey blockout boxes; later stages swap the boxes for real assets.
    /// </summary>
    public static class ExcavationSiteBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main/Excavation site.unity";
        private const string RootName = "ExcavationSite";
        private const string TerrainDataPath = "Assets/_Project/Scenes/Main/TD_ExcavationSite.asset";
        private const string MatDir = "Assets/_Project/Materials/";
        private const string CartoonTex = "Assets/@ExternalAssets/Cartoon_Texture_Pack/";

        // Playable floor: rounded rectangle x -300..300, z -200..200, canyon walls beyond it.
        private const float HalfW = 300f, HalfD = 200f, CornerR = 70f;
        private const float WallRise = 26f;      // horizontal distance over which the canyon wall climbs
        private const float WallHeight = 48f;

        private const float TerrainW = 900f, TerrainD = 700f, TerrainH = 70f, TerrainBase = -14f;
        private static readonly Vector3 TerrainOrigin = new Vector3(-450f, TerrainBase, -350f);

        // River gorge from the north edge down to the south-west, between the excavation and the two compounds. Its bed is
        // carved to a fixed depth (so hills never leave it dry) with wide, smoothed banks.
        private static readonly Vector2[] River = { new Vector2(20f, 230f), new Vector2(5f, 130f), new Vector2(20f, 60f), new Vector2(0f, -20f), new Vector2(-50f, -100f), new Vector2(-85f, -230f) };
        private const float RiverHalfWidth = 9f, RiverBank = 13f, RiverBed = -8.5f, WaterLevel = -5.5f;

        private static readonly Vector2 Sinkhole = new Vector2(205f, -10f);
        private const float SinkholeRadius = 22f;

        // Areas — the floors are levelled; the military compound sits on the highest plateau, the labs on a lower one.
        public static readonly Rect LabArea = Rect.MinMaxRect(30f, -175f, 190f, -45f);
        public static readonly Rect CampArea = Rect.MinMaxRect(-215f, -60f, -95f, 60f);
        private const float MilitaryLevel = 14f, LabLevel = 5f;
        private const float PlateauMargin = 8f;     // level ground kept around the compound fence
        private const float MilitaryCliff = 20f;    // the military plateau slopes down over this distance (then blurred smooth)
        private const float LabSlope = 16f;         // the lab plateau eases down over this distance

        // The dig site is the lowest ground: a terraced pit, and a channel from it to the cave cut into the west wall.
        // CaveMouth is the centre of the cave facade's front at the canyon wall foot.
        public static readonly Vector2 CaveMouth = new Vector2(-306f, 110f);
        private static readonly Vector2 PitCenter = new Vector2(-246f, 98f);
        private static readonly Vector2 CaveFacing = (PitCenter - CaveMouth).normalized;
        private const float CaveChannelHalfWidth = 16f;
        private const float CaveRampLength = 16f;   // the cave model's plank ramp runs this far out in front of its facade
        private const float CaveFacadeDepth = 18f;  // the facade's arch front stands this far behind the mouth line
        private const float CaveArmLength = 38f;    // how far the rock shoulders reach forward around the entrance
        private const float CaveArchHalfWidth = 11f; // half the arch opening (kept clear behind so the dark tunnel shows)
        private const float CaveOverhangSink = 2.5f;  // how far the overhang rock sits down into the facade's lintel
        private const float PitFloor = -10f, PitInner = 28f, PitOuter = 52f;
        private const int PitTerraces = 3;

        // Bridges cross the river where the roads meet it.
        private static readonly Vector2 BridgeNorth = new Vector2(12f, 95f);
        private static readonly Vector2 BridgeSouth = new Vector2(-15f, -45f);

        /// <summary>Roads as (x, ground height, z) points; the ground follows them, so they ramp up the plateaus and down into the pit.</summary>
        private static readonly Vector3[][] Roads =
        {
            new[] { new Vector3(-155f, 0f, 10f), new Vector3(-70f, 0f, 60f), new Vector3(-12f, 0f, 95f), new Vector3(30f, 0f, 95f), new Vector3(56f, MilitaryLevel, 108f), new Vector3(78f, MilitaryLevel, 110f) },
            // The lab road leaves the south bridge road (a route from the north bridge would cut the military plateau corner).
            new[] { new Vector3(12f, 0f, -45f), new Vector3(45f, 2f, -26f), new Vector3(60f, LabLevel, -38f), new Vector3(60f, LabLevel, -60f) },
            new[] { new Vector3(-155f, 0f, 10f), new Vector3(-95f, 0f, -30f), new Vector3(-40f, 0f, -45f), new Vector3(12f, 0f, -45f), new Vector3(30f, LabLevel, -100f), new Vector3(46f, LabLevel, -100f) },
            new[] { new Vector3(-155f, 0f, 10f), new Vector3(-205f, -3f, 68f), new Vector3(-232f, PitFloor, 92f), new Vector3(-292f, PitFloor, 107f) },
            // Inside the military compound: from the gate to a dirt ring road around the central bunker
            // (hub and radius match MilitaryCentre / MilitaryRingRadius, which are declared further down).
            new[] { new Vector3(78f, MilitaryLevel, 110f), new Vector3(116.5f, MilitaryLevel, 113f) },
            RingRoad(new Vector2(152f, 116f), 36f, MilitaryLevel, 18),
        };

        private static Vector3[] RingRoad(Vector2 centre, float radius, float height, int segments)
        {
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                pts[i] = new Vector3(centre.x + Mathf.Cos(a) * radius, height, centre.y + Mathf.Sin(a) * radius);
            }
            return pts;
        }
        private const float RoadWidth = 8f;

        private static Transform root;
        private static Terrain terrain;
        private static Material blockMat, waterMat;

        [MenuItem("MobileMarket/Excavation Site/Build Layout")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("[ExcavationSiteBuilder] Open " + ScenePath + " first."); return; }

            foreach (var go in scene.GetRootGameObjects()) if (go.name == RootName) Object.DestroyImmediate(go);
            root = new GameObject(RootName).transform;
            blockMat = Mat("M_EX_Blockout", new Color(0.62f, 0.62f, 0.6f), false);
            waterMat = Mat("M_EX_Water", new Color(0.13f, 0.27f, 0.25f, 0.86f), true);
            // Rippled surface: the cartoon pack's underwater sand ripples double as small waves (scrolled by WaterScroll).
            waterMat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CartoonTex + "SAND/SAND_Underwater/Textures/Sand_Underwater_Base_Normal.png"));
            waterMat.SetTextureScale("_BumpMap", new Vector2(3f, 3f));
            waterMat.SetFloat("_BumpScale", 0.35f);
            waterMat.EnableKeyword("_NORMALMAP");
            // No sky reflection and a soft highlight: at the low top-down angle sharp sun glare on the ripples turned the river sand-coloured.
            waterMat.SetFloat("_Smoothness", 0.55f);
            waterMat.SetFloat("_EnvironmentReflections", 0f);
            waterMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(waterMat);

            BuildTerrain();
            BuildWater();
            BuildBridges();
            BuildMilitary();
            BuildExcavation();
            BuildLab();
            BuildScatter();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ExcavationSiteBuilder] Built.");
        }

        // ---------------------------------------------------------------- navigation

        private const string NavMeshDataPath = "Assets/_Project/Scenes/Main/NavMesh_ExcavationSite.asset";

        /// <summary>
        /// Bakes the walkable area from the physics colliders (terrain, buildings, props; roofs and RoofHider triggers don't
        /// count). The river and the sinkhole pond are cut out with modifier volumes below bridge-deck height.
        /// </summary>
        [MenuItem("MobileMarket/Excavation Site/Bake NavMesh")]
        public static void BakeNavMesh()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("[ExcavationSiteBuilder] Open " + ScenePath + " first."); return; }
            Transform siteRoot = null;
            foreach (var go in scene.GetRootGameObjects()) if (go.name == RootName) siteRoot = go.transform;
            if (siteRoot == null) { Debug.LogError("[ExcavationSiteBuilder] Build the layout first."); return; }

            var old = siteRoot.Find("NavMesh");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var nav = new GameObject("NavMesh").transform;
            nav.SetParent(siteRoot, false);

            for (int i = 0; i < River.Length - 1; i++)
            {
                Vector2 a = River[i], b = River[i + 1];
                var v = NotWalkable(nav, "RiverCut_" + i);
                v.transform.SetPositionAndRotation(new Vector3((a.x + b.x) * 0.5f, 0f, (a.y + b.y) * 0.5f),
                    Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.y - a.y)));
                v.center = new Vector3(0f, (RiverBed - 2f + WaterLevel + 1.5f) * 0.5f, 0f);
                v.size = new Vector3(RiverHalfWidth * 2f, WaterLevel + 1.5f - (RiverBed - 2f), Vector2.Distance(a, b) + RiverHalfWidth);
            }
            var pond = NotWalkable(nav, "SinkholeCut");
            pond.transform.position = new Vector3(Sinkhole.x, 0f, Sinkhole.y);
            pond.center = new Vector3(0f, -9f, 0f);
            pond.size = new Vector3(SinkholeRadius * 1.6f, 10f, SinkholeRadius * 1.6f);

            var surface = nav.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            var oldData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavMeshDataPath);
            if (oldData != null) AssetDatabase.DeleteAsset(NavMeshDataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ExcavationSiteBuilder] NavMesh baked.");
        }

        private static NavMeshModifierVolume NotWalkable(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<NavMeshModifierVolume>();
            v.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
            return v;
        }

        // ---------------------------------------------------------------- terrain

        private static void BuildTerrain()
        {
            var g = Group("Ground");
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainDataPath);
            }
            td.heightmapResolution = 513;
            td.alphamapResolution = 1024;
            td.baseMapResolution = 1024;
            td.size = new Vector3(TerrainW, TerrainH, TerrainD);
            // 0: brown dirt ground, 1: darker packed dirt (compound floors, patches), 2: light worn tracks (roads), 3: rock.
            td.terrainLayers = new[]
            {
                Layer("TL_EX_DirtPath", CartoonTex + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", CartoonTex + "DIRT/Dirt_Path/Textures/Dirt_Path_Normal.png", 8f),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(MatDir + "TL_Settle_Dirt.terrainlayer"),
                // This map's own road layer (the settlement's packed sand stays untouched): same worn pattern, dark brown.
                Layer("TL_EX_Road", CartoonTex + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", "", 10f),
                Layer("TL_EX_Cliff", CartoonTex + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_A_Basecolor_A.png", CartoonTex + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_A_Normal.png", 14f),
            };
            // The cartoon cliff rock is blue-grey; tint it to the reference's red-brown canyon rock.
            td.terrainLayers[3].diffuseRemapMax = new Vector4(1.05f, 0.72f, 0.5f, 1f);
            EditorUtility.SetDirty(td.terrainLayers[3]);
            td.terrainLayers[2].diffuseRemapMax = new Vector4(0.38f, 0.3f, 0.23f, 1f);
            EditorUtility.SetDirty(td.terrainLayers[2]);

            int hr = td.heightmapResolution;
            var heights = new float[hr, hr];
            var smooth = new float[hr, hr];
            for (int iz = 0; iz < hr; iz++)
                for (int ix = 0; ix < hr; ix++)
                {
                    float x = TerrainOrigin.x + TerrainW * ix / (hr - 1);
                    float z = TerrainOrigin.z + TerrainD * iz / (hr - 1);
                    heights[iz, ix] = (Height(x, z) - TerrainBase) / TerrainH;
                    smooth[iz, ix] = Mathf.Max(RiverInfluence(x, z), MilitaryEdgeInfluence(x, z));
                }
            BlurWhere(heights, smooth, 4);
            td.SetHeights(0, 0, heights);

            int ar = td.alphamapResolution;
            var alphas = new float[ar, ar, 4];
            for (int iz = 0; iz < ar; iz++)
                for (int ix = 0; ix < ar; ix++)
                {
                    float x = TerrainOrigin.x + TerrainW * (ix + 0.5f) / ar;
                    float z = TerrainOrigin.z + TerrainD * (iz + 0.5f) / ar;
                    float wobble = (Noise(x, z, 0.08f) - 0.5f) * 3f;

                    // Steep ground (canyon walls, gorge banks, the pond rim) is bare rock.
                    float steepness = td.GetSteepness((ix + 0.5f) / ar, (iz + 0.5f) / ar);
                    float cliff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 42f, steepness));
                    float road = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 2f, RoadDistance(x, z) + wobble));
                    float zone = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 6f, ZoneDistance(x, z) + wobble * 2f));
                    float mottle = Noise(x, z, 0.03f);

                    float packed = Mathf.Max(zone * 0.8f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, mottle)) * 0.6f);
                    float rest = 1f - cliff;
                    float roadW = road * rest;
                    rest -= roadW;
                    alphas[iz, ix, 0] = rest * (1f - packed);
                    alphas[iz, ix, 1] = rest * packed;
                    alphas[iz, ix, 2] = roadW;
                    alphas[iz, ix, 3] = cliff;
                }
            td.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(g, false);
            go.transform.position = TerrainOrigin;
            terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 400f;
        }

        /// <summary>World height of the ground at (x, z) (before the river banks are blurred).</summary>
        private static float Height(float x, float z)
        {
            var p = new Vector2(x, z);

            // Rolling wasteland hills, kept off the compounds, the camp and the roads: broad swells, a mid-size undulation
            // (offset so it doesn't line up with the swells) and scattered hillocks and hollows.
            float hills = (Noise(x, z, 0.006f) - 0.5f) * 16f + (Noise(x, z, 0.025f) - 0.5f) * 3f
                        + (Noise(x + 531f, z - 277f, 0.022f) - 0.5f) * 8f
                        + Hillocks(x, z);
            float h = hills * (1f - Flatness(x, z));

            // Plateaus: the military compound on a cliff-edged height, the labs on a gently sloping rise.
            float mil = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlateauMargin, PlateauMargin + MilitaryCliff, MilitaryDistance(p)));
            float lab = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlateauMargin, PlateauMargin + LabSlope, RectDistance(p, LabArea)));
            h = Mathf.Lerp(h, LabLevel, lab);
            h = Mathf.Lerp(h, MilitaryLevel, mil);

            // Terraced dig pit in front of the cave.
            float pd = Vector2.Distance(p, PitCenter);
            if (pd < PitOuter)
            {
                float steps = Mathf.InverseLerp(PitInner, PitOuter, pd) * PitTerraces;
                float terrace = (Mathf.Floor(steps) + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, steps - Mathf.Floor(steps)))) / PitTerraces;
                h = Mathf.Lerp(PitFloor, h, terrace);
            }

            // Roads carry their own heights, so they ramp up the plateaus and down into the pit.
            float rd = NearestRoad(x, z, out float roadHeight);
            // A wide falloff so ramps cut into the plateaus get gently sloping sides instead of a sharp V.
            float onRoad = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadWidth * 0.5f + 1f, RoadWidth * 0.5f + 14f, rd));
            h = Mathf.Lerp(h, roadHeight, onRoad);

            // River gorge and sinkhole pond, carved to fixed depths.
            float gorge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiverHalfWidth, RiverHalfWidth + RiverBank, PolylineDistance(x, z, River)));
            h = Mathf.Lerp(h, RiverBed, gorge);
            float sink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SinkholeRadius - 6f, SinkholeRadius + 2f, Vector2.Distance(p, Sinkhole)));
            h = Mathf.Lerp(h, -12f, sink);

            // Canyon wall: a steep climb, then a rough plateau.
            float edge = RimDistance(x, z) + (Noise(x, z, 0.012f) - 0.5f) * 36f + (Noise(x, z, 0.05f) - 0.5f) * 8f;
            if (edge > 0f)
            {
                float climb = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / WallRise));
                float top = WallHeight + (Noise(x, z, 0.02f) - 0.5f) * 14f;
                h = Mathf.Lerp(h, top, climb);
            }
            return CaveCut(x, z, h);
        }

        /// <summary>Randomly placed round hillocks (and a few shallow hollows): centre x, z, radius, height. Fixed seed.</summary>
        private static readonly Vector4[] HillockSet = MakeHillocks(72, 1207);

        private static Vector4[] MakeHillocks(int count, int seed)
        {
            var rng = new System.Random(seed);
            var list = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * (HalfW - 20f);
                float z = (float)(rng.NextDouble() * 2 - 1) * (HalfD - 20f);
                float r = 12f + (float)rng.NextDouble() * 18f;
                // Mostly rises of 3-8 m; about one in five is a shallow hollow. Height capped by radius so slopes stay walkable.
                float hgt = 3f + (float)rng.NextDouble() * 5f;
                if (rng.NextDouble() < 0.2) hgt = -hgt * 0.5f;
                list[i] = new Vector4(x, z, r, Mathf.Min(hgt, r * 0.28f));
            }
            return list;
        }

        private static float Hillocks(float x, float z)
        {
            float sum = 0f;
            foreach (var hk in HillockSet)
            {
                float dx = x - hk.x, dz = z - hk.y;
                float d2 = dx * dx + dz * dz, r2 = hk.z * hk.z;
                if (d2 < r2 * 4f) sum += hk.w * Mathf.Exp(-d2 / r2);
            }
            return sum;
        }

        /// <summary>1 on zone floors and roads (kept level), 0 in the open wasteland.</summary>
        private static float Flatness(float x, float z)
        {
            float zone = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 26f, ZoneDistance(x, z)));
            float road = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 18f, RoadDistance(x, z)));
            return Mathf.Max(zone, road);
        }

        /// <summary>Box-blurs the heightmap, each sample blended toward its blur by weight (1 = fully smoothed).</summary>
        private static void BlurWhere(float[,] heights, float[,] weight, int passes)
        {
            int n = heights.GetLength(0);
            var tmp = new float[n, n];
            for (int pass = 0; pass < passes; pass++)
            {
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        if (weight[z, x] <= 0f) { tmp[z, x] = heights[z, x]; continue; }
                        float sum = 0f; int count = 0;
                        for (int dz = -2; dz <= 2; dz++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int sz = Mathf.Clamp(z + dz, 0, n - 1), sx = Mathf.Clamp(x + dx, 0, n - 1);
                                sum += heights[sz, sx]; count++;
                            }
                        tmp[z, x] = Mathf.Lerp(heights[z, x], sum / count, weight[z, x]);
                    }
                System.Array.Copy(tmp, heights, tmp.Length);
            }
        }

        /// <summary>The ring around the military plateau's edge (its slope plus a few metres either side), blurred smooth.</summary>
        private static float MilitaryEdgeInfluence(float x, float z)
        {
            float d = MilitaryDistance(new Vector2(x, z));
            float mid = PlateauMargin + MilitaryCliff * 0.5f, half = MilitaryCliff * 0.5f + 6f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half - 4f, half, Mathf.Abs(d - mid)));
        }

        /// <summary>How far the river's influence reaches (used to blur only around it).</summary>
        private static float RiverInfluence(float x, float z) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiverHalfWidth + RiverBank, RiverHalfWidth + RiverBank + 10f, PolylineDistance(x, z, River)));

        /// <summary>Signed distance to the rounded playable rectangle (negative inside).</summary>
        private static float RimDistance(float x, float z)
        {
            float qx = Mathf.Abs(x) - (HalfW - CornerR), qz = Mathf.Abs(z) - (HalfD - CornerR);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qz), 0f) - CornerR;
        }

        /// <summary>
        /// The cave facade stands against the west canyon wall: in front of it a channel is cut down to the pit floor (the
        /// cave mouth is the lowest ground), behind it the cliff is raised to full height so the facade reads as carved into it.
        /// </summary>
        private static float CaveCut(float x, float z, float h)
        {
            var rel = new Vector2(x, z) - CaveMouth;
            float t = Vector2.Dot(rel, CaveFacing);                                   // + in front of the facade
            float s = Mathf.Abs(rel.x * CaveFacing.y - rel.y * CaveFacing.x);        // sideways from the mouth's axis
            float rock = (Noise(x, z, 0.02f) - 0.5f) * 10f + (Noise(x, z, 0.07f) - 0.5f) * 4f;

            // The walkway hollow: only as wide as the arch at the facade (the rock closes over the facade's flanks), then
            // opening out like a funnel toward the dig, so the entrance sits at the back of a horseshoe carved into the cliff.
            float inner = CaveArchHalfWidth + 2f + Mathf.Max(0f, t) * 0.6f;
            float soft = Mathf.Lerp(4f, 10f, Mathf.InverseLerp(-6f, 6f, t));
            float side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, inner + soft, s));
            float along = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 62f, t));
            h = Mathf.Lerp(h, PitFloor, side * along);

            if (t < 0f)
            {
                // Just behind the arch (where the darkness panel hangs) the cliff rises over the facade's roof and body;
                // beside the arch it rises right at the facade front, burying the brickwork and roof corners.
                float riseAt = Mathf.Lerp(CaveFacadeDepth + 3.5f, CaveFacadeDepth + 0.5f, Mathf.InverseLerp(CaveArchHalfWidth, CaveArchHalfWidth + 3f, s));
                float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(riseAt, riseAt + 3f, -t));
                float width = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(34f, 50f, s));
                h = Mathf.Lerp(h, Mathf.Max(h, WallHeight + rock), back * width);
            }

            // Rock shoulders along both sides of the hollow, sloping down and rounding off toward the dig (they stand
            // where the canyon wall alone would leave open ground).
            float flank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner + soft * 0.5f, inner + soft + 8f, s))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner + 40f, inner + 60f, s)));
            float reach = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CaveArmLength * 0.45f, CaveArmLength, t + s * 0.15f));
            float shoulder = Mathf.Lerp(WallHeight * 0.9f, 6f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / CaveArmLength))) + rock;
            return Mathf.Lerp(h, Mathf.Max(h, shoulder), flank * reach);
        }

        private static float ZoneDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = Mathf.Min(MilitaryDistance(p), Mathf.Min(RectDistance(p, LabArea), RectDistance(p, CampArea)));
            return Mathf.Min(d, Vector2.Distance(p, PitCenter) - PitOuter);
        }

        private static float RectDistance(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, p.y - r.yMax);
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dz), 0f);
        }

        private static float RoadDistance(float x, float z) => NearestRoad(x, z, out _) - RoadWidth * 0.5f;

        /// <summary>Distance to the nearest road centre line, and the road's height there.</summary>
        private static float NearestRoad(float x, float z, out float height)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            height = 0f;
            foreach (var road in Roads)
                for (int i = 0; i < road.Length - 1; i++)
                {
                    var a = new Vector2(road[i].x, road[i].z); var b = new Vector2(road[i + 1].x, road[i + 1].z);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                    float d = Vector2.Distance(p, a + (b - a) * t);
                    if (d >= best) continue;
                    best = d;
                    height = Mathf.Lerp(road[i].y, road[i + 1].y, t);
                }
            return best;
        }

        private static float PolylineDistance(float x, float z, Vector2[] line)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < line.Length - 1; i++)
            {
                var a = line[i]; var b = line[i + 1];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
            }
            return best;
        }

        private static float Noise(float x, float z, float f) => Mathf.PerlinNoise(x * f + 917.3f, z * f + 211.9f);

        // ---------------------------------------------------------------- water & bridges

        /// <summary>
        /// Water only where it belongs: a ribbon along the river and a disc in the sinkhole (a map-wide plane would also
        /// flood the dig pit and low hollows, which sit below the water level). Meshes are saved under Models/Excavation.
        /// </summary>
        private static void BuildWater()
        {
            var g = Group("Water");
            WaterObject("River", g, RiverRibbon(RiverHalfWidth + 5f, 4f), "Assets/_Project/Models/Excavation/Mesh_EX_RiverWater.asset");
            WaterObject("Sinkhole", g, Disc(Sinkhole, SinkholeRadius, 48), "Assets/_Project/Models/Excavation/Mesh_EX_SinkholeWater.asset");
        }

        private static void WaterObject(string name, Transform parent, Mesh mesh, string assetPath)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (old != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, WaterLevel, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = waterMat;
            go.AddComponent<MobileMarket.Environment.WaterScroll>();
        }

        private static Mesh RiverRibbon(float halfWidth, float step)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < River.Length - 1; i++)
            {
                Vector2 a = River[i], b = River[i + 1];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int k = (i == 0 ? 0 : 1); k <= n; k++)
                {
                    var p = Vector2.Lerp(a, b, (float)k / n);
                    // Side direction from the neighbouring points, so bends stay the same width.
                    var dir = (b - a).normalized;
                    var side = new Vector2(-dir.y, dir.x) * halfWidth;
                    verts.Add(new Vector3(p.x - side.x, 0f, p.y - side.y));
                    verts.Add(new Vector3(p.x + side.x, 0f, p.y + side.y));
                }
            }
            for (int i = 0; i + 3 < verts.Count; i += 2)
            {
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
            }
            return Finish(verts, tris, "RiverWater");
        }

        private static Mesh Disc(Vector2 centre, float radius, int segments)
        {
            var verts = new List<Vector3> { new Vector3(centre.x, 0f, centre.y) };
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(centre.x + Mathf.Cos(a) * radius, 0f, centre.y + Mathf.Sin(a) * radius));
                if (i > 0) { tris.Add(0); tris.Add(i + 1); tris.Add(i); }
            }
            return Finish(verts, tris, "SinkholeWater");
        }

        private static Mesh Finish(List<Vector3> verts, List<int> tris, string name)
        {
            // Make every triangle face up (seen from the camera above), whatever order it was built in.
            for (int i = 0; i < tris.Count; i += 3)
            {
                var n = Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]);
                if (n.y < 0f) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var uv = new Vector2[verts.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(verts[i].x, verts[i].z) * 0.05f;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private const float BridgeDeckY = 0.6f;   // deck surface, level with the road on both banks

        private static void BuildBridges()
        {
            var g = Group("Bridges");
            foreach (var at in new[] { BridgeNorth, BridgeSouth })
            {
                // The bridge's length runs along its X, across the north-south river.
                var bridge = PlacePrefab(ExcavationAssetBuilder.PrefabDir + "P_EX_Env_Bridge.prefab", g, at, 0f);
                if (bridge == null) continue;
                // Drop it so the deck (found by a ray onto its mesh collider) sits at road level; the piers stand in the water.
                bridge.transform.position = new Vector3(at.x, 200f, at.y);
                Physics.SyncTransforms();
                float deck = Physics.Raycast(new Vector3(at.x, 400f, at.y), Vector3.down, out var hit, 500f) ? hit.point.y - 200f : 2f;
                bridge.transform.position = new Vector3(at.x, BridgeDeckY - deck, at.y);
            }
        }

        // ---------------------------------------------------------------- open-ground scatter (stage 5)

        /// <summary>Scatter: prefab, count, scale range. Kept off zones, roads, water, the dig and steep ground.</summary>
        private static readonly (string prefab, int count, float minScale, float maxScale)[] Scatter =
        {
            ("P_EX_Env_Boulder", 26, 0.7f, 1.4f),
            ("P_EX_Env_RockCluster", 20, 0.8f, 1.3f),
            ("P_EX_Env_RockSlab", 14, 0.8f, 1.3f),
            ("P_EX_Env_DeadTree", 18, 0.8f, 1.2f),
            ("P_EX_Env_Bush", 70, 0.7f, 1.3f),
            ("P_EX_Env_TruckWreck", 6, 1f, 1f),
            ("P_EX_Env_JunkPile", 12, 0.8f, 1.2f),
        };

        private static void BuildScatter()
        {
            var g = Group("Scatter");
            var rng = new System.Random(2026);
            var placed = new List<Vector3>();   // (x, z, radius)
            foreach (var (prefab, count, minS, maxS) in Scatter)
            {
                string path = ExcavationAssetBuilder.PrefabDir + prefab + ".prefab";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) { Debug.LogWarning("[ExcavationSiteBuilder] Missing " + path); continue; }
                var box = asset.GetComponent<BoxCollider>();
                float baseRadius = box != null ? Mathf.Max(box.size.x, box.size.z) * 0.5f : 1f;

                for (int n = 0, tries = 0; n < count && tries < count * 60; tries++)
                {
                    float x = (float)(rng.NextDouble() * 2 - 1) * (HalfW - 10f);
                    float z = (float)(rng.NextDouble() * 2 - 1) * (HalfD - 10f);
                    float s = Mathf.Lerp(minS, maxS, (float)rng.NextDouble());
                    float r = baseRadius * s;
                    if (!ScatterFree(x, z, r, placed)) continue;
                    var go = PlacePrefab(path, g, new Vector2(x, z), (float)rng.NextDouble() * 360f);
                    go.transform.localScale *= s;
                    // Sink a little so nothing hovers over uneven ground.
                    go.transform.position += Vector3.down * 0.15f;
                    placed.Add(new Vector3(x, z, r));
                    n++;
                }
            }

            // Leaning utility poles along the main road, a line of dead wires across the wasteland.
            var poles = Group("Poles", g);
            var road = Roads[0];
            for (int i = 0; i < road.Length - 1; i++)
            {
                Vector2 a = new Vector2(road[i].x, road[i].z), b = new Vector2(road[i + 1].x, road[i + 1].z);
                float len = Vector2.Distance(a, b);
                var dir = (b - a) / len;
                var side = new Vector2(-dir.y, dir.x) * (RoadWidth * 0.5f + 4f);
                for (float t = 12f; t < len; t += 28f)
                {
                    var p = a + dir * t + side;
                    if (ZoneDistance(p.x, p.y) < 6f || PolylineDistance(p.x, p.y, River) < RiverHalfWidth + RiverBank + 2f) continue;
                    float tilt = (float)(rng.NextDouble() * 2 - 1) * 6f;
                    var pole = PlacePrefab(ExcavationAssetBuilder.PrefabDir + "P_EX_Env_Pole.prefab", poles, p, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
                    if (pole != null) pole.transform.rotation *= Quaternion.Euler(tilt, 0f, 0f);
                }
            }
        }

        /// <summary>Open wasteland: inside the canyon, clear of zones, roads, water, the dig, the cave channel, slopes and other scatter.</summary>
        private static bool ScatterFree(float x, float z, float r, List<Vector3> placed)
        {
            if (RimDistance(x, z) > -18f - r) return false;
            if (ZoneDistance(x, z) < 10f + r) return false;
            if (RoadDistance(x, z) < 4f + r) return false;
            if (PolylineDistance(x, z, River) < RiverHalfWidth + RiverBank + 3f + r) return false;
            if (Vector2.Distance(new Vector2(x, z), Sinkhole) < SinkholeRadius + 8f + r) return false;
            if (Vector2.Distance(new Vector2(x, z), CaveMouth) < 70f) return false;
            var td = terrain.terrainData;
            var local = new Vector2((x - TerrainOrigin.x) / TerrainW, (z - TerrainOrigin.z) / TerrainD);
            if (td.GetSteepness(local.x, local.y) > 22f) return false;
            foreach (var q in placed)
                if ((new Vector2(q.x, q.y) - new Vector2(x, z)).sqrMagnitude < (q.z + r + 2f) * (q.z + r + 2f)) return false;
            return true;
        }

        // ---------------------------------------------------------------- military compound (stage 2, reshaped)

        private static readonly Vector2 MilitaryGate = new Vector2(60f, 110f);

        /// <summary>
        /// The compound's outline: an irregular octagon on the plateau (west edge straight for the gate), so the base reads
        /// as a fortified hilltop rather than a rectangle. Also shapes the plateau itself.
        /// </summary>
        public static readonly Vector2[] MilitaryOutline =
        {
            new Vector2(60f, 90f), new Vector2(60f, 128f), new Vector2(82f, 170f), new Vector2(150f, 182f), new Vector2(220f, 172f),
            new Vector2(248f, 120f), new Vector2(232f, 62f), new Vector2(160f, 42f), new Vector2(88f, 52f),
        };

        /// <summary>Hub of the compound: the bunker stands here, ringed by a dirt road; everything else faces it.</summary>
        private static readonly Vector2 MilitaryCentre = new Vector2(152f, 116f);
        private const float MilitaryRingRadius = 36f;

        /// <summary>Buildings and big pieces around the hub: prefab, angle (deg, 0 = east, CCW), distance from the hub. They face the hub.</summary>
        private static readonly (string prefab, float angle, float radius)[] MilitaryRing =
        {
            ("P_EX_Mil_CommandPost", 140f, 56f),
            ("P_EX_Mil_Barracks", 115f, 54f), ("P_EX_Mil_Barracks", 62f, 52f), ("P_EX_Mil_Barracks", 35f, 56f),
            ("P_EX_Mil_Barracks", -45f, 56f), ("P_EX_Mil_Barracks", -72f, 56f),
            ("P_EX_Mil_Garage", 10f, 60f), ("P_EX_Mil_Garage", -18f, 60f),
            ("P_EX_Mil_Tent", -100f, 54f), ("P_EX_Mil_Tent", -157f, 54f),
            ("P_EX_Lab_Helipad", -130f, 52f),
            ("P_EX_Lab_SatDish", 128f, 42f), ("P_EX_Mil_RadioMast", 80f, 40f), ("P_EX_Mil_Generator", 100f, 42f),
            ("P_EX_Mil_Floodlight", 45f, 42f), ("P_EX_Mil_Floodlight", 135f, 44f), ("P_EX_Mil_Floodlight", -45f, 42f), ("P_EX_Mil_Floodlight", -135f, 40f),
            ("P_EX_Mil_CamoNet", 0f, 44f), ("P_EX_Mil_AmmoCrates", -4f, 46f), ("P_EX_Mil_AmmoCrates", 4f, 42f),
            ("P_EX_Mil_TruckWreck", -60f, 42f), ("P_EX_Mil_TruckWreck", 152f, 43f),
            ("P_EX_Mil_AmmoCrates", 155f, 47f), ("P_EX_Mil_FuelDrums", 33f, 42f), ("P_EX_Mil_FuelDrums", -83f, 44f),
        };

        /// <summary>Fixed pieces: prefab, position, yaw (front faces -Z at yaw 0).</summary>
        private static readonly (string prefab, Vector2 pos, float yaw)[] MilitaryLayout =
        {
            // The bunker in the middle, its blast door facing south.
            ("P_EX_Mil_Bunker", new Vector2(152f, 122f), 0f),
            ("P_EX_Mil_Sandbags", new Vector2(140f, 98f), 0f), ("P_EX_Mil_Sandbags", new Vector2(164f, 98f), 0f),
            // Fuel farm along the north edge.
            ("P_EX_Mil_FuelTank", new Vector2(138f, 164f), 0f), ("P_EX_Mil_FuelTank", new Vector2(152f, 168f), 0f),
            ("P_EX_Mil_FuelTank", new Vector2(166f, 164f), 0f),
            // Gate guard posts.
            ("P_EX_Mil_Sandbags", new Vector2(68f, 96f), 90f), ("P_EX_Mil_Sandbags", new Vector2(68f, 124f), 90f),
            ("P_EX_Mil_Sandbags", new Vector2(88f, 104f), 0f),
        };

        // The environment vehicles were built at the settlement's 0.7 prop scale; real size here.
        private const float VehicleScale = 1.56f;
        private static readonly (string prefab, float angle, float radius, float yaw)[] MilitaryVehicles =
        {
            ("P_Wreck_BTR60", -30f, 44f, 75f), ("P_Wreck_BTR60", 25f, 44f, 110f), ("P_Wreck_BTR60", -112f, 40f, 30f),
            ("P_Wreck_Mi24", -130f, 52f, 200f),
        };

        private static Vector2 MilitaryPolar(float angle, float radius) =>
            MilitaryCentre + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;

        private static void BuildMilitary()
        {
            var g = Group("Military");
            // Perimeter: HESCO barrier walls and razor-wire fence on alternate sides, the gate gap on the west side.
            PolygonOutline(Group("Fence", g), MilitaryOutline, new[] { MilitaryGate }, 7.6f, i => i % 2 == 0 ? "P_EX_Mil_Barrier" : "P_EX_Mil_Fence");
            PlacePrefab(ExcavationAssetBuilder.PrefabDir + "P_EX_Mil_Gate.prefab", g, MilitaryGate, 90f);

            // A watchtower just inside every corner, ladder toward the hub.
            foreach (var c in MilitaryOutline)
            {
                var p = Vector2.MoveTowards(c, MilitaryCentre, 6f);
                PlacePrefab(ExcavationAssetBuilder.PrefabDir + "P_EX_Mil_Watchtower.prefab", g, p, YawFacing(MilitaryCentre - p));
            }

            foreach (var (prefab, angle, radius) in MilitaryRing)
            {
                var p = MilitaryPolar(angle, radius);
                PlacePrefab(ExcavationAssetBuilder.PrefabDir + prefab + ".prefab", g, p, YawFacing(MilitaryCentre - p));
            }
            foreach (var (prefab, pos, yaw) in MilitaryLayout)
                PlacePrefab(ExcavationAssetBuilder.PrefabDir + prefab + ".prefab", g, pos, yaw);
            foreach (var (prefab, angle, radius, yaw) in MilitaryVehicles)
            {
                var go = PlacePrefab("Assets/_Project/Prefabs/Environment/" + prefab + ".prefab", g, MilitaryPolar(angle, radius), yaw);
                if (go != null) go.transform.localScale *= VehicleScale;
            }
        }

        /// <summary>Signed distance to the compound outline (negative inside).</summary>
        private static float MilitaryDistance(Vector2 p)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = MilitaryOutline.Length - 1; i < MilitaryOutline.Length; j = i++)
            {
                Vector2 a = MilitaryOutline[j], b = MilitaryOutline[i];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
                if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x)) inside = !inside;
            }
            return inside ? -d : d;
        }

        /// <summary>Fence modules (6 m long) along a to b, stretched slightly so a whole number of them fills the run.</summary>
        private static void FenceLine(Transform parent, Vector2 a, Vector2 b, string fencePrefab)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1f) return;
            var dir = (b - a) / len;
            float piece = 6f * ExcavationAssetBuilder.WorldScale;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / piece));
            float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;   // the module's length runs along its X
            for (int i = 0; i < n; i++)
            {
                var go = PlacePrefab(ExcavationAssetBuilder.PrefabDir + fencePrefab + ".prefab", parent, a + dir * ((i + 0.5f) * len / n), yaw);
                if (go != null) go.transform.localScale = new Vector3(len / n / piece, 1f, 1f);
            }
        }

        /// <summary>Yaw that turns a model's front (-Z) toward the given direction.</summary>
        private static float YawFacing(Vector2 dir) => Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;

        private static GameObject PlacePrefab(string path, Transform parent, Vector2 xz, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[ExcavationSiteBuilder] Missing " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(xz.x, GroundY(xz), xz.y), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        private static float GroundY(Vector2 xz) => terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;

        // ---------------------------------------------------------------- blockout (stage 1)

        // ---------------------------------------------------------------- ruin excavation & researchers' camp (stage 3)

        /// <summary>The dig: ruins in the pit, sifting and finds on its floor, scaffolding by the cave, the crane on a terrace.</summary>
        private static readonly (string prefab, Vector2 pos, float yaw)[] DigLayout =
        {
            ("P_EX_Exc_RuinWall", new Vector2(-238f, 84f), 0f),
            ("P_EX_Exc_Statue", new Vector2(-256f, 114f), 330f),
            ("P_EX_Exc_Scaffolding", new Vector2(-291.8f, 119.4f), 11.4f),
            ("P_EX_Exc_Crane", new Vector2(-206f, 112f), 240f),
            ("P_EX_Exc_Sifting", new Vector2(-226f, 102f), 0f), ("P_EX_Exc_Sifting", new Vector2(-262f, 88f), 20f),
            ("P_EX_Exc_ArtifactCrates", new Vector2(-230f, 96f), 0f), ("P_EX_Exc_ArtifactCrates", new Vector2(-266f, 100f), 90f),
            ("P_EX_Exc_ArtifactCrates", new Vector2(-288.6f, 95.3f), 270f),
            ("P_EX_Exc_WorkLight", new Vector2(-294f, 96f), 270f), ("P_EX_Exc_WorkLight", new Vector2(-280.7f, 114.1f), 270f),
            ("P_EX_Exc_WorkLight", new Vector2(-250f, 76f), 0f), ("P_EX_Exc_WorkLight", new Vector2(-230f, 118f), 180f),
            ("P_EX_Exc_WorkLight", new Vector2(-270f, 80f), 20f),
            ("P_EX_Camp_Tent", new Vector2(-256f, 76f), 0f),
            ("P_EX_Mil_Generator", new Vector2(-268f, 112f), 90f),
            ("P_EX_Exc_RuinWall", new Vector2(-232f, 114f), 10f), ("P_EX_Exc_Scaffolding", new Vector2(-238f, 74f), 0f),
            ("P_EX_Exc_ArtifactCrates", new Vector2(-221f, 108f), 30f), ("P_EX_Exc_Sifting", new Vector2(-248f, 98f), 340f),
        };

        /// <summary>
        /// The camp around the road hub at (-155, 10): enterable dorms, labs and an office in the west and south sectors,
        /// research tents, storage containers, water, power and a campfire, kept clear of the roads.
        /// </summary>
        private static readonly (string prefab, Vector2 pos, float yaw)[] CampLayout =
        {
            ("P_EX_Camp_Dorm", new Vector2(-197f, 15f), 0f), ("P_EX_Camp_Dorm", new Vector2(-197f, -10f), 0f),
            ("P_EX_Camp_Lab", new Vector2(-197f, -38f), 0f), ("P_EX_Camp_Lab", new Vector2(-130f, -45f), 180f),
            ("P_EX_Camp_Office", new Vector2(-170f, -40f), 180f),
            ("P_EX_Camp_Tent", new Vector2(-120f, -28f), 0f), ("P_EX_Camp_Tent", new Vector2(-105f, -48f), 90f),
            ("P_EX_Camp_Tent", new Vector2(-135f, 45f), 0f), ("P_EX_Camp_Tent", new Vector2(-112f, 5f), 90f),
            ("P_EX_Camp_Container", new Vector2(-100f, 18f), 90f), ("P_EX_Camp_Container", new Vector2(-100f, 28f), 90f),
            ("P_EX_Camp_Container", new Vector2(-162f, 50f), 0f), ("P_EX_Camp_Container", new Vector2(-172f, 50f), 0f),
            ("P_EX_Camp_Campfire", new Vector2(-172f, -8f), 0f),
            ("P_EX_Camp_WaterTank", new Vector2(-182f, 26f), 0f), ("P_EX_Camp_WaterTank", new Vector2(-145f, -30f), 0f),
            ("P_EX_Mil_Generator", new Vector2(-183f, -25f), 90f), ("P_EX_Mil_Generator", new Vector2(-115f, -58f), 0f),
            ("P_EX_Mil_FuelDrums", new Vector2(-183f, -18f), 0f), ("P_EX_Mil_FuelDrums", new Vector2(-112f, -56f), 0f),
            ("P_EX_Exc_ArtifactCrates", new Vector2(-182f, -32f), 0f), ("P_EX_Exc_ArtifactCrates", new Vector2(-148f, -50f), 0f),
            ("P_EX_Exc_ArtifactCrates", new Vector2(-150f, 50f), 0f),
            ("P_EX_Exc_WorkLight", new Vector2(-160f, -20f), 0f), ("P_EX_Exc_WorkLight", new Vector2(-140f, 30f), 0f),
            ("P_EX_Exc_WorkLight", new Vector2(-192f, 40f), 0f), ("P_EX_Exc_WorkLight", new Vector2(-125f, -58f), 0f),
            // Filling out the north and west sectors.
            ("P_EX_Camp_Dorm", new Vector2(-160f, 35f), 0f),
            ("P_EX_Camp_Tent", new Vector2(-196f, 47f), 0f), ("P_EX_Camp_Tent", new Vector2(-200f, -55f), 0f),
            ("P_EX_Camp_Container", new Vector2(-210f, 30f), 90f), ("P_EX_Camp_Container", new Vector2(-160f, -57f), 0f),
            ("P_EX_Exc_Sifting", new Vector2(-108f, -32f), 0f), ("P_EX_Exc_ArtifactCrates", new Vector2(-135f, -30f), 0f),
            ("P_EX_Camp_Campfire", new Vector2(-118f, 52f), 0f), ("P_EX_Exc_WorkLight", new Vector2(-212f, 0f), 90f),
        };

        private static void BuildExcavation()
        {
            var g = Group("Excavation");

            // The cave facade's front stands on the mouth line, its body pushed back into the raised cliff.
            string cavePath = ExcavationAssetBuilder.PrefabDir + "P_EX_Exc_CaveEntrance.prefab";
            var cavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cavePath);
            if (cavePrefab != null)
            {
                float depth = cavePrefab.GetComponent<BoxCollider>().size.z;
                var cave = PlacePrefab(cavePath, g, CaveMouth - CaveFacing * (depth * 0.5f), YawFacing(CaveFacing));
                var pos = cave.transform.position;
                cave.transform.position = new Vector3(pos.x, PitFloor, pos.z);

                // Darkness filling the arch, so the cliff behind the facade reads as the cave's black interior.
                var dark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dark.name = "CaveDarkness";
                dark.transform.SetParent(g, false);
                Object.DestroyImmediate(dark.GetComponent<Collider>());
                var c = CaveMouth - CaveFacing * (CaveFacadeDepth + 3f);
                dark.transform.SetPositionAndRotation(new Vector3(c.x, PitFloor + 11f, c.y), Quaternion.LookRotation(new Vector3(CaveFacing.x, 0f, CaveFacing.y)));
                dark.transform.localScale = new Vector3(26f, 22f, 0.5f);
                dark.GetComponent<Renderer>().sharedMaterial = Mat("M_EX_CaveDark", new Color(0.02f, 0.015f, 0.01f), false);

                // A rock overhang laid over the facade's stone lintel (the terrain can't overhang the open arch), its
                // jagged lip just past the facade front, its back sunk into the cliff.
                float lintelTop = float.MinValue;
                foreach (var r in cave.GetComponentsInChildren<Renderer>()) lintelTop = Mathf.Max(lintelTop, r.bounds.max.y);
                var lip = CaveMouth - CaveFacing * (CaveFacadeDepth - 1.5f);
                var overhangPath = ExcavationAssetBuilder.PrefabDir + "P_EX_Env_CaveOverhang.prefab";
                var overhangAsset = AssetDatabase.LoadAssetAtPath<GameObject>(overhangPath);
                if (overhangAsset != null)
                {
                    float depth = overhangAsset.GetComponentInChildren<Renderer>().bounds.size.z;
                    var at = lip - CaveFacing * (depth * 0.5f);
                    var overhang = PlacePrefab(overhangPath, g, at, YawFacing(CaveFacing));
                    overhang.transform.position = new Vector3(at.x, lintelTop - CaveOverhangSink, at.y);
                }
            }
            else Debug.LogWarning("[ExcavationSiteBuilder] Missing " + cavePath);

            var dig = Group("Dig", g);
            foreach (var (prefab, pos, yaw) in DigLayout) PlacePrefab(ExcavationAssetBuilder.PrefabDir + prefab + ".prefab", dig, pos, yaw);
            var camp = Group("Camp", g);
            foreach (var (prefab, pos, yaw) in CampLayout) PlacePrefab(ExcavationAssetBuilder.PrefabDir + prefab + ".prefab", camp, pos, yaw);
        }

        // ---------------------------------------------------------------- laboratory complex (stage 4)

        // The lab road comes in at the north gate, the south-bridge road at the west gate.
        private static readonly Vector2 LabNorthGate = new Vector2(60f, -45f);
        private static readonly Vector2 LabWestGate = new Vector2(30f, -100f);

        /// <summary>
        /// The main lab (exterior only) faces a central courtyard; enterable aux labs and the dormitory line the west side,
        /// the warehouse and greenhouse the south, the radar, dish and helipad the east.
        /// </summary>
        private static readonly (string prefab, Vector2 pos, float yaw)[] LabLayout =
        {
            ("P_EX_Lab_MainLab", new Vector2(115f, -80f), 0f),
            ("P_EX_Lab_RadarDome", new Vector2(163f, -66f), 0f),
            ("P_EX_Lab_SatDish", new Vector2(180f, -84f), 330f),
            ("P_EX_Lab_Helipad", new Vector2(160f, -105f), 0f),
            ("P_EX_Lab_AuxLab", new Vector2(55f, -85f), 270f), ("P_EX_Lab_AuxLab", new Vector2(168f, -135f), 90f),
            ("P_EX_Lab_Dormitory", new Vector2(55f, -150f), 270f),
            ("P_EX_Lab_Warehouse", new Vector2(110f, -158f), 180f),
            ("P_EX_Lab_Greenhouse", new Vector2(155f, -162f), 180f),
            ("P_EX_Lab_Checkpoint", LabNorthGate, 0f), ("P_EX_Lab_Checkpoint", LabWestGate, 90f),
            ("P_EX_Lab_Transformer", new Vector2(82f, -120f), 90f), ("P_EX_Lab_Transformer", new Vector2(146f, -72f), 0f),
            ("P_EX_Mil_Generator", new Vector2(84f, -129f), 90f), ("P_EX_Mil_FuelDrums", new Vector2(88f, -135f), 0f),
            ("P_EX_Lab_HazardDrums", new Vector2(96f, -150f), 0f), ("P_EX_Lab_HazardDrums", new Vector2(124f, -146f), 30f),
            ("P_EX_Lab_HazardDrums", new Vector2(132f, -164f), 0f),
            ("P_EX_Camp_WaterTank", new Vector2(140f, -160f), 0f),
            ("P_EX_Lab_LampPost", new Vector2(80f, -105f), 90f), ("P_EX_Lab_LampPost", new Vector2(146f, -112f), 270f),
            ("P_EX_Lab_LampPost", new Vector2(80f, -142f), 90f), ("P_EX_Lab_LampPost", new Vector2(140f, -142f), 270f),
            ("P_EX_Lab_LampPost", new Vector2(70f, -58f), 0f), ("P_EX_Lab_LampPost", new Vector2(42f, -92f), 90f),
            ("P_EX_Lab_LampPost", new Vector2(95f, -170f), 180f),
            // Filling out the compound: a third aux lab, decontamination tents, storage containers, gate sandbags.
            ("P_EX_Lab_AuxLab", new Vector2(85f, -152f), 0f),
            ("P_EX_Camp_Tent", new Vector2(70f, -105f), 0f), ("P_EX_Camp_Tent", new Vector2(135f, -125f), 0f),
            ("P_EX_Camp_Container", new Vector2(178f, -150f), 90f), ("P_EX_Camp_Container", new Vector2(178f, -160f), 90f),
            ("P_EX_Camp_Container", new Vector2(40f, -120f), 90f), ("P_EX_Camp_Container", new Vector2(40f, -130f), 90f),
            ("P_EX_Mil_Sandbags", new Vector2(50f, -52f), 0f), ("P_EX_Mil_Sandbags", new Vector2(70f, -52f), 0f),
            ("P_EX_Mil_Sandbags", new Vector2(37f, -90f), 90f), ("P_EX_Mil_Sandbags", new Vector2(37f, -110f), 90f),
            ("P_EX_Lab_HazardDrums", new Vector2(155f, -142f), 0f), ("P_EX_Lab_HazardDrums", new Vector2(88f, -97f), 60f),
            ("P_EX_Mil_AmmoCrates", new Vector2(125f, -135f), 0f),
        };

        private static void BuildLab()
        {
            var g = Group("Laboratory");
            FenceOutline(Group("Fence", g), LabArea, new[] { LabNorthGate, LabWestGate }, 7.6f, "P_EX_Lab_Fence");
            foreach (var (prefab, pos, yaw) in LabLayout) PlacePrefab(ExcavationAssetBuilder.PrefabDir + prefab + ".prefab", g, pos, yaw);
            // The research helicopter that never took off again, crashed beside its pad.
            var wreck = PlacePrefab("Assets/_Project/Prefabs/Environment/P_Wreck_Mi17.prefab", g, new Vector2(158f, -100f), 35f);
            if (wreck != null) wreck.transform.localScale *= VehicleScale;
            // An armoured car that made it to the main lab's doors and no further.
            var btr = PlacePrefab("Assets/_Project/Prefabs/Environment/P_Wreck_BTR80.prefab", g, new Vector2(100f, -112f), 20f);
            if (btr != null) btr.transform.localScale *= VehicleScale;
        }

        /// <summary>Runs fence modules around the area, leaving a gap of gapHalf either side of each gate.</summary>
        private static void FenceOutline(Transform g, Rect area, Vector2[] gates, float gapHalf, string fencePrefab)
        {
            Vector2[] c = { new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin), new Vector2(area.xMax, area.yMax), new Vector2(area.xMin, area.yMax) };
            PolygonOutline(g, c, gates, gapHalf, i => fencePrefab);
        }

        /// <summary>Runs wall/fence modules (prefab chosen per side) around a closed outline, with gaps at the gates.</summary>
        private static void PolygonOutline(Transform g, Vector2[] c, Vector2[] gates, float gapHalf, System.Func<int, string> sidePrefab)
        {
            for (int i = 0; i < c.Length; i++)
            {
                Vector2 a = c[i], b = c[(i + 1) % c.Length];
                string fencePrefab = sidePrefab(i);
                float len = Vector2.Distance(a, b);
                var dir = (b - a) / len;
                // Gates on this side, in order along it.
                var cuts = new List<float>();
                foreach (var gate in gates)
                {
                    float t = Vector2.Dot(gate - a, dir);
                    float off = Mathf.Abs(dir.x * (gate.y - a.y) - dir.y * (gate.x - a.x));
                    if (off < 2f && t > 0f && t < len) cuts.Add(t);
                }
                cuts.Sort();
                float from = 0f;
                foreach (float t in cuts)
                {
                    FenceLine(g, a + dir * from, a + dir * (t - gapHalf), fencePrefab);
                    from = t + gapHalf;
                }
                FenceLine(g, a + dir * from, b, fencePrefab);
            }
        }

        private static IEnumerable<Vector2> Corners(Rect r)
        {
            yield return new Vector2(r.xMin, r.yMin); yield return new Vector2(r.xMax, r.yMin);
            yield return new Vector2(r.xMax, r.yMax); yield return new Vector2(r.xMin, r.yMax);
        }

        // ---------------------------------------------------------------- helpers

        private static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : root, false);
            return t;
        }

        /// <summary>Grey blockout box standing on the ground at (xz), or at baseY above the ground when given.</summary>
        private static GameObject Block(string name, Transform parent, Vector2 xz, Vector3 size, float yaw, float baseY = float.NaN)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            float y = float.IsNaN(baseY) ? Height(xz.x, xz.y) : baseY;
            go.transform.SetPositionAndRotation(new Vector3(xz.x, y + size.y * 0.5f, xz.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = blockMat;
            return go;
        }

        private static TerrainLayer Layer(string name, string diffuse, string normal, float tile)
        {
            string path = MatDir + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            if (normalTex != null && AssetImporter.GetAtPath(normal) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            layer.normalMapTexture = normalTex;
            layer.tileSize = new Vector2(tile, tile);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Material Mat(string name, Color color, bool transparent)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", transparent ? 0.85f : 0.1f);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
