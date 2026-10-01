using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MobileMarket.EditorTools
{
    /// <summary>Regenerates the "Radrock Gate" settlement layout in the open scene from the reference floor plan.</summary>
    public static class SettlementBuilder
    {
        private const string RootName = "Settlement";
        private const string RU = "Assets/@ExternalAssets/kenney_retro-urban-kit/Models/FBX format/";
        private const string SV = "Assets/@ExternalAssets/kenney_survival-kit/Models/FBX format/";
        private const string IN = "Assets/@ExternalAssets/kenney_city-kit-industrial/Models/FBX format/";
        private const string MatFolder = "Assets/_Project/Materials/";

        // One kenney unit = 3m in this map.
        private const float M = 3f;

        // Reference image: 1536px wide, map 1 occupies y 0..510, scale bar 50m = 210px.
        private const float PxToM = 0.238f;

        private static System.Random rng;
        private static Transform root;
        private static readonly Dictionary<string, GameObject> modelCache = new Dictionary<string, GameObject>();
        private static readonly List<Rect> occupied = new List<Rect>();

        private static Material sandMat, packedMat, dirtMat, roofGrey, roofGreen, roofTeal, roofYellow, roofRust, roofRed, beamMat;

        private static Vector3 P(float px, float py) => new Vector3((px - 768f) * PxToM, 0f, (255f - py) * PxToM);

        [MenuItem("MobileMarket/Build Small Settlement")]
        public static void Build()
        {
            rng = new System.Random(1234);
            modelCache.Clear();
            occupied.Clear();

            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);
            root = new GameObject(RootName).transform;

            CreateMaterials();
            BuildGround();
            BuildPerimeter();
            BuildGate();
            BuildBuildings();
            BuildProps();
            BuildVegetation();

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[SettlementBuilder] Built settlement with {root.GetComponentsInChildren<Transform>().Length} objects.");
        }

        // ---------------------------------------------------------------- VARCO variant

        private const string VA = "Assets/_Project/Models/Settlement/";

        /// <summary>Same layout as Build(), but buildings, wall, gate and hero props use the VARCO-generated models.</summary>
        [MenuItem("MobileMarket/Build Small Settlement (VARCO)")]
        public static void BuildVarco()
        {
            rng = new System.Random(1234);
            modelCache.Clear();
            occupied.Clear();

            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);
            root = new GameObject(RootName).transform;

            CreateMaterials();
            BuildGround();
            BuildVarcoPerimeter();
            BuildVarcoBuildings();
            BuildVarcoProps();
            BuildDesertGroundTiles();
            BuildVegetation(true);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[SettlementBuilder] Built VARCO settlement with {root.GetComponentsInChildren<Transform>().Length} objects.");
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i == 0) b = renderers[i].bounds; else b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        /// <summary>
        /// Instantiates a VARCO model under an identity wrapper. The FBX root carries its own axis-conversion rotation,
        /// so all placement (yaw, scale, position) is applied to the wrapper instead of the model.
        /// </summary>
        private static Transform Wrap(GameObject prefab, Transform parent)
        {
            var wrapper = new GameObject(prefab.name).transform;
            wrapper.SetParent(parent, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper);
            inst.transform.localPosition = Vector3.zero;
            return wrapper;
        }

        /// <summary>Places a VARCO model so its footprint fits width x depth (long axes matched), grounded at y=0, with a box collider.</summary>
        private static GameObject PlaceFit(string model, Transform parent, Vector3 center, float width, float depth, float yaw, bool collider = true)
        {
            var prefab = Model(VA + model + ".fbx");
            if (prefab == null) return null;
            var w = Wrap(prefab, parent);
            var native = RendererBounds(w.gameObject).size;

            bool swap = (width >= depth) != (native.x >= native.z);
            float sx = swap ? native.z : native.x;
            float sz = swap ? native.x : native.z;
            float s = Mathf.Min(width / sx, depth / sz);

            w.localScale = Vector3.one * s;
            w.rotation = Quaternion.Euler(0f, yaw + (swap ? 90f : 0f), 0f);
            var b = RendererBounds(w.gameObject);
            w.position = new Vector3(center.x - b.center.x, -b.min.y, center.z - b.center.z);

            if (collider) AddBoundsCollider(w.gameObject);
            MarkOccupied(center, Mathf.Max(width, depth), Mathf.Max(width, depth));
            return w.gameObject;
        }

        private static void BuildVarcoPerimeter()
        {
            var g = Group("PerimeterWall");
            VarcoWall(g, new[] { P(45, 365), P(690, 365) });
            VarcoWall(g, new[] { P(805, 365), P(1230, 372), P(1310, 358), P(1380, 330), P(1435, 285), P(1462, 220), P(1466, 125) });
            VarcoWall(g, new[] { P(45, 365), P(45, 100), P(655, 100) });
            VarcoWall(g, new[] { P(960, 60), P(1030, 60) });
            VarcoWall(g, new[] { P(1200, 80), P(1240, 80) });

            var gate = Group("Gate");
            var mid = (P(690, 365) + P(805, 365)) * 0.5f;
            PlaceFit("Settle_Gate", gate, mid, 38f, 9f, 0f, false);
            // Huts block, the road gap between them stays open.
            foreach (var side in new[] { -1f, 1f })
            {
                var col = new GameObject("HutCollider");
                col.transform.SetParent(gate, false);
                col.transform.position = mid + new Vector3(side * 15.5f, 3f, 0f);
                col.AddComponent<BoxCollider>().size = new Vector3(7f, 6f, 8f);
            }
        }

        private static void VarcoWall(Transform parent, Vector3[] points)
        {
            var prefab = Model(VA + "Settle_Wall.fbx");
            if (prefab == null) return;

            var probe = Wrap(prefab, null);
            var nb = RendererBounds(probe.gameObject);
            Object.DestroyImmediate(probe.gameObject);
            bool longX = nb.size.x >= nb.size.z;
            float nativeLen = longX ? nb.size.x : nb.size.z;
            float nativeH = nb.size.y;
            const float segLen = 6f, wallH = 3.5f;

            var seg = Group("VarcoWall", parent);
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = points[i]; var b = points[i + 1];
                var d = b - a; float len = d.magnitude; d /= len;
                int count = Mathf.Max(1, Mathf.RoundToInt(len / segLen));
                float step = len / count;
                float yaw = longX ? Quaternion.LookRotation(Vector3.Cross(Vector3.up, d)).eulerAngles.y
                                  : Quaternion.LookRotation(d).eulerAngles.y;
                for (int k = 0; k < count; k++)
                {
                    var w = Wrap(prefab, seg);
                    float s = step / nativeLen;
                    w.localScale = new Vector3(s, wallH / nativeH, s);
                    w.rotation = Quaternion.Euler(0f, yaw + (k % 2 == 1 ? 180f : 0f), 0f);
                    var wb = RendererBounds(w.gameObject);
                    var mid = a + d * (step * (k + 0.5f));
                    w.position = new Vector3(mid.x - wb.center.x, -wb.min.y, mid.z - wb.center.z);
                }

                var col = new GameObject("Collider");
                col.transform.SetParent(seg, false);
                col.transform.position = (a + b) * 0.5f + Vector3.up * 2f;
                col.transform.rotation = Quaternion.LookRotation(d);
                col.AddComponent<BoxCollider>().size = new Vector3(0.8f, 4f, len);
            }
        }

        private static void BuildVarcoBuildings()
        {
            var g = Group("Buildings");

            PlaceFit("Settle_Warehouse", g, P(145, 212), 34f, 37f, 0f);
            PlaceFit("Settle_MainHall", g, P(530, 190), 55f, 42f, 0f);
            PlaceFit("Settle_Barracks", g, P(525, 290), 51f, 15f, 0f);
            PlaceFit("Settle_Barracks", g, P(805, 70), 72f, 18f, 180f);
            PlaceFit("Settle_Barracks", g, P(1115, 55), 39f, 18f, 0f);
            PlaceFit("Settle_Barracks", g, P(1355, 90), 54f, 15f, 14f);
            PlaceFit("Settle_TealHall", g, P(958, 252), 24f, 22f, 0f);
            PlaceFit("Settle_Factory", g, P(1240, 245), 66f, 37f, 0f);

            // Shacks: sheds, shops and outbuildings, varied facing.
            var shacks = new (float px, float py, float w, float d, float yaw)[]
            {
                (265, 305, 9, 12, 90), (295, 105, 9, 6, 0), (310, 150, 9, 9, 180), (360, 220, 9, 15, 90), (380, 110, 6, 6, 0),
                (690, 185, 12, 15, 270), (705, 245, 15, 9, 180), (1075, 235, 12, 33, 90), (1415, 205, 9, 18, 270),
                (1030, 205, 6, 9, 0), (645, 360, 15, 12, 180), (890, 368, 24, 12, 180), (1060, 372, 15, 9, 180)
            };
            foreach (var s in shacks) PlaceFit("Settle_Shack", g, P(s.px, s.py), s.w, s.d, s.yaw);

            PlaceFit("Settle_Watchtower", g, P(1140, 140), 7f, 7f, 0f);
            PlaceFit("Settle_Watchtower", g, P(820, 125), 7f, 7f, 45f);

            var market = Group("Market", g);
            for (int i = 0; i < 6; i++) PlaceFit("Settle_Stall", market, P(430 + i * 38, 330), 5f, 4f, 180f);
        }

        private static void BuildVarcoProps()
        {
            var g = Group("Props");
            PlaceFit("Settle_WaterTank", g, P(695, 125), 7f, 7f, 0f);
            PlaceFit("Settle_WaterTank", g, P(1395, 290), 8f, 8f, 30f);
            PlaceFit("Settle_Car", g, P(625, 455), 5f, 2.4f, -60f);
            PlaceFit("Settle_Car", g, P(1270, 415), 5f, 2.4f, 20f);

            Prop(g, RU + "detail-barrier-strong-damaged.fbx", P(665, 445), 10f, M * 1.5f, true);
            Prop(g, RU + "detail-barrier-strong-type-a.fbx", P(790, 450), -5f, M * 1.5f, true);
            Prop(g, RU + "detail-barrier-strong-type-b.fbx", P(850, 445), 5f, M * 1.5f, true);
            Prop(g, IN + "shipping-container-a.fbx", P(1000, 320), 90f, 7.3f, true);
            Prop(g, IN + "shipping-container-c.fbx", P(250, 340), 0f, 7.3f, true);

            Cluster(g, P(240, 210), 10, 8f);
            Cluster(g, P(820, 200), 8, 7f);
            Cluster(g, P(1020, 150), 8, 6f);
            Cluster(g, P(1410, 320), 8, 6f);
            Cluster(g, P(560, 345), 8, 8f);
            Cluster(g, P(940, 330), 6, 6f);
            Cluster(g, P(380, 330), 6, 6f);
        }

        // ---------------------------------------------------------------- materials

        private static void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/@ExternalAssets/Cartoon_Texture_Pack/SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png")
                ?? FindTexture("Sand_Beach_Base_Basecolor");
            var dirtTex = FindTexture("Dirt_Path_Basecolor");
            var roofTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RU + "Textures/roof.png");
            var plateTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RU + "Textures/roof_plates.png");

            sandMat = GetOrCreateMat("M_Settle_Sand", lit, sandTex, new Color(1f, 0.93f, 0.8f));
            packedMat = GetOrCreateMat("M_Settle_PackedSand", lit, sandTex, new Color(0.88f, 0.76f, 0.6f));
            dirtMat = GetOrCreateMat("M_Settle_Dirt", lit, dirtTex, new Color(1.35f, 1.2f, 1f));
            roofGrey = GetOrCreateMat("M_Settle_RoofGrey", lit, roofTex, new Color(0.75f, 0.75f, 0.78f));
            roofGreen = GetOrCreateMat("M_Settle_RoofGreen", lit, roofTex, new Color(0.45f, 0.68f, 0.52f));
            roofTeal = GetOrCreateMat("M_Settle_RoofTeal", lit, roofTex, new Color(0.35f, 0.7f, 0.68f));
            roofYellow = GetOrCreateMat("M_Settle_RoofYellow", lit, plateTex, new Color(0.85f, 0.75f, 0.4f));
            roofRust = GetOrCreateMat("M_Settle_RoofRust", lit, roofTex, new Color(0.8f, 0.48f, 0.32f));
            roofRed = GetOrCreateMat("M_Settle_RoofRed", lit, roofTex, new Color(0.85f, 0.4f, 0.4f));
            beamMat = GetOrCreateMat("M_Settle_Beam", lit, plateTex, new Color(0.6f, 0.35f, 0.25f));
        }

        private static Texture2D FindTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (tex != null && tex.name == name) return tex;
            }
            return null;
        }

        private static Material GetOrCreateMat(string name, Shader shader, Texture2D tex, Color tint)
        {
            string path = MatFolder + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- helpers

        private static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            return go.transform;
        }

        private static GameObject Model(string path)
        {
            if (!modelCache.TryGetValue(path, out var go))
            {
                go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                modelCache[path] = go;
                if (go == null) Debug.LogWarning("[SettlementBuilder] Missing model " + path);
            }
            return go;
        }

        private static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var prefab = Model(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = prefab.transform.localScale * scale;
            return go;
        }

        private static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        private static T Pick<T>(T[] items) => items[rng.Next(items.Length)];

        /// <summary>Axis-aligned box mesh whose UVs repeat every tileSize meters, so textures keep a constant density.</summary>
        private static Mesh BoxMesh(Vector3 size, float tileSize)
        {
            var h = size * 0.5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 c, Vector3 u, Vector3 v, float uLen, float vLen)
            {
                int i = verts.Count;
                verts.Add(c - u - v); verts.Add(c + u - v); verts.Add(c + u + v); verts.Add(c - u + v);
                float su = uLen / tileSize, sv = vLen / tileSize;
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(su, 0)); uvs.Add(new Vector2(su, sv)); uvs.Add(new Vector2(0, sv));
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Face(new Vector3(0, h.y, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), size.x, size.z);   // top
            Face(new Vector3(0, -h.y, 0), new Vector3(-h.x, 0, 0), new Vector3(0, 0, h.z), size.x, size.z); // bottom
            Face(new Vector3(0, 0, h.z), new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0), size.x, size.y);  // north
            Face(new Vector3(0, 0, -h.z), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), size.x, size.y);  // south
            Face(new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), new Vector3(0, h.y, 0), size.z, size.y);   // east
            Face(new Vector3(-h.x, 0, 0), new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0), size.z, size.y); // west
            var mesh = new Mesh { name = "SettleBox" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject Slab(string name, Transform parent, Vector3 center, Vector3 size, float yaw, Material mat, float tile, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        private static void MarkOccupied(Vector3 center, float w, float d, float pad = 2f)
        {
            occupied.Add(new Rect(center.x - w * 0.5f - pad, center.z - d * 0.5f - pad, w + pad * 2f, d + pad * 2f));
        }

        private static bool IsFree(Vector3 p)
        {
            foreach (var r in occupied) if (r.Contains(new Vector2(p.x, p.z))) return false;
            return true;
        }

        // ---------------------------------------------------------------- ground

        private const string DE = "Assets/@ExternalAssets/Low Poly Desert Environment/Prefabs/";
        private static readonly List<Rect> groundExclusions = new List<Rect>();

        private static void BuildGround()
        {
            groundExclusions.Clear();
            var g = Group("Ground");
            var ground = Slab("Sand", g, new Vector3(0f, -0.5f, -10f), new Vector3(440f, 1f, 200f), 0f, sandMat, 8f, true);
            ground.isStatic = true;

            // Packed dirt inside the compound and along the roads (thin, stacked slightly to avoid z-fighting).
            float y = 0.01f;
            void Dirt(float px0, float py0, float px1, float py1, Material mat)
            {
                var a = P(px0, py0); var b = P(px1, py1);
                var c = (a + b) * 0.5f;
                var size = new Vector3(Mathf.Abs(b.x - a.x), 0.02f, Mathf.Abs(b.z - a.z));
                Slab("Dirt", g, new Vector3(c.x, y, c.z), size, 0f, mat, 10f, false);
                groundExclusions.Add(new Rect(c.x - size.x * 0.5f, c.z - size.z * 0.5f, size.x, size.z));
                y += 0.005f;
            }
            Dirt(240, 95, 1460, 360, packedMat);   // compound interior
            Dirt(50, 100, 240, 365, packedMat);    // warehouse yard
            Dirt(715, 360, 785, 520, dirtMat);     // gate road south
            Dirt(470, 440, 715, 480, dirtMat);     // road branch west
            Dirt(785, 425, 1120, 465, dirtMat);    // road branch east
            Dirt(715, 250, 785, 365, dirtMat);     // road from gate to the plaza
        }

        // ---------------------------------------------------------------- perimeter

        private static readonly string[] scrapPanels =
        {
            RU + "wall-b-flat.fbx", RU + "wall-b-flat.fbx", RU + "wall-a-flat-painted.fbx",
            RU + "wall-a-flat.fbx", RU + "wall-b-flat-garage.fbx", SV + "metal-panel-screws.fbx"
        };

        private static void BuildPerimeter()
        {
            var g = Group("PerimeterWall");

            // South scrap wall (west part, east part with curve up the east side). Gate gap is 690..805px.
            ScrapWall(g, new[] { P(45, 365), P(690, 365) });
            ScrapWall(g, new[] { P(805, 365), P(1230, 372), P(1310, 358), P(1380, 330), P(1435, 285), P(1462, 220), P(1466, 125) });

            // West + north fences around the warehouse yard and between north buildings.
            Fence(g, new[] { P(45, 365), P(45, 100), P(655, 100) });
            Fence(g, new[] { P(960, 60), P(1030, 60) });
            Fence(g, new[] { P(1200, 80), P(1240, 80) });
        }

        private static void ScrapWall(Transform parent, Vector3[] points)
        {
            var seg = Group("ScrapWall", parent);
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = points[i]; var b = points[i + 1];
                var d = (b - a); float len = d.magnitude; d /= len;
                var outward = Vector3.Cross(Vector3.up, d); // right-hand side of travel = outside (south/east)
                int count = Mathf.Max(1, Mathf.RoundToInt(len / M));
                float step = len / count;
                for (int k = 0; k < count; k++)
                {
                    var mid = a + d * (step * (k + 0.5f));
                    float height = rng.NextDouble() < 0.25 ? 2f : 1f;
                    for (int h = 0; h < height; h++)
                    {
                        PlacePanel(Pick(scrapPanels), seg, mid + Vector3.up * (h * M) + outward * 0.05f, outward, step);
                        PlacePanel(Pick(scrapPanels), seg, mid + Vector3.up * (h * M) - outward * 0.05f, -outward, step);
                    }
                }

                var col = new GameObject("Collider");
                col.transform.SetParent(seg, false);
                col.transform.position = (a + b) * 0.5f + Vector3.up * 2f;
                col.transform.rotation = Quaternion.LookRotation(d);
                var box = col.AddComponent<BoxCollider>();
                box.size = new Vector3(0.6f, 4f, len);
            }
        }

        private static void Fence(Transform parent, Vector3[] points)
        {
            var seg = Group("Fence", parent);
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = points[i]; var b = points[i + 1];
                var d = (b - a); float len = d.magnitude; d /= len;
                int count = Mathf.Max(1, Mathf.RoundToInt(len / M));
                float step = len / count;
                float yaw = Quaternion.LookRotation(Vector3.Cross(Vector3.up, d)).eulerAngles.y;
                for (int k = 0; k < count; k++)
                {
                    var mid = a + d * (step * (k + 0.5f));
                    var piece = rng.NextDouble() < 0.7 ? RU + "wall-c-flat.fbx" : RU + "wall-fence.fbx";
                    var go = Place(piece, seg, mid, yaw, M);
                    if (go != null) go.transform.localScale = new Vector3(step, M, M);
                }

                var col = new GameObject("Collider");
                col.transform.SetParent(seg, false);
                col.transform.position = (a + b) * 0.5f + Vector3.up * 1.5f;
                col.transform.rotation = Quaternion.LookRotation(d);
                col.AddComponent<BoxCollider>().size = new Vector3(0.4f, 3f, len);
            }
        }

        /// <summary>Single-sided kenney flat panel whose visible face points along "facing".</summary>
        private static void PlacePanel(string path, Transform parent, Vector3 basePos, Vector3 facing, float width)
        {
            var prefab = Model(path);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = basePos;
            go.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            bool survival = path.StartsWith(SV);
            float baseW = survival ? 0.5f : 1f;
            go.transform.localScale = new Vector3(width / baseW, M / baseW, M / baseW);
        }

        // ---------------------------------------------------------------- gate

        private static void BuildGate()
        {
            var g = Group("Gate");
            var left = P(690, 365);
            var right = P(805, 365);
            Building(g, "GuardHutWest", left + new Vector3(-3f, 0, 0), 6, 6, 2, 0f, WallStyle.Metal, roofRust);
            Building(g, "GuardHutEast", right + new Vector3(3f, 0, 0), 6, 6, 2, 0f, WallStyle.Metal, roofRust);

            // Sign beam across the gate.
            var mid = (left + right) * 0.5f;
            Slab("GateBeam", g, mid + Vector3.up * 7f, new Vector3(right.x - left.x, 1.2f, 0.8f), 0f, beamMat, 2f, false);
            Place(RU + "detail-barrier-type-a.fbx", g, mid + new Vector3(-6f, 0, 0), 0f, M);
            Place(RU + "detail-barrier-type-b.fbx", g, mid + new Vector3(6f, 0, 0), 0f, M);
        }

        // ---------------------------------------------------------------- buildings

        private enum WallStyle { Brick, Metal, Painted, Mixed }

        private static readonly Dictionary<WallStyle, string[]> groundPanels = new Dictionary<WallStyle, string[]>
        {
            { WallStyle.Brick, new[] { RU + "wall-a-flat.fbx", RU + "wall-a-flat.fbx", RU + "wall-a-flat-window.fbx", RU + "wall-a-flat-garage.fbx" } },
            { WallStyle.Metal, new[] { RU + "wall-b-flat.fbx", RU + "wall-b-flat.fbx", RU + "wall-b-flat-window.fbx", RU + "wall-b-flat-garage.fbx" } },
            { WallStyle.Painted, new[] { RU + "wall-a-flat-painted.fbx", RU + "wall-a-flat-window.fbx", RU + "wall-a-flat-painted.fbx", RU + "wall-a-flat-garage.fbx" } },
            { WallStyle.Mixed, new[] { RU + "wall-b-flat.fbx", RU + "wall-a-flat-painted.fbx", RU + "wall-b-flat-window.fbx", SV + "metal-panel-screws.fbx", RU + "wall-a-flat-window.fbx" } },
        };

        private static readonly Dictionary<WallStyle, string[]> upperPanels = new Dictionary<WallStyle, string[]>
        {
            { WallStyle.Brick, new[] { RU + "wall-a-flat.fbx", RU + "wall-a-flat-window.fbx" } },
            { WallStyle.Metal, new[] { RU + "wall-b-flat.fbx", RU + "wall-b-flat-window.fbx" } },
            { WallStyle.Painted, new[] { RU + "wall-a-flat-painted.fbx", RU + "wall-a-flat-window.fbx" } },
            { WallStyle.Mixed, new[] { RU + "wall-b-flat.fbx", RU + "wall-a-flat-painted.fbx", SV + "metal-panel-screws.fbx" } },
        };

        /// <summary>Closed, non-enterable building shell: panel walls on all four sides, corrugated slab roof, one box collider.</summary>
        private static Transform Building(Transform parent, string name, Vector3 center, float width, float depth, int stories, float yaw, WallStyle style, Material roof)
        {
            var b = Group(name, parent);
            b.position = center;
            b.rotation = Quaternion.Euler(0f, yaw, 0f);

            int nx = Mathf.Max(1, Mathf.RoundToInt(width / M));
            int nz = Mathf.Max(1, Mathf.RoundToInt(depth / M));
            float w = nx * M, d = nz * M, h = stories * M;

            var rot = b.rotation;
            var right = rot * Vector3.right;
            var fwd = rot * Vector3.forward;

            void Side(Vector3 start, Vector3 dir, Vector3 outward, int count)
            {
                for (int k = 0; k < count; k++)
                {
                    var basePos = start + dir * (M * (k + 0.5f));
                    for (int s = 0; s < stories; s++)
                    {
                        var set = s == 0 ? groundPanels[style] : upperPanels[style];
                        PlacePanel(Pick(set), b, basePos + Vector3.up * (s * M), outward, M);
                    }
                }
            }

            var c = center;
            Side(c - right * (w * 0.5f) - fwd * (d * 0.5f), right, -fwd, nx);   // south
            Side(c + right * (w * 0.5f) + fwd * (d * 0.5f), -right, fwd, nx);   // north
            Side(c + right * (w * 0.5f) - fwd * (d * 0.5f), fwd, right, nz);    // east
            Side(c - right * (w * 0.5f) + fwd * (d * 0.5f), -fwd, -right, nz);  // west

            Slab("Roof", b, c + Vector3.up * (h + 0.2f), new Vector3(w + 1f, 0.4f, d + 1f), yaw, roof, 3f, false);

            var col = b.gameObject.AddComponent<BoxCollider>();
            col.center = Quaternion.Inverse(rot) * (Vector3.up * (h * 0.5f));
            col.size = new Vector3(w, h, d);

            MarkOccupied(center, Mathf.Max(w, d), Mathf.Max(w, d));
            return b;
        }

        private static void BuildBuildings()
        {
            var g = Group("Buildings");

            // West warehouse + annex.
            Building(g, "Warehouse", P(145, 212), 33, 36, 2, 0f, WallStyle.Brick, roofGrey);
            Building(g, "WarehouseAnnex", P(265, 305), 9, 12, 1, 0f, WallStyle.Metal, roofGrey);

            // Small sheds west of the main hall.
            Building(g, "ShedA", P(295, 105), 9, 6, 1, 0f, WallStyle.Metal, roofRust);
            Building(g, "ShedB", P(310, 150), 9, 9, 1, 0f, WallStyle.Mixed, roofGrey);
            Building(g, "ShedC", P(360, 220), 9, 15, 1, 0f, WallStyle.Metal, roofGrey);
            Building(g, "ShedD", P(380, 110), 6, 6, 1, 0f, WallStyle.Painted, roofRed);

            // Main hall complex (green roofs) with a raised central hall.
            Building(g, "MainHallWestWing", P(460, 175), 18, 33, 2, 0f, WallStyle.Brick, roofGreen);
            Building(g, "MainHallEastWing", P(605, 175), 21, 30, 2, 0f, WallStyle.Brick, roofGreen);
            var hall = Building(g, "MainHallCenter", P(530, 175), 18, 21, 3, 0f, WallStyle.Painted, roofGreen);
            Slab("HallCrown", hall, P(530, 175) + Vector3.up * 10f, new Vector3(14f, 1.2f, 14f), 45f, roofGreen, 3f, false);
            Building(g, "MainHallSouthBlock", P(525, 290), 51, 15, 1, 0f, WallStyle.Mixed, roofRed);

            // Market stalls in front of the hall's south block.
            var market = Group("Market", g);
            for (int i = 0; i < 6; i++)
            {
                var p = P(430 + i * 38, 325);
                Place(RU + "detail-awning-wide.fbx", market, p + new Vector3(0, 2.6f, 0), 180f, M * 1.5f);
                Place(SV + "structure-canvas.fbx", market, p + new Vector3(0, 0, -2.5f), rng.Next(4) * 90f, 5f);
            }

            // Patchwork shops east of the hall.
            Building(g, "ShopA", P(690, 185), 12, 15, 1, 0f, WallStyle.Mixed, roofYellow);
            Building(g, "ShopB", P(705, 245), 15, 9, 1, 0f, WallStyle.Painted, roofRust);

            // North long building with a derrick in the middle.
            Building(g, "NorthHall", P(805, 70), 72, 18, 2, 0f, WallStyle.Metal, roofGrey);
            Tower(g, "Derrick", P(815, 40) + Vector3.up * (2 * M), 4);

            // North-east barracks.
            Building(g, "BarracksA", P(1115, 55), 39, 18, 1, 0f, WallStyle.Brick, roofGrey);
            Building(g, "BarracksB", P(1355, 90), 54, 15, 1, 14f, WallStyle.Metal, roofRust);

            // Teal hall with a rooftop antenna.
            Building(g, "TealHall", P(958, 252), 24, 21, 2, 0f, WallStyle.Painted, roofTeal);
            Place(RU + "detail-light-single.fbx", g, P(958, 252) + Vector3.up * (2 * M + 0.4f), 0f, M * 2f);

            // Factory with pipes and chimneys, plus its outbuildings.
            Building(g, "Factory", P(1240, 245), 66, 36, 2, 0f, WallStyle.Brick, roofYellow);
            Building(g, "FactoryOffice", P(1075, 235), 12, 33, 1, 0f, WallStyle.Metal, roofGrey);
            var pipes = Group("FactoryPipes", g);
            Place(IN + "detail-tank.fbx", pipes, P(1150, 215) + Vector3.up * (2 * M + 0.4f), 90f, 4f);
            Place(IN + "detail-tank.fbx", pipes, P(1180, 270) + Vector3.up * (2 * M + 0.4f), 0f, 4f);
            Place(IN + "chimney-medium.fbx", pipes, P(1135, 290) + Vector3.up * (2 * M + 0.4f), 0f, 4f);
            Place(IN + "chimney-small.fbx", pipes, P(1320, 200) + Vector3.up * (2 * M + 0.4f), 0f, 4f);
            Building(g, "EastShedA", P(1415, 205), 9, 18, 1, 0f, WallStyle.Metal, roofGrey);
            Building(g, "EastShedB", P(1030, 205), 6, 9, 1, 0f, WallStyle.Mixed, roofRust);

            // Watchtower north of the factory.
            Tower(g, "Watchtower", P(1140, 140), 3);

            // Shops built into the south wall on both sides of the gate.
            Building(g, "WallShopWest", P(645, 360), 15, 12, 1, 0f, WallStyle.Mixed, roofRust);
            Building(g, "WallShopEast", P(890, 368), 24, 12, 1, 0f, WallStyle.Mixed, roofRed);
            Building(g, "WallShopFar", P(1060, 372), 15, 9, 1, 0f, WallStyle.Painted, roofGrey);
        }

        private static void Tower(Transform parent, string name, Vector3 basePos, int levels)
        {
            var t = Group(name, parent);
            t.position = basePos;
            for (int i = 0; i < levels; i++)
                Place(RU + "scaffolding-structure.fbx", t, basePos + Vector3.up * (i * M), 0f, M);
            Place(RU + "scaffolding-floor.fbx", t, basePos + Vector3.up * (levels * M), 0f, M);
            Place(RU + "roof-metal-type-a.fbx", t, basePos + Vector3.up * (levels * M + 2.2f), 0f, M * 1.2f);
            var col = t.gameObject.AddComponent<BoxCollider>();
            col.center = Vector3.up * (levels * M * 0.5f);
            col.size = new Vector3(M, levels * M, M);
            MarkOccupied(basePos, M, M);
        }

        // ---------------------------------------------------------------- props

        private static readonly string[] clutter =
        {
            SV + "barrel.fbx", SV + "barrel.fbx", SV + "barrel-open.fbx", SV + "box-large.fbx", SV + "box.fbx",
            RU + "pallet.fbx", RU + "pallet-small.fbx", RU + "planks.fbx", RU + "detail-dumpster-closed.fbx",
            RU + "detail-bricks-type-a.fbx", RU + "detail-block.fbx"
        };

        private static void BuildProps()
        {
            var g = Group("Props");

            // Vehicles and barricades outside the gate.
            Prop(g, RU + "truck-grey.fbx", P(625, 455), -60f, M, true);
            Prop(g, RU + "truck-flat.fbx", P(1270, 415), 20f, M, true);
            Prop(g, RU + "detail-barrier-strong-damaged.fbx", P(665, 445), 10f, M * 1.5f, true);
            Prop(g, RU + "detail-barrier-strong-type-a.fbx", P(790, 450), -5f, M * 1.5f, true);
            Prop(g, RU + "detail-barrier-strong-type-b.fbx", P(850, 445), 5f, M * 1.5f, true);

            // Water tower, tanks and containers.
            Prop(g, IN + "water-tower.fbx", P(695, 125), 0f, 5f, true);
            Prop(g, IN + "detail-tank-large.fbx", P(1395, 290), 30f, 5f, true);
            Prop(g, IN + "shipping-container-a.fbx", P(1000, 320), 90f, 7.3f, true);
            Prop(g, IN + "shipping-container-c.fbx", P(250, 340), 0f, 7.3f, true);
            Prop(g, IN + "shipping-container-b.fbx", P(1300, 150), 80f, 7.3f, true);

            // Clutter clusters.
            Cluster(g, P(240, 210), 10, 8f);   // warehouse yard
            Cluster(g, P(820, 200), 8, 7f);    // central plaza
            Cluster(g, P(1020, 150), 8, 6f);   // between north hall and factory
            Cluster(g, P(1410, 320), 8, 6f);   // east wall corner
            Cluster(g, P(560, 345), 8, 8f);    // market
            Cluster(g, P(940, 330), 6, 6f);    // behind teal hall
            Cluster(g, P(380, 330), 6, 6f);    // west of market
        }

        private static void Prop(Transform parent, string path, Vector3 pos, float yaw, float scale, bool collider)
        {
            var go = Place(path, parent, pos, yaw, scale);
            if (go == null) return;
            if (collider) AddBoundsCollider(go);
            MarkOccupied(pos, 3f, 3f, 0.5f);
        }

        private static void Cluster(Transform parent, Vector3 center, int count, float radius)
        {
            var g = Group("Clutter", parent);
            for (int i = 0; i < count; i++)
            {
                var p = center + new Vector3(Rand(-radius, radius), 0f, Rand(-radius, radius));
                if (!IsFree(p)) continue;
                var go = Place(Pick(clutter), g, p, Rand(0f, 360f), M);
                if (go != null) AddBoundsCollider(go);
            }
        }

        private static void AddBoundsCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            var s = go.transform.lossyScale;
            box.size = new Vector3(bounds.size.x / s.x, bounds.size.y / s.y, bounds.size.z / s.z);
        }

        // ---------------------------------------------------------------- vegetation

        /// <summary>Covers the desert outside the dirt/compound areas with Low Poly Desert Environment ground tiles (visual only; the sand slab keeps the collider).</summary>
        private static void BuildDesertGroundTiles()
        {
            var tile = Model(DE + "Ground_01.prefab");
            if (tile == null) return;
            var g = Group("DesertTiles", root.Find("Ground"));

            // Paint the base sand slab with the tile's palette colour so the gaps left along roads/compound edges blend in.
            var tileMf = tile.GetComponentInChildren<MeshFilter>();
            var tileMr = tile.GetComponentInChildren<MeshRenderer>();
            var slab = root.Find("Ground/Sand");
            if (tileMf != null && tileMr != null && slab != null)
            {
                var slabMesh = slab.GetComponent<MeshFilter>().sharedMesh;
                var paletteUv = tileMf.sharedMesh.uv[0];
                var uvs = new Vector2[slabMesh.vertexCount];
                for (int i = 0; i < uvs.Length; i++) uvs[i] = paletteUv;
                slabMesh.uv = uvs;
                slab.GetComponent<MeshRenderer>().sharedMaterial = tileMr.sharedMaterial;
            }
            const float size = 5f;
            for (float x = -220f + size * 0.5f; x < 220f; x += size)
            {
                for (float z = -110f + size * 0.5f; z < 90f; z += size)
                {
                    var r = new Rect(x - size * 0.5f, z - size * 0.5f, size, size);
                    bool blocked = false;
                    foreach (var ex in groundExclusions) if (ex.Overlaps(r)) { blocked = true; break; }
                    if (blocked) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(tile, g);
                    go.transform.position = new Vector3(x, 0.1f, z);
                    foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                }
            }
        }

        /// <summary>Rocks, dead trees and cacti from Low Poly Desert Environment, scattered outside the compound.</summary>
        private static void BuildDesertProps(Transform g)
        {
            string[] bigRocks = { DE + "Rock_01.prefab", DE + "Rock_02.prefab", DE + "Rock_03.prefab" };
            string[] smallRocks = { DE + "Rock_04.prefab", DE + "Rock_05.prefab" };
            string[] cacti = { DE + "Cactus_01.prefab", DE + "Cactus_02.prefab", DE + "Cactus_03.prefab" };

            void Scatter(string[] set, int count, float minS, float maxS, bool keepCollider)
            {
                int placed = 0;
                for (int i = 0; i < count * 4 && placed < count; i++)
                {
                    var p = new Vector3(Rand(-210f, 210f), 0.05f, Rand(-105f, 85f));
                    bool inside = p.x > -180f && p.x < 172f && p.z > -30f && p.z < 62f;
                    if (inside || !IsFree(p)) continue;
                    var go = Place(Pick(set), g, p, Rand(0f, 360f), Rand(minS, maxS));
                    if (go == null) continue;
                    if (!keepCollider) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    MarkOccupied(p, 2f, 2f, 0.5f);
                    placed++;
                }
            }

            Scatter(bigRocks, 45, 1f, 2.5f, true);
            Scatter(smallRocks, 60, 2f, 5f, false);
            Scatter(new[] { DE + "Tree_01.prefab" }, 25, 0.8f, 1.4f, true);
            Scatter(cacti, 45, 0.8f, 1.5f, true);
        }

        private static void BuildVegetation(bool desertPack = false)
        {
            var g = Group("Vegetation");
            string[] rocks = { SV + "rock-sand-a.fbx", SV + "rock-sand-b.fbx", SV + "rock-sand-c.fbx", SV + "rock-flat.fbx" };

            // VARCO desert shrub (kenney's alpha-card shrubs render as solid green planes in URP, so they are not used).
            var shrub = Model(VA + "Settle_Shrub.fbx");
            int placed = 0;
            for (int i = 0; shrub != null && i < 400 && placed < 180; i++)
            {
                var p = new Vector3(Rand(-210f, 210f), 0f, Rand(-105f, 85f));
                bool inside = p.x > -175f && p.x < 170f && p.z > -27f && p.z < 60f;
                if (inside && rng.NextDouble() < 0.8) continue;
                if (!IsFree(p)) continue;
                var w = Wrap(shrub, g);
                var size = RendererBounds(w.gameObject).size;
                w.localScale = Vector3.one * (Rand(1.2f, 2.6f) / Mathf.Max(size.x, size.z));
                w.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                var b = RendererBounds(w.gameObject);
                w.position = new Vector3(p.x - b.center.x + w.position.x, -b.min.y, p.z - b.center.z + w.position.z);
                placed++;
            }

            if (desertPack)
            {
                BuildDesertProps(g);
                return;
            }

            for (int i = 0; i < 45; i++)
            {
                var p = new Vector3(Rand(-210f, 210f), 0f, Rand(-105f, 85f));
                bool inside = p.x > -175f && p.x < 170f && p.z > -27f && p.z < 60f;
                if (inside || !IsFree(p)) continue;
                var go = Place(Pick(rocks), g, p, Rand(0f, 360f), Rand(3f, 7f));
                if (go != null) AddBoundsCollider(go);
            }
        }

        // ---------------------------------------------------------------- preview

        /// <summary>Renders the scene from a temporary camera into a PNG for layout review.</summary>
        public static string RenderPreview(string outPath, Vector3 camPos, Vector3 camEuler, bool ortho, float sizeOrFov, int width, int height)
        {
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.Euler(camEuler);
            cam.orthographic = ortho;
            if (ortho) cam.orthographicSize = sizeOrFov; else cam.fieldOfView = sizeOrFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f);

            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
            return outPath;
        }
    }
}
