using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using MobileMarket.Environment;
using MobileMarket.Player;
using MobileMarket.UI;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Renders the open scene from straight above into Textures/T_UI_Minimap_[Scene].png (desaturated, north = +Z up) and
    /// stores it with the covered area on the scene's MinimapArea (created if missing), then points the HUD's minimap at it.
    /// An existing MinimapArea keeps its bounds, so they can be tuned in the Inspector and re-baked.
    /// </summary>
    public static class MinimapBaker
    {
        private const string TexDir = "Assets/_Project/Textures/";
        // The HUD shows about 6 screen pixels per meter, so the picture aims a bit above that, up to the max texture size.
        private const float TexelsPerMeter = 8f;
        private const int MinResolution = 512;
        private const int MaxResolution = 4096;
        private const float Padding = 2f;
        // How much of the original color survives the desaturation (0 = pure gray).
        private const float ColorKeep = 0.25f;
        private const float Brightness = 0.85f;

        [MenuItem("MobileMarket/UI/Bake Minimap")]
        public static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            var area = Object.FindFirstObjectByType<MinimapArea>();
            if (area == null) area = new GameObject("MinimapArea").AddComponent<MinimapArea>();

            var so = new SerializedObject(area);
            var minProp = so.FindProperty("worldMin");
            var sizeProp = so.FindProperty("worldSize");
            bool firstBake = so.FindProperty("mapImage").objectReferenceValue == null;
            if (firstBake && TryGetSceneBounds(out var bounds))
            {
                minProp.vector2Value = new Vector2(bounds.min.x - Padding, bounds.min.z - Padding);
                sizeProp.vector2Value = new Vector2(bounds.size.x + Padding * 2f, bounds.size.z + Padding * 2f);
            }

            Vector2 min = minProp.vector2Value, size = sizeProp.vector2Value;
            if (size.x <= 0f || size.y <= 0f) { Debug.LogError("[MinimapBaker] MinimapArea has no size."); return; }

            string path = TexDir + "T_UI_Minimap_" + scene.name + ".png";
            // The player is drawn by the HUD's arrow, so it's left out of the picture.
            var player = Object.FindFirstObjectByType<PlayerHealth>();
            bool playerWasActive = player != null && player.gameObject.activeSelf;
            if (playerWasActive) player.gameObject.SetActive(false);
            var tex = Render(min, size);
            if (playerWasActive) player.gameObject.SetActive(true);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxResolution;
            importer.SaveAndReimport();

            so.FindProperty("mapImage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            so.ApplyModifiedPropertiesWithoutUndo();

            var hud = Object.FindFirstObjectByType<MinimapHUD>(FindObjectsInactive.Include);
            if (hud != null)
            {
                var hso = new SerializedObject(hud);
                hso.FindProperty("area").objectReferenceValue = area;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MinimapBaker] Baked {path} covering X {min.x:0.#}..{min.x + size.x:0.#}, Z {min.y:0.#}..{min.y + size.y:0.#}");
        }

        /// <summary>Combined bounds of every visible mesh in the scene (UI excluded).</summary>
        private static bool TryGetSceneBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.gameObject.layer == uiLayer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        private static Texture2D Render(Vector2 min, Vector2 size)
        {
            float aspect = size.x / size.y;
            int longSide = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(size.x, size.y) * TexelsPerMeter), MinResolution, MaxResolution);
            int width = aspect >= 1f ? longSide : Mathf.RoundToInt(longSide * aspect);
            int height = aspect >= 1f ? Mathf.RoundToInt(longSide / aspect) : longSide;

            var camGo = new GameObject("MinimapBakeCamera") { hideFlags = HideFlags.DontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size.y * 0.5f;
            cam.aspect = aspect;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            // Looking straight down with +Z as the image's up.
            camGo.transform.SetPositionAndRotation(new Vector3(min.x + size.x * 0.5f, 300f, min.y + size.y * 0.5f), Quaternion.Euler(90f, 0f, 0f));

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            // URP ignores Camera.Render(); a standard render request draws the camera into the texture.
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
            }

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo);

            Desaturate(tex);
            return tex;
        }

        private static void Desaturate(Texture2D tex)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                var g = new Color(gray, gray, gray);
                px[i] = Color.Lerp(g, c, ColorKeep) * Brightness;
                px[i].a = 1f;
            }
            tex.SetPixels(px);
            tex.Apply();
        }
    }
}
