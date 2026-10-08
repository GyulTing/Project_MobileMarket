using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using MobileMarket.Data;
using MobileMarket.Environment;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Builds the map loot containers from the VARCO exports (Models/Varco_Item/상자,배치물/Loot_{Kind}_{Closed|Open}.fbx):
    /// one prefab per kind (Prefabs/Environment/P_LootBox_{Kind}) holding a LootBox, both models (Closed shown, Open hidden
    /// until the first opening), a BoxCollider and a carving NavMeshObstacle. Also creates the per-kind loot tables
    /// (Resources/Items/LT_{Kind}) and P_LootSpawnPoint, which picks one of these boxes when the map starts.
    /// Models keep their real size and face -Z (toward the quarter-view camera).
    /// </summary>
    public static class LootBuilder
    {
        private const string ModelDir = "Assets/_Project/Models/Varco_Item/상자,배치물/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/";
        private const string TableDir = "Assets/_Project/Resources/Items/";
        private const string PanelArtDir = "Assets/_Project/Textures/";

        // Real-world meters to world units (the 2.21-unit player stands for 1.75 m).
        private const float WorldScale = 2.21f / 1.75f;

        private enum SizeAxis { Length, Height }

        /// <summary>
        /// How the open model lines up with the closed one: the side its opening doesn't stick out of (Front/Back), or the
        /// center of its top part (Top — for a lid lying beside the body).
        /// </summary>
        private enum OpenAlign { Front, Back, Top }

        private struct Kind
        {
            public string name, displayName;
            public float size;          // real meters along axis (Length = longest horizontal side)
            public SizeAxis axis;
            public float closedYaw, openYaw; // turns each model's front to -Z (VARCO exports don't share a facing)
            public OpenAlign align;
            public int columns, rows, rolls;
            public float openDuration;
            public float chanceToDropAnything;
            public (string item, float weight)[] loot;
            public Rect panelArea; // where the grid sits on Textures/T_UI_Panel_{name} (none = default crate drawing)
        }

        private static readonly string[] Seeds = { "Seed_Chili", "Seed_Corn", "Seed_KidneyBean", "Seed_Mushroom", "Seed_Potato", "Seed_Rice", "Seed_Tomato", "Seed_Wheat" };
        private static readonly string[] Crops = { "Crop_Chili", "Crop_Corn", "Crop_KidneyBean", "Crop_Mushroom", "Crop_Potato", "Crop_Rice", "Crop_Tomato", "Crop_Wheat" };
        private static readonly string[] Guns = { "AK-47", "Ash-12", "BAR-1918", "K-2", "M1A1", "M1_Carbine", "M1_Rifle", "MP-40", "PP-19-01", "STG-44", "Spas", "Vepr" };

        // Tool and medical items don't exist yet: ToolCabinet and MedicalCase hold stand-ins until they do.
        private static readonly Kind[] Kinds =
        {
            new Kind { name = "WoodCrate", displayName = "나무 상자", size = 1.0f, axis = SizeAxis.Length, closedYaw = -90f, openYaw = 180f,
                align = OpenAlign.Front, columns = 3, rows = 3, rolls = 3, openDuration = 1.5f, chanceToDropAnything = 0.8f, panelArea = new Rect(0.06f, 0.03f, 0.88f, 0.42f), // on the front planks
                loot = Concat(new[] { ("OldSpam", 4f), ("Gear_Backpack_Canvas", 0.5f) }, Each(Seeds, 1f), Each(Crops, 0.5f)) },
            new Kind { name = "MilitaryCrate", displayName = "군용 상자", size = 1.1f, axis = SizeAxis.Length, closedYaw = 0f, openYaw = 180f,
                align = OpenAlign.Front, columns = 3, rows = 3, rolls = 3, openDuration = 1.5f, chanceToDropAnything = 0.8f,
                loot = Concat(new[] { ("Gear_Armor_Flak", 1f), ("Gear_Armor_Olive_Plate", 0.7f), ("Gear_Armor_Omon", 0.7f), ("Gear_Armor_Plate", 0.5f),
                    ("Gear_Gasmask_Military", 1f), ("Gear_Backpack_Alice", 0.7f), ("Gear_Backpack_Tactical", 0.5f), ("OldSpam", 1.5f) }, Each(Guns, 0.6f)) },
            new Kind { name = "TrashPile", displayName = "쓰레기 더미", size = 1.2f, axis = SizeAxis.Length, closedYaw = 180f, openYaw = 180f,
                align = OpenAlign.Back, columns = 3, rows = 2, rolls = 2, openDuration = 2.0f, chanceToDropAnything = 0.55f, panelArea = new Rect(0.29f, 0.22f, 0.46f, 0.52f),
                loot = Concat(new[] { ("OldSpam", 3f), ("Gear_Gasmask_Basic", 0.5f), ("Gear_Backpack_Canvas", 0.3f) }, Each(Seeds, 0.5f)) },
            new Kind { name = "ToolCabinet", displayName = "공구함", size = 1.0f, axis = SizeAxis.Height, closedYaw = 180f, openYaw = 180f,
                align = OpenAlign.Back, columns = 3, rows = 3, rolls = 3, openDuration = 1.5f, chanceToDropAnything = 0.7f, panelArea = new Rect(0.29f, 0.60f, 0.58f, 0.33f),
                loot = new[] { ("Gear_Gasmask_Basic", 1f), ("Gear_Gasmask_Black", 1f), ("Gear_Armor_Flak", 0.5f), ("MP-40", 0.3f), ("PP-19-01", 0.3f), ("OldSpam", 1.5f) } },
            new Kind { name = "MedicalCase", displayName = "의료품 상자", size = 0.7f, axis = SizeAxis.Length, closedYaw = 0f, openYaw = 180f,
                align = OpenAlign.Front, columns = 2, rows = 2, rolls = 2, openDuration = 1.0f, chanceToDropAnything = 0.8f, panelArea = new Rect(0.12f, 0.47f, 0.76f, 0.42f), // on the open lid
                loot = new[] { ("Gear_Gasmask_Basic", 1f), ("Gear_Gasmask_Black", 0.7f), ("Gear_Gasmask_Military", 0.4f), ("OldSpam", 2f) } },
            new Kind { name = "LuggagePile", displayName = "여행 가방 더미", size = 1.2f, axis = SizeAxis.Length, closedYaw = 180f, openYaw = 180f,
                align = OpenAlign.Back, columns = 4, rows = 3, rolls = 4, openDuration = 2.0f, chanceToDropAnything = 0.7f, panelArea = new Rect(0.16f, 0.12f, 0.68f, 0.42f),
                loot = Concat(new[] { ("Gear_Backpack_Canvas", 1f), ("Gear_Backpack_Olive", 1f), ("Gear_Backpack_Alice", 0.5f), ("Gear_Backpack_Tactical", 0.3f),
                    ("OldSpam", 2f), ("Gear_Gasmask_Basic", 1f), ("Gear_Armor_Flak", 0.5f) }, Each(Seeds, 0.3f)) },
            new Kind { name = "OilDrum", displayName = "드럼통", size = 0.9f, axis = SizeAxis.Height, closedYaw = 0f, openYaw = -45f, // lid leaning at the front-right
                align = OpenAlign.Top, columns = 2, rows = 2, rolls = 2, openDuration = 1.0f, chanceToDropAnything = 0.5f, panelArea = new Rect(0.17f, 0.46f, 0.66f, 0.36f),
                loot = Concat(new[] { ("OldSpam", 2f), ("Gear_Gasmask_Basic", 0.3f) }, Each(Seeds, 0.5f)) },
        };

        [MenuItem("MobileMarket/Environment/Build Loot Boxes")]
        public static void BuildAll()
        {
            var boxes = new LootBox[Kinds.Length];
            for (int i = 0; i < Kinds.Length; i++) boxes[i] = BuildBox(Kinds[i]);
            BuildSpawnPoint(boxes);
            AssetDatabase.SaveAssets();
        }

        private static LootBox BuildBox(Kind kind)
        {
            string closedName = "Loot_" + kind.name + "_Closed";
            string openName = "Loot_" + kind.name + "_Open";
            var closedModel = LoadModel(closedName);
            var openModel = LoadModel(openName);
            if (closedModel == null || openModel == null) return null;

            var root = new GameObject("P_LootBox_" + kind.name);
            var closed = PlaceModel(closedModel, "Closed", root.transform, kind.closedYaw, kind.size * WorldScale, kind.axis);
            var open = PlaceModel(openModel, "Open", root.transform, kind.openYaw, kind.size * WorldScale, kind.axis);

            // Closed model centered with its base at y = 0; the open one lines up with it on the side its opening doesn't change.
            var cb = WorldBounds(closed);
            closed.transform.localPosition -= new Vector3(cb.center.x, cb.min.y, cb.center.z);
            cb = WorldBounds(closed);
            var ob = WorldBounds(open);
            Vector3 shift;
            if (kind.align == OpenAlign.Top)
            {
                Vector3 closedTop = TopCenter(closed), openTop = TopCenter(open);
                shift = new Vector3(closedTop.x - openTop.x, cb.min.y - ob.min.y, closedTop.z - openTop.z);
            }
            else
            {
                float dz = kind.align == OpenAlign.Front ? cb.min.z - ob.min.z : cb.max.z - ob.max.z;
                shift = new Vector3(cb.center.x - ob.center.x, cb.min.y - ob.min.y, dz);
            }
            open.transform.localPosition += shift;
            open.SetActive(false);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = cb.center;
            collider.size = cb.size;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = cb.center;
            obstacle.size = cb.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            var box = root.AddComponent<LootBox>();
            var so = new SerializedObject(box);
            so.FindProperty("displayName").stringValue = kind.displayName;
            so.FindProperty("columns").intValue = kind.columns;
            so.FindProperty("rows").intValue = kind.rows;
            so.FindProperty("rollCount").intValue = kind.rolls;
            so.FindProperty("lootTable").objectReferenceValue = BuildTable(kind);
            so.FindProperty("openDuration").floatValue = kind.openDuration;
            so.FindProperty("closedVisual").objectReferenceValue = closed;
            so.FindProperty("openVisual").objectReferenceValue = open;
            var panelArt = AssetDatabase.LoadAssetAtPath<Sprite>(PanelArtDir + "T_UI_Panel_" + kind.name + ".png");
            so.FindProperty("panelArt").objectReferenceValue = panelArt;
            if (panelArt != null) so.FindProperty("panelGridArea").rectValue = kind.panelArea;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + root.name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<LootBox>();
        }

        private static GameObject LoadModel(string name)
        {
            string path = ModelDir + name + ".fbx";
            if (AssetImporter.GetAtPath(path) == null) { Debug.LogWarning("[LootBuilder] Missing " + path); return null; }
            VarcoCharacterTools.SetupStaticModel(path, name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Instantiates a model under parent, turned by yaw and scaled to size along axis.</summary>
        private static GameObject PlaceModel(GameObject model, string name, Transform parent, float yaw, float size, SizeAxis axis)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.name = name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            var bounds = WorldBounds(go);
            float current = axis == SizeAxis.Height ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
            if (current > 0f) go.transform.localScale *= size / current;
            return go;
        }

        private static LootTable BuildTable(Kind kind)
        {
            string path = TableDir + "LT_" + kind.name + ".asset";
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                AssetDatabase.CreateAsset(table, path);
            }
            table.chanceToDropAnything = kind.chanceToDropAnything;
            table.entries.Clear();
            foreach (var (id, weight) in kind.loot)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(TableDir + id + ".asset");
                if (item == null) { Debug.LogWarning("[LootBuilder] Missing item " + id); continue; }
                table.entries.Add(new LootTable.Entry { item = item, weight = weight });
            }
            EditorUtility.SetDirty(table);
            return table;
        }

        private static void BuildSpawnPoint(LootBox[] boxes)
        {
            var root = new GameObject("P_LootSpawnPoint");
            var point = root.AddComponent<LootSpawnPoint>();
            var so = new SerializedObject(point);
            var candidates = so.FindProperty("candidates");
            candidates.arraySize = boxes.Length;
            for (int i = 0; i < boxes.Length; i++) candidates.GetArrayElementAtIndex(i).objectReferenceValue = boxes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "P_LootSpawnPoint.prefab");
            Object.DestroyImmediate(root);
        }

        private static (string, float)[] Each(string[] ids, float weight)
        {
            var result = new (string, float)[ids.Length];
            for (int i = 0; i < ids.Length; i++) result[i] = (ids[i], weight);
            return result;
        }

        private static (string, float)[] Concat(params (string, float)[][] parts)
        {
            int count = 0;
            foreach (var part in parts) count += part.Length;
            var result = new (string, float)[count];
            int index = 0;
            foreach (var part in parts) { part.CopyTo(result, index); index += part.Length; }
            return result;
        }

        /// <summary>Center (world XZ) of the vertices in the top quarter of the model's height.</summary>
        private static Vector3 TopCenter(GameObject go)
        {
            var bounds = WorldBounds(go);
            float threshold = bounds.min.y + bounds.size.y * 0.75f;
            var top = new Bounds();
            bool any = false;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    if (p.y < threshold) continue;
                    if (any) top.Encapsulate(p);
                    else { top = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            return any ? top.center : bounds.center;
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
