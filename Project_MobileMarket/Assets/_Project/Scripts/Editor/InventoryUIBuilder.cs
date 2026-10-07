using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Data;
using MobileMarket.Managers;
using MobileMarket.Systems;
using MobileMarket.UI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Builds the cartoon inventory screen (equipment, player backpack grid, loot crate grid) under the open scene's Canvas,
    /// saves it as Prefabs/UI/P_InventoryScreen and wires it into the UIManager.
    /// Art: Textures/T_UI_* (generated, white background removed by UiArtImporter). Fonts: Black Han Sans (titles), Do Hyeon (body).
    /// </summary>
    public static class InventoryUIBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/P_InventoryScreen.prefab";
        private const string TexDir = "Assets/_Project/Textures/";
        private const string FontDir = "Assets/_Project/Fonts/";

        private static readonly Color Dim = new Color(0.03f, 0.03f, 0.02f, 0.72f);
        private static readonly Color GridBacking = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color TitleColor = new Color32(240, 232, 214, 255);
        private static readonly Color TextColor = new Color32(232, 222, 200, 255);
        private static readonly Color CaptionColor = new Color32(170, 160, 140, 255);

        private const float CellSize = 56f;
        private const float CellSpacing = 5f;

        private static Font titleFont, bodyFont;
        private static Sprite pocketsArt, crateArt, equipArt, brushArt, slotArt;

        // The flat middle of the open cloth bundle (T_UI_Panel_Pockets), normalized, origin bottom-left.
        private static readonly Rect PocketsGridArea = new Rect(0.2f, 0.23f, 0.58f, 0.57f);

        /// <summary>Re-imports the generated art from the given PNGs (white background removed) into Textures/T_UI_*.</summary>
        public static void ImportArt(string cratePng, string equipPng, string brushPng, string slotPng)
        {
            UiArtImporter.Import(cratePng, "T_UI_CratePanel");
            UiArtImporter.Import(equipPng, "T_UI_EquipPanel");
            UiArtImporter.Import(brushPng, "T_UI_TitleBrush", true);
            UiArtImporter.Import(slotPng, "T_UI_Slot", false, 256);

            // The slot frame is 9-sliced so long equipment slots keep a thin border.
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexDir + "T_UI_Slot.png");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "T_UI_Slot.png");
            float border = Mathf.Min(tex.width, tex.height) * 0.14f;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        [MenuItem("MobileMarket/UI/Build Inventory Screen")]
        public static void Build()
        {
            titleFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "BlackHanSans-Regular.ttf");
            bodyFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "DoHyeon-Regular.ttf");
            if (titleFont == null || bodyFont == null) titleFont = bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pocketsArt = Art("T_UI_Panel_Pockets");
            crateArt = Art("T_UI_CratePanel");
            equipArt = Art("T_UI_EquipPanel");
            brushArt = Art("T_UI_TitleBrush");
            slotArt = Art("T_UI_Slot");

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[InventoryUIBuilder] No Canvas in the open scene."); return; }

            foreach (var oldName in new[] { "LootPanel", "BagPanel", "InventoryScreen", "P_InventoryScreen" })
            {
                var old = canvas.transform.Find(oldName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var root = Rect("P_InventoryScreen", canvas.transform, Vector2.zero, Vector2.zero);
            Stretch(root);
            AddImage(root.gameObject, null, Dim);
            var screen = root.gameObject.AddComponent<InventoryScreenUI>();

            var cellTemplate = BuildCell("CellTemplate", root, new Vector2(CellSize, CellSize), null, false, default);
            cellTemplate.gameObject.SetActive(false);

            // Equipment: canvas tarp with the weapon slot on the left and head / body / backpack on the right.
            var equip = ArtPanel("EquipmentPanel", root, new Vector2(-680f, -20f), new Vector2(400f, 640f), equipArt, "장비");
            var slots = new[]
            {
                BuildCell("Slot_Primary", equip, new Vector2(130f, 540f), "주무기", true, EquipSlot.Primary, new Vector2(-95f, -20f), true),
                BuildCell("Slot_Head", equip, new Vector2(170f, 150f), "머리", true, EquipSlot.Head, new Vector2(75f, 175f)),
                BuildCell("Slot_Body", equip, new Vector2(170f, 190f), "몸", true, EquipSlot.Body, new Vector2(75f, -20f)),
                BuildCell("Slot_Backpack", equip, new Vector2(170f, 160f), "가방", true, EquipSlot.Backpack, new Vector2(75f, -220f)),
            };

            // The player panel's drawing is swapped at runtime to match the worn backpack (or the open bundle without one).
            var playerGrid = BuildGridView("PlayerGrid", root, new Vector2(-150f, -20f), new Vector2(600f, 760f), pocketsArt, cellTemplate);
            var lootGrid = BuildGridView("LootGrid", root, new Vector2(480f, -20f), new Vector2(540f, 700f), crateArt, cellTemplate);

            var status = AddText(Rect("Status", root, new Vector2(0f, -420f), new Vector2(900f, 50f)).gameObject, "", 30, TextAnchor.MiddleCenter, new Color32(255, 170, 110, 255), titleFont);
            status.gameObject.AddComponent<Outline>().effectColor = Color.black;

            var ghost = AddImage(Rect("DragGhost", root, Vector2.zero, new Vector2(90f, 90f)).gameObject, null, Color.white);
            ghost.preserveAspect = true;
            ghost.raycastTarget = false;

            var so = new SerializedObject(screen);
            var slotArray = so.FindProperty("equipSlots");
            slotArray.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotArray.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("playerGrid").objectReferenceValue = playerGrid;
            so.FindProperty("lootGrid").objectReferenceValue = lootGrid;
            so.FindProperty("dragGhost").objectReferenceValue = ghost;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("pocketsArt").objectReferenceValue = pocketsArt;
            so.FindProperty("pocketsGridArea").rectValue = PocketsGridArea;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);

            var manager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                var mso = new SerializedObject(manager);
                mso.FindProperty("inventoryScreen").objectReferenceValue = root.GetComponent<InventoryScreenUI>();
                if (mso.FindProperty("playerInventory").objectReferenceValue == null)
                    mso.FindProperty("playerInventory").objectReferenceValue = Object.FindFirstObjectByType<Inventory>();
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }

        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + name + ".png");

        private static ItemGridViewUI BuildGridView(string name, RectTransform parent, Vector2 pos, Vector2 size, Sprite art, ItemCellUI template)
        {
            var panel = ArtPanel(name, parent, pos, size, art, "");
            var view = panel.gameObject.AddComponent<ItemGridViewUI>();
            var title = panel.Find("TitleBrush/Title").GetComponent<Text>();

            // Grid with its own dark backing that hugs the cells, so any size (3x3 .. 6x7) reads well on the art.
            // Its position and cell size are fitted to the drawing at runtime (ItemGridViewUI.FitToArea).
            var content = Rect("Cells", panel, Vector2.zero, Vector2.zero);
            AddImage(content.gameObject, null, GridBacking);
            var layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CellSize, CellSize);
            layout.spacing = new Vector2(CellSpacing, CellSpacing);
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var so = new SerializedObject(view);
            so.FindProperty("cellTemplate").objectReferenceValue = template;
            so.FindProperty("layout").objectReferenceValue = layout;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("background").objectReferenceValue = panel.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>A panel drawn with generated art (aspect kept) and a black brush-stroke title above it.</summary>
        private static RectTransform ArtPanel(string name, RectTransform parent, Vector2 pos, Vector2 size, Sprite art, string heading)
        {
            var panel = Rect(name, parent, pos, size);
            var bg = AddImage(panel.gameObject, art, Color.white);
            bg.preserveAspect = true;

            var brush = Rect("TitleBrush", panel, new Vector2(0f, size.y * 0.5f + 8f), new Vector2(380f, 84f));
            AddImage(brush.gameObject, brushArt, Color.white).raycastTarget = false;
            // Only one line fits, so best-fit shrinks long names instead of wrapping.
            var title = AddText(Rect("Title", brush, new Vector2(0f, 2f), new Vector2(340f, 44f)).gameObject, heading, 34, TextAnchor.MiddleCenter, TitleColor, titleFont);
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 34;
            return panel;
        }

        private static ItemCellUI BuildCell(string name, RectTransform parent, Vector2 size, string caption, bool equip, EquipSlot slot, Vector2 pos = default, bool rotateIcon = false)
        {
            var cell = Rect(name, parent, pos, size);
            var frame = AddImage(cell.gameObject, slotArt, Color.white);
            // Big equipment slots are 9-sliced (thin frame at any size); small grid cells just scale the whole tile.
            if (equip)
            {
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2f;
            }

            if (!string.IsNullOrEmpty(caption))
            {
                var cap = Rect("Caption", cell, new Vector2(0f, size.y * 0.5f - 20f), new Vector2(size.x - 20f, 26f));
                AddText(cap.gameObject, caption, 20, TextAnchor.MiddleCenter, CaptionColor, bodyFont);
            }

            // Upright weapon slots turn the icon 90 degrees, so its rect is laid out with width/height swapped.
            // Equipment icons sit below the caption.
            var iconSize = rotateIcon ? new Vector2(size.y - 80f, size.x - 30f) : equip ? size - new Vector2(30f, 54f) : size - new Vector2(14f, 14f);
            var icon = AddImage(Rect("Icon", cell, equip ? new Vector2(0f, -14f) : Vector2.zero, iconSize).gameObject, null, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var label = AddText(Rect("Label", cell, Vector2.zero, size - new Vector2(8f, 8f)).gameObject, "", 14, TextAnchor.MiddleCenter, TextColor, bodyFont);
            var badge = AddText(Rect("Badge", cell, new Vector2(0f, -size.y * 0.5f + 12f), new Vector2(size.x - 10f, 20f)).gameObject, "", 14, TextAnchor.LowerRight, new Color32(255, 214, 120, 255), bodyFont);
            badge.gameObject.AddComponent<Outline>().effectColor = Color.black;

            var ui = cell.gameObject.AddComponent<ItemCellUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("badge").objectReferenceValue = badge;
            so.FindProperty("isEquipSlot").boolValue = equip;
            so.FindProperty("slot").enumValueIndex = (int)slot;
            so.FindProperty("rotateIcon").boolValue = rotateIcon;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
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
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
