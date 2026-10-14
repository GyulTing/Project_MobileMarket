using System.IO;
using UnityEditor;
using UnityEngine;
using MobileMarket.Data;
using MobileMarket.Items;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Renders item icons from their 3D models (weapons, gear) into Textures/ItemIcons with a transparent background
    /// and a thick dark outline for a cartoon "sticker" look, then assigns them to ItemData.icon.
    /// </summary>
    public static class ItemIconRenderer
    {
        private const string ItemDir = "Assets/_Project/Resources/Items";
        private const string IconDir = "Assets/_Project/Textures/ItemIcons/";
        private const int Size = 256;
        private const int OutlinePx = 5;
        private const int Layer = 31;
        private static readonly Color32 OutlineColor = new Color32(24, 20, 16, 255);

        [MenuItem("MobileMarket/Items/Render Item Icons")]
        public static void RenderAll()
        {
            var items = new System.Collections.Generic.List<ItemData>();
            foreach (var guid in AssetDatabase.FindAssets("t:ItemData", new[] { ItemDir }))
                items.Add(AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid)));
            Render(items);
        }

        /// <summary>Renders icons for just these items (e.g. newly added weapons), leaving the others untouched.</summary>
        public static void Render(System.Collections.Generic.IEnumerable<ItemData> items)
        {
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var camGo = new GameObject("__IconCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << Layer;
            cam.targetTexture = rt;
            cam.enabled = false;

            var lightGo = new GameObject("__IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.cullingMask = 1 << Layer;
            lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            int count = 0;
            try
            {
                foreach (var data in items)
                {
                    var model = ModelFor(data, out bool sideProfile);
                    if (model == null) continue;

                    var tex = Render(model, sideProfile, cam, rt);
                    AddOutline(tex);
                    var trimmed = Trim(tex);
                    data.icon = SaveSprite(trimmed, "T_Icon_" + data.name);
                    Object.DestroyImmediate(tex);
                    Object.DestroyImmediate(trimmed);
                    EditorUtility.SetDirty(data);
                    count++;
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemIconRenderer] Rendered {count} icons.");
        }

        /// <summary>
        /// Uses a 2D illustration (square, plain white background) as an item's icon: background removed, scaled to icon size,
        /// same dark outline as the rendered icons, trimmed.
        /// </summary>
        public static void ImportIllustration(string png, ItemData data)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(png));
            UiArtImporter.RemoveWhiteBackground(src);

            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            AddOutline(tex);
            var trimmed = Trim(tex);
            data.icon = SaveSprite(trimmed, "T_Icon_" + data.name);
            EditorUtility.SetDirty(data);

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(trimmed);
        }

        /// <summary>Weapons are shown from the side (barrel horizontal); gear from a 3/4 front view.</summary>
        private static GameObject ModelFor(ItemData data, out bool sideProfile)
        {
            sideProfile = false;
            if (data is WeaponData weapon)
            {
                sideProfile = true;
                return weapon.weaponModelPrefab;
            }
            if (data is EquipmentData)
            {
                var path = GearItemBuilder.ModelPathFor(data.id);
                return path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            }
            return null;
        }

        private static Texture2D Render(GameObject model, bool sideProfile, Camera cam, RenderTexture rt)
        {
            var inst = (GameObject)Object.Instantiate(model);
            try
            {
                inst.transform.position = new Vector3(0f, -5000f, 0f);
                if (sideProfile) inst.transform.rotation = Quaternion.identity;
                foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

                var renderers = inst.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                Vector3 forward, up;
                if (sideProfile)
                {
                    // Weapon models are built barrel along +Z, top up +Y: look along +X so the barrel runs left-right.
                    // (Not "the thinnest axis": a spread bipod makes the MG42 wider than it is tall.)
                    forward = Vector3.right;
                    up = Vector3.up;
                }
                else
                {
                    forward = Quaternion.Euler(10f, 205f, 0f) * Vector3.forward;
                    up = Vector3.up;
                }

                float distance = bounds.extents.magnitude * 4f;
                cam.transform.SetPositionAndRotation(bounds.center - forward * distance, Quaternion.LookRotation(forward, up));
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = distance * 4f;
                cam.orthographicSize = FitSize(cam, bounds) * 1.12f;

                cam.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                return tex;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>Half-height of the ortho view needed to fit the bounds' 8 corners.</summary>
        private static float FitSize(Camera cam, Bounds b)
        {
            float maxX = 0f, maxY = 0f;
            var view = cam.transform;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = view.InverseTransformPoint(corner) - view.InverseTransformPoint(b.center);
                maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
            }
            return Mathf.Max(maxY, maxX);
        }

        /// <summary>Paints a solid dark border around the opaque silhouette.</summary>
        private static void AddOutline(Texture2D tex)
        {
            var src = tex.GetPixels32();
            var dst = (Color32[])src.Clone();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    if (src[i].a > 128) continue;

                    bool near = false;
                    for (int dy = -OutlinePx; dy <= OutlinePx && !near; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= Size) continue;
                        for (int dx = -OutlinePx; dx <= OutlinePx; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= Size || dx * dx + dy * dy > OutlinePx * OutlinePx) continue;
                            if (src[yy * Size + xx].a > 128) { near = true; break; }
                        }
                    }
                    if (near) dst[i] = OutlineColor;
                }
            }
            tex.SetPixels32(dst);
            tex.Apply();
        }

        /// <summary>Crops to the visible pixels (plus a little padding) so the sprite's aspect matches the item — a rifle icon is wide, not square.</summary>
        private static Texture2D Trim(Texture2D tex)
        {
            const int pad = 2;
            var px = tex.GetPixels32();
            int minX = Size, minY = Size, maxX = -1, maxY = -1;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (px[y * Size + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (maxX < 0) return Object.Instantiate(tex);

            minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(Size - 1, maxX + pad); maxY = Mathf.Min(Size - 1, maxY + pad);
            int w = maxX - minX + 1, h = maxY - minY + 1;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.SetPixels(tex.GetPixels(minX, minY, w, h));
            result.Apply();
            return result;
        }

        private static Sprite SaveSprite(Texture2D tex, string fileName)
        {
            if (!AssetDatabase.IsValidFolder(IconDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/_Project/Textures", "ItemIcons");
            string path = IconDir + fileName + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
