using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Creates the gear item data (gas masks, body armor, backpacks) from the models in Models/Varco_Item,
    /// wiring each model's baseColor/normal textures into an M_Gear_* material.
    /// </summary>
    public static class GearItemBuilder
    {
        private const string ModelRoot = "Assets/_Project/Models/Varco_Item/";
        private const string ItemDir = "Assets/_Project/Resources/Items/";
        private const string MatDir = "Assets/_Project/Materials/";

        private const string TexDir = "Assets/_Project/Textures/";

        private struct Gear
        {
            public string id, name, folder, fbx;
            public EquipSlot slot;
            public int columns, rows;
            public string panel;   // Textures/T_UI_Panel_{panel}.png, the drawing shown behind the grid while worn
            public Rect gridArea;  // the flat front pocket on that drawing, normalized, origin bottom-left
            public float wornSize; // largest side when worn, meters; 0 = the slot's default (WornDefaults)
        }

        private static readonly Gear[] Gears =
        {
            new Gear { id = "gear_gasmask_basic", name = "Basic Gas Mask", folder = "방독면/Basic Gas Mask", fbx = "Basic Gas Mask", slot = EquipSlot.Head },
            new Gear { id = "gear_gasmask_black", name = "Black Gas Mask", folder = "방독면/Black Gas Mask", fbx = "Black Gas Mask", slot = EquipSlot.Head },
            new Gear { id = "gear_gasmask_military", name = "Military Gas Mask", folder = "방독면/Military Gas Mask", fbx = "Military Gas Mask", slot = EquipSlot.Head },
            new Gear { id = "gear_armor_flak", name = "Flak Armor", folder = "방어구/Flak Armor", fbx = "Green Tactical Vest", slot = EquipSlot.Body },
            new Gear { id = "gear_armor_omon", name = "OMON Body Armor", folder = "방어구/OMON body armor", fbx = "Tactical Body Armor", slot = EquipSlot.Body },
            // Plate carriers' shoulder straps and cummerbund wings make them read larger than the vests at the same size.
            new Gear { id = "gear_armor_olive_plate", name = "Olive Plate Carrier", folder = "방어구/Olive Plate Carrier", fbx = "Olive Plate Carrier", slot = EquipSlot.Body, wornSize = 0.52f },
            new Gear { id = "gear_armor_plate", name = "Plate Carrier", folder = "방어구/Plate Carrier", fbx = "Green Plate Carrier", slot = EquipSlot.Body, wornSize = 0.52f },
            new Gear { id = "gear_backpack_canvas", name = "Canvas Rucksack", folder = "가방/Canvas Rucksack (4x4)", fbx = "Canvas Rucksack", slot = EquipSlot.Backpack, columns = 4, rows = 4,
                       panel = "Canvas", gridArea = new Rect(0.15f, 0.18f, 0.69f, 0.49f) },
            new Gear { id = "gear_backpack_olive", name = "Olive Rucksack", folder = "가방/Olive Rucksack (5x5)", fbx = "Olive Rucksack Bedroll", slot = EquipSlot.Backpack, columns = 5, rows = 5,
                       panel = "Olive", gridArea = new Rect(0.18f, 0.17f, 0.64f, 0.44f) },
            new Gear { id = "gear_backpack_tactical", name = "Tactical Backpack", folder = "가방/Tactical Backpack (5x7)", fbx = "Tactical Backpack", slot = EquipSlot.Backpack, columns = 5, rows = 7,
                       panel = "Tactical", gridArea = new Rect(0.2f, 0.13f, 0.6f, 0.55f) },
            new Gear { id = "gear_backpack_alice", name = "Alice BackPack", folder = "가방/Alice BackPack (6x7)", fbx = "Alice BackPack", slot = EquipSlot.Backpack, columns = 6, rows = 7,
                       panel = "Alice", gridArea = new Rect(0.25f, 0.12f, 0.5f, 0.52f) },
        };

        /// <summary>Path of the model for a gear item id (used by the icon renderer).</summary>
        public static string ModelPathFor(string id)
        {
            foreach (var g in Gears) if (g.id == id) return FindModel(g);
            return null;
        }

        [MenuItem("MobileMarket/Items/Build Gear Items")]
        public static void Build()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var g in Gears)
            {
                string modelPath = FindModel(g);
                if (modelPath == null) { Debug.LogWarning("[GearItemBuilder] Model not found for " + g.name); continue; }

                SetupModel(modelPath, g, lit);

                string assetName = ToAssetName(g.id);
                string itemPath = ItemDir + assetName + ".asset";
                EquipmentData data = AssetDatabase.LoadAssetAtPath<EquipmentData>(itemPath);
                if (data == null)
                {
                    data = g.slot == EquipSlot.Backpack ? ScriptableObject.CreateInstance<BackpackData>() : ScriptableObject.CreateInstance<EquipmentData>();
                    AssetDatabase.CreateAsset(data, itemPath);
                }
                data.id = g.id;
                data.itemName = g.name;
                data.slot = g.slot;
                data.wornModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (data is BackpackData backpack)
                {
                    backpack.columns = g.columns;
                    backpack.rows = g.rows;
                    backpack.panelArt = AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + "T_UI_Panel_" + g.panel + ".png");
                    backpack.gridArea = g.gridArea;
                }
                EditorUtility.SetDirty(data);
            }
            FitWornPoses();
            AssetDatabase.SaveAssets();
        }

        private const string PlayerPrefab = "Assets/_Project/Prefabs/Player/P_Player.prefab";

        /// <summary>
        /// Default worn placement per slot, in the player prefab's space (root at the origin, facing +Z, about 2m tall):
        /// mask over the face, armor around the torso, backpack on the back turned to face backwards.
        /// Stored relative to the bone each slot hangs on, so it can be fine-tuned per item in the Inspector afterwards.
        /// </summary>
        private static readonly (EquipSlot slot, Vector3 center, float yaw, float size)[] WornDefaults =
        {
            // Mask pulled in against the face and kept small enough that the hood covers its edges.
            (EquipSlot.Head, new Vector3(0f, 0.93f, 0.06f), 0f, 0.25f),
            // Armor is sized over the long coat so it reads outside it.
            (EquipSlot.Body, new Vector3(0f, 0.56f, 0.03f), 0f, 0.7f),
            // Backpack z is replaced per model so its strap side rests on the back (see BackSurfaceZ).
            (EquipSlot.Backpack, new Vector3(0f, 0.55f, 0f), 180f, 0.62f),
        };

        // Back of the player's coat at chest height (prefab space); backpacks sink this far into it so they sit flush.
        private const float BackSurfaceZ = -0.19f;
        private const float BackpackSink = 0.03f;

        [MenuItem("MobileMarket/Items/Fit Worn Gear To Player")]
        public static void FitWornPoses()
        {
            var player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            try
            {
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = player.GetComponentInChildren<Animator>();

                foreach (var g in Gears)
                {
                    var data = AssetDatabase.LoadAssetAtPath<EquipmentData>(ItemDir + ToAssetName(g.id) + ".asset");
                    if (data == null) continue;

                    var bone = animator.GetBoneTransform(MobileMarket.Player.PlayerEquipmentVisuals.BoneFor(g.slot));
                    if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
                    foreach (var d in WornDefaults)
                    {
                        if (d.slot != g.slot) continue;
                        var center = d.center;
                        float size = g.wornSize > 0f ? g.wornSize : d.size;
                        if (g.slot == EquipSlot.Backpack && data.wornModel != null)
                            center.z = BackSurfaceZ + BackpackSink - BodySideOffset(data.wornModel, d.yaw, size);
                        data.wornPosition = bone.InverseTransformPoint(center);
                        data.wornRotation = (Quaternion.Inverse(bone.rotation) * Quaternion.Euler(0f, d.yaw, 0f)).eulerAngles;
                        data.wornSize = size;
                    }
                    EditorUtility.SetDirty(data);
                }
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// How far the back panel of a worn backpack (the side against the body) sits in front of its center
        /// (model fitted to size, turned by yaw). Only the middle of the pack body is measured — the top and the sides,
        /// where the shoulder straps loop toward the body, are left out — so the panel itself rests on the player's back.
        /// </summary>
        private static float BodySideOffset(GameObject model, float yaw, float size)
        {
            var holder = new GameObject("__WornMeasure").transform;
            try
            {
                holder.rotation = Quaternion.Euler(0f, yaw, 0f);
                var inst = (GameObject)Object.Instantiate(model, holder, false);
                MobileMarket.Player.PlayerEquipmentVisuals.FitToSize(inst.transform, holder, size);

                // Low-poly meshes have too few vertices on a flat panel to measure, so cast rays at the pack from the body side
                // over the middle of the pack (25-65% of its height, inner 50% of its width) and take the median hit:
                // most rays land on the back panel, the few that hit a shoulder strap fall outside the median.
                var colliders = new List<MeshCollider>();
                var bounds = new Bounds();
                bool any = false;
                foreach (var filter in inst.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    var col = filter.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = filter.sharedMesh;
                    colliders.Add(col);
                    if (!any) { bounds = col.bounds; any = true; }
                    else bounds.Encapsulate(col.bounds);
                }
                if (!any) return 0f;

                const int steps = 12;
                var hits = new List<float>();
                for (int ix = 0; ix < steps; ix++)
                {
                    for (int iy = 0; iy < steps; iy++)
                    {
                        float x = Mathf.Lerp(bounds.min.x, bounds.max.x, 0.25f + 0.5f * (ix + 0.5f) / steps);
                        float y = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.25f + 0.4f * (iy + 0.5f) / steps);
                        var ray = new Ray(new Vector3(x, y, bounds.max.z + 1f), Vector3.back);
                        float best = float.MaxValue;
                        foreach (var col in colliders)
                            if (col.Raycast(ray, out var hit, 10f) && hit.distance < best) best = hit.distance;
                        if (best < float.MaxValue) hits.Add(ray.origin.z - best);
                    }
                }
                if (hits.Count == 0) return 0f;
                hits.Sort();
                return hits[hits.Count / 2];
            }
            finally
            {
                Object.DestroyImmediate(holder.gameObject);
            }
        }

        // gear_backpack_canvas -> Gear_Backpack_Canvas
        private static string ToAssetName(string id)
        {
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join("_", parts);
        }

        private static string FindModel(Gear g)
        {
            string dir = ModelRoot + g.folder;
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).StartsWith(g.fbx)) return path;
            }
            return null;
        }

        private static void SetupModel(string modelPath, Gear g, Shader lit)
        {
            string dir = Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/";
            Texture2D baseColor = null, normal = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isNormal = path.Contains("-normal");

                // The source textures are huge; 2048 is plenty for a held/worn item.
                bool dirty = importer.maxTextureSize != 2048 || (isNormal && importer.textureType != TextureImporterType.NormalMap);
                importer.maxTextureSize = 2048;
                if (isNormal) importer.textureType = TextureImporterType.NormalMap;
                if (dirty) importer.SaveAndReimport();

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (isNormal) normal = tex;
                else if (path.Contains("-baseColor")) baseColor = tex;
            }

            string matPath = MatDir + "M_" + ToAssetName(g.id) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", baseColor);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);

            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material_0"), mat);
            modelImporter.SaveAndReimport();
        }
    }
}
