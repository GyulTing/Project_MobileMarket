using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Turns generated UI art (drawn on a plain white background) into transparent, trimmed sprites under Textures/.
    /// The white background is removed by flood fill from the image border, so white highlights inside the art survive.
    /// </summary>
    public static class UiArtImporter
    {
        private const string OutDir = "Assets/_Project/Textures/";
        private const int WhiteThreshold = 232;

        /// <param name="removeAllWhite">Also clear enclosed white areas (for brush strokes with gaps).</param>
        public static Sprite Import(string sourcePng, string spriteName, bool removeAllWhite = false, int maxSize = 1024)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(sourcePng));
            RemoveWhiteBackground(tex, removeAllWhite);

            var trimmed = Trim(tex);
            string path = OutDir + spriteName + ".png";
            File.WriteAllBytes(path, trimmed.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(trimmed);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Makes the white background transparent (flood fill from the border, or every white pixel when removeAllWhite).</summary>
        public static void RemoveWhiteBackground(Texture2D tex, bool removeAllWhite = false)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();

            var clear = new bool[px.Length];
            if (removeAllWhite)
            {
                for (int i = 0; i < px.Length; i++) clear[i] = IsWhite(px[i]);
            }
            else
            {
                FloodFromBorder(px, w, h, clear);
            }

            for (int i = 0; i < px.Length; i++)
            {
                if (clear[i]) px[i] = new Color32(0, 0, 0, 0); // black, not white, so scaling never bleeds a white halo
                else if (IsNearWhite(px[i]) && TouchesCleared(i, w, h, clear)) px[i].a = 96; // soften the fringe
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        private static bool IsWhite(Color32 c) => c.r >= WhiteThreshold && c.g >= WhiteThreshold && c.b >= WhiteThreshold;
        private static bool IsNearWhite(Color32 c) => c.r >= 200 && c.g >= 200 && c.b >= 200;

        private static void FloodFromBorder(Color32[] px, int w, int h, bool[] clear)
        {
            var stack = new Stack<int>();
            for (int x = 0; x < w; x++) { stack.Push(x); stack.Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { stack.Push(y * w); stack.Push(y * w + w - 1); }

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                if (clear[i] || !IsWhite(px[i])) continue;
                clear[i] = true;
                int x = i % w, y = i / w;
                if (x > 0) stack.Push(i - 1);
                if (x < w - 1) stack.Push(i + 1);
                if (y > 0) stack.Push(i - w);
                if (y < h - 1) stack.Push(i + w);
            }
        }

        private static bool TouchesCleared(int i, int w, int h, bool[] clear)
        {
            int x = i % w, y = i / w;
            return (x > 0 && clear[i - 1]) || (x < w - 1 && clear[i + 1]) || (y > 0 && clear[i - w]) || (y < h - 1 && clear[i + w]);
        }

        public static Texture2D Trim(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (maxX < 0) return Object.Instantiate(tex);

            int tw = maxX - minX + 1, th = maxY - minY + 1;
            var result = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            result.SetPixels(tex.GetPixels(minX, minY, tw, th));
            result.Apply();
            return result;
        }
    }
}
