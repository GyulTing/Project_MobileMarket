using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MobileMarket.Data;
using MobileMarket.Environment;
using MobileMarket.Managers;
using MobileMarket.Player;
using MobileMarket.UI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Sets up truck travel: the region assets (Resources/Regions), the world map art (Textures/T_UI_WorldMap), the world
    /// map screen (Prefabs/UI/P_WorldMapScreen), the scene loader with its loading screen (Prefabs/UI/P_SceneLoader), and
    /// per scene the loader, the map under the Canvas, the ParkingSpots and the Build Settings entry.
    /// </summary>
    public static class TravelSystemBuilder
    {
        private const string RegionDir = "Assets/_Project/Resources/Regions/";
        private const string MapTexPath = "Assets/_Project/Textures/T_UI_WorldMap.png";
        private const string MapPrefabPath = "Assets/_Project/Prefabs/UI/P_WorldMapScreen.prefab";
        private const string LoaderPrefabPath = "Assets/_Project/Prefabs/UI/P_SceneLoader.prefab";
        private const string TruckPrefabPath = "Assets/_Project/Prefabs/Player/P_PlayerTruck.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/P_Player.prefab";
        private const string FontDir = "Assets/_Project/Fonts/";

        private const string HomeScene = "Assets/_Project/Scenes/Main/Player_Settlement.unity";
        private const string WaypointScene = "Assets/_Project/Scenes/Main/Small_Settlement 2.unity";
        private const string TestScene = "Assets/_Project/Scenes/Test/TestScene.unity";

        private static readonly Color TextColor = new Color32(232, 226, 210, 255);
        private static readonly Color CaptionColor = new Color32(165, 158, 140, 255);
        private static readonly Color Accent = new Color32(214, 160, 70, 255);

        private static Font titleFont, bodyFont;
        private static Sprite circle;

        // ---------------------------------------------------------------- regions

        [MenuItem("MobileMarket/Travel/1. Create Regions")]
        public static void CreateRegions()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Regions")) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Regions");

            // Map positions are picked on T_UI_WorldMap (normalized, origin bottom-left).
            var home = Region("Region_Home", "HOME", RegionType.Home, "Player_Settlement", new Vector2(0.69f, 0.52f), false);
            var waypoint = Region("Region_Waypoint", "WAYPOINT", RegionType.Settlement, "Small_Settlement 2", new Vector2(0.61f, 0.26f), false);
            var rust = Region("Region_RustMarket", "RUST MARKET", RegionType.Settlement, "", new Vector2(0.70f, 0.78f), true);
            var wastes = Region("Region_AshenWastes", "ASHEN WASTES", RegionType.Combat, "TestScene", new Vector2(0.27f, 0.45f), false);
            var woods = Region("Region_BlackpineWoods", "BLACKPINE WOODS", RegionType.Combat, "TestScene", new Vector2(0.80f, 0.66f), false);

            Connect(home, waypoint, rust, wastes, woods);
            Connect(waypoint, wastes);
            Connect(rust);
            Connect(wastes);
            Connect(woods);
            AssetDatabase.SaveAssets();
        }

        private static RegionData Region(string asset, string name, RegionType type, string scene, Vector2 mapPos, bool locked)
        {
            string path = RegionDir + asset + ".asset";
            var region = AssetDatabase.LoadAssetAtPath<RegionData>(path);
            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionData>();
                AssetDatabase.CreateAsset(region, path);
            }
            var so = new SerializedObject(region);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("type").enumValueIndex = (int)type;
            so.FindProperty("sceneName").stringValue = scene;
            so.FindProperty("mapPosition").vector2Value = mapPos;
            so.FindProperty("locked").boolValue = locked;
            so.ApplyModifiedPropertiesWithoutUndo();
            return region;
        }

        private static void Connect(RegionData region, params RegionData[] others)
        {
            var so = new SerializedObject(region);
            var list = so.FindProperty("connections");
            list.arraySize = others.Length;
            for (int i = 0; i < others.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = others[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RegionData[] AllRegions() => new[]
        {
            Load("Region_Home"), Load("Region_Waypoint"), Load("Region_RustMarket"), Load("Region_AshenWastes"), Load("Region_BlackpineWoods"),
        };

        private static RegionData Load(string asset) => AssetDatabase.LoadAssetAtPath<RegionData>(RegionDir + asset + ".asset");

        // ---------------------------------------------------------------- art

        /// <summary>Imports the generated world map image as a single UI sprite.</summary>
        public static void ImportMapArt(string sourcePng)
        {
            System.IO.File.Copy(sourcePng, MapTexPath, true);
            AssetDatabase.ImportAsset(MapTexPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(MapTexPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- world map screen

        [MenuItem("MobileMarket/Travel/2. Build World Map Screen (open scene)")]
        public static void BuildWorldMapScreen()
        {
            LoadStyle();
            var canvas = UICanvas(SceneManager.GetActiveScene());
            if (canvas == null) { Debug.LogError("[TravelSystemBuilder] No root \"Canvas\" in the open scene."); return; }
            var old = canvas.transform.Find("P_WorldMapScreen");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = Rect("P_WorldMapScreen", canvas.transform, Vector2.zero, Vector2.zero);
            Stretch(root);
            Img(root.gameObject, null, new Color(0.02f, 0.02f, 0.02f, 0.92f));
            var screen = root.gameObject.AddComponent<WorldMapUI>();

            var title = Txt(Rect("Title", root, new Vector2(-850f, 470f), new Vector2(600f, 60f)), "WORLD MAP", 40, TextAnchor.MiddleLeft, TextColor, titleFont);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            // Map image (3:2), with a thin frame.
            var frame = Img(Rect("MapFrame", root, new Vector2(-190f, -10f), new Vector2(1332f, 888f)).gameObject, null, new Color(0.55f, 0.52f, 0.45f, 0.6f));
            var mapImage = Img(Rect("Map", frame.rectTransform, Vector2.zero, new Vector2(1320f, 880f)).gameObject, AssetDatabase.LoadAssetAtPath<Sprite>(MapTexPath), new Color(0.82f, 0.82f, 0.8f, 1f));
            var map = mapImage.rectTransform;
            var routes = Rect("Routes", map, Vector2.zero, Vector2.zero);
            Stretch(routes);

            var routeDot = Img(Rect("RouteDotTemplate", routes, Vector2.zero, new Vector2(5f, 5f)).gameObject, circle, new Color(0.92f, 0.9f, 0.82f, 0.75f));
            routeDot.raycastTarget = false;

            var node = BuildNodeTemplate(map);

            // Info box on the right.
            var info = Img(Rect("InfoBox", root, new Vector2(700f, 80f), new Vector2(380f, 400f)).gameObject, null, new Color(0.07f, 0.07f, 0.06f, 0.95f));
            info.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.52f, 0.45f, 0.6f);
            var it = info.rectTransform;
            Txt(Rect("Caption", it, new Vector2(0f, 160f), new Vector2(330f, 30f)), "목적지", 22, TextAnchor.MiddleLeft, CaptionColor, bodyFont);
            var infoName = Txt(Rect("Name", it, new Vector2(0f, 110f), new Vector2(330f, 60f)), "", 40, TextAnchor.MiddleLeft, TextColor, titleFont);
            var infoType = Txt(Rect("Type", it, new Vector2(0f, 60f), new Vector2(330f, 34f)), "", 26, TextAnchor.MiddleLeft, Accent, bodyFont);
            var infoStatus = Txt(Rect("Status", it, new Vector2(0f, -10f), new Vector2(330f, 80f)), "", 22, TextAnchor.UpperLeft, CaptionColor, bodyFont);
            var depart = MakeButton(it, "DepartButton", "출발", new Vector2(0f, -140f), new Vector2(330f, 70f), new Color32(170, 110, 40, 255));

            var close = MakeButton(root, "CloseButton", "닫기 (Esc)", new Vector2(700f, -230f), new Vector2(380f, 56f), new Color32(60, 58, 52, 255));
            Txt(Rect("Hint", root, new Vector2(-190f, -480f), new Vector2(1320f, 40f)), "지도에서 지역을 선택하세요", 24, TextAnchor.MiddleCenter, CaptionColor, bodyFont);

            var so = new SerializedObject(screen);
            so.FindProperty("mapArea").objectReferenceValue = map;
            so.FindProperty("routeRoot").objectReferenceValue = routes;
            so.FindProperty("nodeTemplate").objectReferenceValue = node;
            so.FindProperty("routeDotTemplate").objectReferenceValue = routeDot;
            so.FindProperty("infoName").objectReferenceValue = infoName;
            so.FindProperty("infoType").objectReferenceValue = infoType;
            so.FindProperty("infoStatus").objectReferenceValue = infoStatus;
            so.FindProperty("departButton").objectReferenceValue = depart;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, MapPrefabPath, InteractionMode.AutomatedAction);
            WireUIManager(root.GetComponent<WorldMapUI>());
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }

        private static RegionNodeUI BuildNodeTemplate(RectTransform map)
        {
            var t = Rect("NodeTemplate", map, Vector2.zero, new Vector2(40f, 40f));
            var hit = Img(t.gameObject, null, new Color(0f, 0f, 0f, 0f)); // click area
            var ring = Img(Rect("CurrentRing", t, Vector2.zero, new Vector2(40f, 40f)).gameObject, circle, new Color(0.94f, 0.78f, 0.43f, 0.45f));
            var selected = Img(Rect("Selected", t, Vector2.zero, new Vector2(28f, 28f)).gameObject, circle, new Color(1f, 1f, 1f, 0.35f));
            var dot = Img(Rect("Dot", t, Vector2.zero, new Vector2(16f, 16f)).gameObject, circle, Color.white);
            dot.gameObject.AddComponent<Outline>().effectColor = Color.black;
            foreach (var g in new Graphic[] { ring, selected, dot }) g.raycastTarget = false;

            var name = Txt(Rect("Name", t, new Vector2(18f, 4f), new Vector2(320f, 34f)), "REGION", 26, TextAnchor.MiddleLeft, TextColor, titleFont);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            name.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            name.raycastTarget = false;
            var status = Txt(Rect("Status", t, new Vector2(20f, -20f), new Vector2(240f, 22f)), "NOT AVAILABLE", 16, TextAnchor.MiddleLeft, CaptionColor, bodyFont);
            status.rectTransform.pivot = new Vector2(0f, 0.5f);
            status.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            status.raycastTarget = false;

            // The click area covers the dot and the name.
            hit.rectTransform.sizeDelta = new Vector2(48f, 48f);
            var button = t.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            var nameHit = Img(Rect("NameHit", t, new Vector2(18f, 0f), new Vector2(300f, 48f)).gameObject, null, new Color(0f, 0f, 0f, 0f));
            nameHit.rectTransform.pivot = new Vector2(0f, 0.5f);

            var node = t.gameObject.AddComponent<RegionNodeUI>();
            var so = new SerializedObject(node);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("dot").objectReferenceValue = dot;
            so.FindProperty("currentRing").objectReferenceValue = ring;
            so.FindProperty("selectedFrame").objectReferenceValue = selected;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            return node;
        }

        /// <summary>
        /// The scene's main screen-space UI canvas (the root object named "Canvas"); the loading screen and the player's
        /// head-label canvases are other Canvases and must not be picked.
        /// </summary>
        private static Canvas UICanvas(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Canvas" && go.TryGetComponent(out Canvas canvas)) return canvas;
            return null;
        }

        private static void WireUIManager(WorldMapUI map)
        {
            var manager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (manager == null) return;
            var so = new SerializedObject(manager);
            so.FindProperty("worldMap").objectReferenceValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- scene loader

        [MenuItem("MobileMarket/Travel/3. Build Scene Loader Prefab")]
        public static void BuildSceneLoaderPrefab()
        {
            LoadStyle();
            var root = new GameObject("P_SceneLoader");
            var loader = root.AddComponent<SceneLoader>();

            var canvasGo = new GameObject("LoadingCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var screen = Rect("LoadingScreen", canvasGo.transform, Vector2.zero, Vector2.zero);
            Stretch(screen);
            Img(screen.gameObject, null, Color.black);
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            var text = Txt(Rect("Text", screen, new Vector2(0f, -380f), new Vector2(1200f, 60f)), "", 34, TextAnchor.MiddleCenter, TextColor, titleFont);

            var so = new SerializedObject(loader);
            var regions = AllRegions();
            var list = so.FindProperty("regions");
            list.arraySize = regions.Length;
            for (int i = 0; i < regions.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = regions[i];
            so.FindProperty("homeRegion").objectReferenceValue = regions[0];
            so.FindProperty("truckPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefabPath);
            so.FindProperty("loadingScreen").objectReferenceValue = group;
            so.FindProperty("loadingText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, LoaderPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- per scene

        /// <summary>The player prefab needs PlayerArrival so SceneLoader can place it.</summary>
        public static void EnsurePlayerArrival()
        {
            var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (contents.GetComponent<PlayerArrival>() == null)
            {
                contents.AddComponent<PlayerArrival>();
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>
        /// In the open scene: adds the scene loader, the world map under the Canvas (wired to UIManager) and the given parking
        /// spots (replacing earlier ones), then saves the scene.
        /// </summary>
        public static void SetupOpenScene(params Pose[] spots)
        {
            var scene = SceneManager.GetActiveScene();

            bool hasLoader = false;
            foreach (var go in scene.GetRootGameObjects()) if (go.GetComponent<SceneLoader>() != null) hasLoader = true;
            if (!hasLoader) PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LoaderPrefabPath), scene);

            var canvas = UICanvas(scene);
            if (canvas != null && canvas.transform.Find("P_WorldMapScreen") == null)
            {
                var map = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath), canvas.transform);
                map.SetActive(false);
                WireUIManager(map.GetComponent<WorldMapUI>());
            }

            foreach (var go in scene.GetRootGameObjects()) if (go.name == "ParkingSpots") Object.DestroyImmediate(go);
            var group = new GameObject("ParkingSpots");
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = new GameObject("ParkingSpot_" + (i + 1));
                spot.transform.SetParent(group.transform, false);
                spot.transform.SetPositionAndRotation(spots[i].position, spots[i].rotation);
                spot.AddComponent<ParkingSpot>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("MobileMarket/Travel/4. Register Build Scenes")]
        public static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(HomeScene, true),
                new EditorBuildSettingsScene(WaypointScene, true),
                new EditorBuildSettingsScene(TestScene, true),
            };
        }

        // ---------------------------------------------------------------- UI helpers

        private static void LoadStyle()
        {
            titleFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "BlackHanSans-Regular.ttf");
            bodyFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "DoHyeon-Regular.ttf");
            if (titleFont == null || bodyFont == null) titleFont = bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
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
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Image Img(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private static Text Txt(RectTransform rt, string text, int size, TextAnchor anchor, Color color, Font font)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, pos, size);
            var img = Img(rt.gameObject, null, color);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            button.colors = colors;
            var text = Txt(Rect("Label", rt, Vector2.zero, size), label, 30, TextAnchor.MiddleCenter, TextColor, titleFont);
            text.raycastTarget = false;
            return button;
        }
    }
}
