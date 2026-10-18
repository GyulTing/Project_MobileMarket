using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Environment;
using MobileMarket.Items;
using MobileMarket.Managers;
using MobileMarket.Player;
using MobileMarket.UI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Generates the in-game HUD prefab Prefabs/UI/P_HUD (vitals bottom-left, weapon bottom-right, minimap top-right, objective
    /// top-left) and places it under a scene's main Canvas, wired to that scene's player, MinimapArea and QuestManager.
    /// Icons: Textures/T_UI_HUD_* (sliced from a generated white-on-black 3x3 sheet by ImportIcons).
    /// </summary>
    public static class HUDBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/P_HUD.prefab";
        private const string TexDir = "Assets/_Project/Textures/";
        private const string FontDir = "Assets/_Project/Fonts/";
        private const string SilhouetteMaterialPath = "Assets/_Project/Materials/M_UI_Silhouette.mat";

        // Sheet order, row by row from the top-left.
        private static readonly string[] SheetIcons =
        {
            "T_UI_HUD_Health", "T_UI_HUD_Stamina", "T_UI_HUD_Frag",
            "T_UI_HUD_Flashbang", "T_UI_HUD_ObjectiveHeader", "T_UI_HUD_ObjectiveBullet",
            "T_UI_HUD_PlayerArrow", "T_UI_HUD_ObjectiveMarker", "T_UI_HUD_Rifle",
        };

        private static readonly Color TextColor = new Color32(236, 236, 230, 255);
        private static readonly Color DimText = new Color32(170, 170, 165, 255);
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color PanelBack = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color MarkerColor = new Color32(232, 178, 58, 255);
        private static readonly Color MapTint = new Color32(200, 200, 200, 255);

        private const float BarWidth = 270f;
        private const float MinimapSize = 230f;

        private static Font titleFont, bodyFont;

        /// <summary>Cuts the 3x3 icon sheet (white icons on black) into transparent, trimmed sprites, plus the generated shapes.</summary>
        public static void ImportIcons(string sheetPng)
        {
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(sheetPng));
            int cellW = sheet.width / 3, cellH = sheet.height / 3;

            for (int i = 0; i < SheetIcons.Length; i++)
            {
                int col = i % 3, row = i / 3;
                // Texture rows start at the bottom, the sheet's rows at the top.
                var cell = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false);
                cell.SetPixels(sheet.GetPixels(col * cellW, (2 - row) * cellH, cellW, cellH));
                LuminanceToAlpha(cell);
                var trimmed = UiArtImporter.Trim(cell);
                WriteSprite(trimmed, SheetIcons[i], 256);
                Object.DestroyImmediate(cell);
                Object.DestroyImmediate(trimmed);
            }
            Object.DestroyImmediate(sheet);

            WriteSprite(MakeDisc(256, 0f), "T_UI_HUD_Circle", 256);
            WriteSprite(MakeDisc(256, 0.035f), "T_UI_HUD_Ring", 256);
            WriteSprite(MakeSolid(8), "T_UI_HUD_White", 32);
        }

        /// <summary>
        /// Turns a generated white-on-black weapon silhouette into Textures/ItemIcons/T_HudIcon_[asset name].png and assigns it
        /// to the weapon's hudIcon (WeaponData looked up by id under Resources).
        /// </summary>
        public static void ImportWeaponHudIcon(string png, string weaponId)
        {
            WeaponData weapon = null;
            foreach (var w in Resources.LoadAll<WeaponData>(""))
                if (w.id == weaponId) weapon = w;
            if (weapon == null) { Debug.LogError($"[HUDBuilder] No WeaponData with id '{weaponId}'."); return; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(png));
            LuminanceToAlpha(tex);
            var trimmed = UiArtImporter.Trim(tex);
            string spriteName = "ItemIcons/T_HudIcon_" + weapon.name;
            WriteSprite(trimmed, spriteName, 512);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(trimmed);

            weapon.hudIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + spriteName + ".png");
            EditorUtility.SetDirty(weapon);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Places Prefabs/UI/P_HUD under the open scene's main Canvas (replacing an older HUD or the temporary HP bar) and wires it
        /// to the scene. The prefab is only generated when it doesn't exist yet, so edits made in Prefab Mode are kept.
        /// </summary>
        [MenuItem("MobileMarket/UI/Build HUD")]
        public static void Build()
        {
            var canvas = FindMainCanvas();
            if (canvas == null) { Debug.LogError("[HUDBuilder] No root screen-space Canvas in the open scene."); return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                RebuildPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            // The temporary HP bar is replaced by the vitals block.
            foreach (var oldName in new[] { "PlayerHealthUI", "P_HUD" })
            {
                var old = canvas.transform.Find(oldName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            // The HUD sits under the full-screen screens (inventory, world map) so they cover it.
            hud.transform.SetAsFirstSibling();

            WireScene(hud.GetComponentInChildren<PlayerVitalsHUD>(true), hud.GetComponentInChildren<WeaponHUD>(true), hud.GetComponentInChildren<MinimapHUD>(true));
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log($"[HUDBuilder] HUD placed in {canvas.gameObject.scene.name}.");
        }

        /// <summary>Regenerates Prefabs/UI/P_HUD from code, overwriting it (and any Prefab Mode edits). Placed HUDs follow the new prefab.</summary>
        [MenuItem("MobileMarket/UI/Rebuild HUD Prefab")]
        public static void RebuildPrefab()
        {
            titleFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "BlackHanSans-Regular.ttf");
            bodyFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "DoHyeon-Regular.ttf");
            if (titleFont == null || bodyFont == null) titleFont = bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = Rect("P_HUD", null, Vector2.zero, Vector2.zero);
            Stretch(root);
            BuildVitals(root);
            BuildWeapon(root);
            BuildMinimap(root);
            BuildObjective(root);

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            Debug.Log("[HUDBuilder] HUD prefab rebuilt: " + PrefabPath);
        }

        /// <summary>Scene-side references (player, minimap area) live on the prefab instance, plus a QuestManager if the scene has none.</summary>
        private static void WireScene(PlayerVitalsHUD vitals, WeaponHUD weapon, MinimapHUD minimap)
        {
            var health = Object.FindFirstObjectByType<PlayerHealth>();
            var combat = health != null ? health.GetComponent<PlayerCombat>() : null;
            if (health == null) Debug.LogWarning("[HUDBuilder] No PlayerHealth in the scene; HUD left unwired.");

            SetRef(vitals, "playerHealth", health);
            SetRef(weapon, "playerCombat", combat);
            SetRef(minimap, "player", health != null ? health.transform : null);
            SetRef(minimap, "area", Object.FindFirstObjectByType<MinimapArea>());

            if (Object.FindFirstObjectByType<QuestManager>() == null)
                new GameObject("QuestManager").AddComponent<QuestManager>();
        }

        private static PlayerVitalsHUD BuildVitals(RectTransform root)
        {
            var block = Corner("Vitals", root, new Vector2(0f, 0f), new Vector2(48f, 44f), new Vector2(BarWidth + 40f, 60f));
            var healthFill = Bar("Health", block, new Vector2(0f, 40f), "T_UI_HUD_Health", 24f, 10f);
            var staminaFill = Bar("Stamina", block, new Vector2(0f, 10f), "T_UI_HUD_Stamina", 22f, 6f);

            var ui = block.gameObject.AddComponent<PlayerVitalsHUD>();
            var so = new SerializedObject(ui);
            so.FindProperty("healthFill").objectReferenceValue = healthFill;
            so.FindProperty("staminaFill").objectReferenceValue = staminaFill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        /// <summary>Icon on the left, then a dark track with a filled bar. Returns the fill.</summary>
        private static Image Bar(string name, RectTransform parent, Vector2 pos, string icon, float iconSize, float height)
        {
            var row = Rect(name, parent, pos, new Vector2(BarWidth + 40f, 26f));
            row.pivot = row.anchorMin = row.anchorMax = new Vector2(0f, 0.5f);
            row.anchoredPosition = pos;

            var iconImage = Icon("Icon", row, icon, new Vector2(iconSize, iconSize), TextColor);
            Pin(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(iconSize * 0.5f, 0f));

            var track = Rect("Track", row, Vector2.zero, new Vector2(BarWidth, height));
            Pin(track, new Vector2(0f, 0.5f), new Vector2(40f + BarWidth * 0.5f, 0f));
            AddImage(track.gameObject, Art("T_UI_HUD_White"), BarBack);

            var fill = AddImage(Rect("Fill", track, Vector2.zero, Vector2.zero).gameObject, Art("T_UI_HUD_White"), TextColor);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        private static WeaponHUD BuildWeapon(RectTransform root)
        {
            var block = Corner("Weapon", root, new Vector2(1f, 0f), new Vector2(-48f, 34f), new Vector2(420f, 110f));

            var group = Rect("WeaponGroup", block, Vector2.zero, Vector2.zero);
            Stretch(group);

            // The weapon's own (colored) inventory icon is shown, drawn as a flat silhouette in the text color.
            var icon = Icon("WeaponIcon", group, "T_UI_HUD_Rifle", new Vector2(170f, 56f), TextColor);
            Pin(icon.rectTransform, new Vector2(1f, 1f), new Vector2(-300f, -34f));
            icon.material = SilhouetteMaterial();

            var mag = AddText(Rect("Magazine", group, Vector2.zero, new Vector2(110f, 60f)).gameObject, "30", 50, TextAnchor.MiddleRight, TextColor, titleFont);
            Pin(mag.rectTransform, new Vector2(1f, 1f), new Vector2(-135f, -34f));
            Shade(mag);

            var reserve = AddText(Rect("Reserve", group, Vector2.zero, new Vector2(80f, 40f)).gameObject, "/--", 26, TextAnchor.LowerLeft, DimText, titleFont);
            Pin(reserve.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -42f));
            Shade(reserve);

            // Grenade slots: frag and flashbang, right-aligned under the ammo.
            var frag = GrenadeSlot("Frag", block, "T_UI_HUD_Frag", new Vector2(-140f, 22f));
            var flash = GrenadeSlot("Flashbang", block, "T_UI_HUD_Flashbang", new Vector2(-60f, 22f));

            var ui = block.gameObject.AddComponent<WeaponHUD>();
            var so = new SerializedObject(ui);
            so.FindProperty("weaponGroup").objectReferenceValue = group.gameObject;
            so.FindProperty("weaponIcon").objectReferenceValue = icon;
            so.FindProperty("magazineText").objectReferenceValue = mag;
            so.FindProperty("reserveText").objectReferenceValue = reserve;
            var counts = so.FindProperty("grenadeCounts");
            counts.arraySize = 2;
            counts.GetArrayElementAtIndex(0).objectReferenceValue = frag;
            counts.GetArrayElementAtIndex(1).objectReferenceValue = flash;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        /// <summary>Grenade icon with its count to the right. Returns the count label.</summary>
        private static Text GrenadeSlot(string name, RectTransform parent, string icon, Vector2 pos)
        {
            var slot = Rect(name, parent, Vector2.zero, new Vector2(70f, 30f));
            Pin(slot, new Vector2(1f, 0f), pos);

            var iconImage = Icon("Icon", slot, icon, new Vector2(24f, 24f), TextColor);
            Pin(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f));

            var count = AddText(Rect("Count", slot, Vector2.zero, new Vector2(40f, 30f)).gameObject, "--", 22, TextAnchor.MiddleLeft, TextColor, titleFont);
            Pin(count.rectTransform, new Vector2(0f, 0.5f), new Vector2(50f, 0f));
            Shade(count);
            return count;
        }

        private static MinimapHUD BuildMinimap(RectTransform root)
        {
            var block = Corner("Minimap", root, new Vector2(1f, 1f), new Vector2(-40f - MinimapSize * 0.5f, -40f - MinimapSize * 0.5f), new Vector2(MinimapSize, MinimapSize));
            block.pivot = new Vector2(0.5f, 0.5f);
            block.anchoredPosition = new Vector2(-40f - MinimapSize * 0.5f, -40f - MinimapSize * 0.5f);

            var disc = AddImage(Rect("Background", block, Vector2.zero, Vector2.zero).gameObject, Art("T_UI_HUD_Circle"), new Color(0.12f, 0.12f, 0.12f, 0.85f));
            Stretch(disc.rectTransform);

            var viewport = Rect("Viewport", block, Vector2.zero, Vector2.zero);
            Stretch(viewport);
            AddImage(viewport.gameObject, Art("T_UI_HUD_Circle"), Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            // Bottom-left pivot at the viewport center: MinimapHUD slides it so the player's spot is in the middle.
            var map = AddImage(Rect("Map", viewport, Vector2.zero, new Vector2(MinimapSize, MinimapSize)).gameObject, null, MapTint);
            map.rectTransform.anchorMin = map.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            map.rectTransform.pivot = Vector2.zero;
            map.raycastTarget = false;

            var ring = AddImage(Rect("Ring", block, Vector2.zero, Vector2.zero).gameObject, Art("T_UI_HUD_Ring"), new Color(0.85f, 0.85f, 0.85f, 0.9f));
            Stretch(ring.rectTransform);

            var marker = Icon("ObjectiveMarker", block, "T_UI_HUD_ObjectiveMarker", new Vector2(22f, 22f), MarkerColor);
            var arrow = Icon("PlayerArrow", block, "T_UI_HUD_PlayerArrow", new Vector2(20f, 22f), Color.white);

            float r = MinimapSize * 0.5f + 12f;
            Compass(block, "N", new Vector2(0f, r));
            Compass(block, "E", new Vector2(r, 0f));
            Compass(block, "S", new Vector2(0f, -r));
            Compass(block, "W", new Vector2(-r, 0f));

            var ui = block.gameObject.AddComponent<MinimapHUD>();
            var so = new SerializedObject(ui);
            so.FindProperty("viewport").objectReferenceValue = viewport;
            so.FindProperty("mapImage").objectReferenceValue = map;
            so.FindProperty("playerArrow").objectReferenceValue = arrow.rectTransform;
            so.FindProperty("objectiveMarker").objectReferenceValue = marker.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        private static void Compass(RectTransform parent, string letter, Vector2 pos)
        {
            var t = AddText(Rect("Compass_" + letter, parent, pos, new Vector2(24f, 24f)).gameObject, letter, 16, TextAnchor.MiddleCenter, TextColor, titleFont);
            Shade(t);
        }

        private static ObjectiveHUD BuildObjective(RectTransform root)
        {
            var block = Corner("Objective", root, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(460f, 84f));
            var panel = Rect("Panel", block, Vector2.zero, Vector2.zero);
            Stretch(panel);
            AddImage(panel.gameObject, Art("T_UI_HUD_White"), PanelBack).raycastTarget = false;

            var header = Icon("HeaderIcon", panel, "T_UI_HUD_ObjectiveHeader", new Vector2(18f, 18f), TextColor);
            Pin(header.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -22f));
            var title = AddText(Rect("Title", panel, Vector2.zero, new Vector2(380f, 28f)).gameObject, "주요 목표", 22, TextAnchor.MiddleLeft, TextColor, bodyFont);
            Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(44f + 190f, -22f));
            Shade(title);

            var bullet = Icon("Bullet", panel, "T_UI_HUD_ObjectiveBullet", new Vector2(16f, 16f), TextColor);
            Pin(bullet.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -58f));
            var text = AddText(Rect("ObjectiveText", panel, Vector2.zero, new Vector2(390f, 32f)).gameObject, "", 26, TextAnchor.MiddleLeft, TextColor, bodyFont);
            Pin(text.rectTransform, new Vector2(0f, 1f), new Vector2(44f + 195f, -58f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Shade(text);

            var ui = block.gameObject.AddComponent<ObjectiveHUD>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("objectiveText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        // ---------- helpers ----------

        /// <summary>The scene's top-level overlay Canvas (not one nested in a prefab such as the loading screen or a world-space label).</summary>
        private static Canvas FindMainCanvas()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.transform.parent == null && c.renderMode != RenderMode.WorldSpace) return c;
            return null;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + name + ".png");

        /// <summary>Materials/M_UI_Silhouette (MobileMarket/UI/Silhouette), created on first use.</summary>
        private static Material SilhouetteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterialPath);
            if (material != null) return material;

            var shader = Shader.Find("MobileMarket/UI/Silhouette");
            if (shader == null) { Debug.LogError("[HUDBuilder] Shader MobileMarket/UI/Silhouette not found."); return null; }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SilhouetteMaterialPath);
            return material;
        }

        /// <summary>A block anchored and pivoted to a screen corner.</summary>
        private static RectTransform Corner(string name, RectTransform parent, Vector2 corner, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, pos, size);
            rt.anchorMin = rt.anchorMax = rt.pivot = corner;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>Anchors rt at one point of its parent (pivot centered) and offsets it from there.</summary>
        private static void Pin(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
        }

        private static Image Icon(string name, RectTransform parent, string sprite, Vector2 size, Color color)
        {
            var image = AddImage(Rect(name, parent, Vector2.zero, size).gameObject, Art(sprite), color);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Shade(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(GameObject go, string text, int size, TextAnchor anchor, Color color, Font font)
        {
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---------- generated textures ----------

        /// <summary>White where the source is bright, transparent where it's black (keeps the anti-aliased edges).</summary>
        private static void LuminanceToAlpha(Texture2D tex)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                byte lum = (byte)((px[i].r * 299 + px[i].g * 587 + px[i].b * 114) / 1000);
                // Pull the near-black noise of the generated background down to fully transparent.
                byte a = lum < 24 ? (byte)0 : lum;
                px[i] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        /// <summary>Anti-aliased white disc, or a ring of the given thickness (share of the radius) when thickness > 0.</summary>
        private static Texture2D MakeDisc(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            float inner = thickness > 0f ? radius * (1f - thickness * 2f) : -1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    if (inner > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeSolid(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static void WriteSprite(Texture2D tex, string spriteName, int maxSize)
        {
            string path = TexDir + spriteName + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }
    }
}
