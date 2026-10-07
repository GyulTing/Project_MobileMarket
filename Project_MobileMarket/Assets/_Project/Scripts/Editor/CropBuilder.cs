using UnityEditor;
using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Turns the VARCO crop exports (Models/Varco_Item/작물/Crop_{Crop}_{Stage}.fbx) into per-stage field prefabs
    /// (Prefabs/Items/P_Crop_{Crop}_{Stage}) and assigns them to each SeedData.stagePrefabs.
    /// Each prefab plants several copies on the 1.5 x 1.5 plot with a little rotation/size variation (mushrooms grow on two logs).
    /// The temporary root sits at the world origin, so world bounds equal prefab-local positions. Missing models are skipped.
    /// </summary>
    public static class CropBuilder
    {
        private const string ModelDir = "Assets/_Project/Models/Varco_Item/작물/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Items/";
        private const string ItemDir = "Assets/_Project/Resources/Items/";

        // Heights below are real-world meters; the player is 2.21 units tall, so 1 m = 2.21 / 1.75 units.
        private const float WorldScale = 2.21f / 1.75f;

        public static readonly string[] Stages = { "Sprout", "Growing", "Ready" };

        /// <summary>
        /// Grid2x2: plants scaled to the stage height, four in a 2x2 grid (or two on the diagonal for heavy models).
        /// TwoLogs: two logs lying side by side, scaled to LogLength.
        /// </summary>
        public enum Layout { Grid2x2, TwoLogs }

        // Log length in world units (the plot is 1.5 wide), used instead of the stage height for TwoLogs.
        private const float LogLength = 1.2f;

        public struct Crop
        {
            public string name;
            public Layout layout;
            public float[] heights; // per stage, meters
            public int[] plants;    // per stage, 4 or 2 (Grid2x2 only)
            public Crop(string name, Layout layout, float sprout, float growing, float ready,
                int sproutPlants = 4, int growingPlants = 4, int readyPlants = 4)
            {
                this.name = name;
                this.layout = layout;
                heights = new[] { sprout, growing, ready };
                plants = new[] { sproutPlants, growingPlants, readyPlants };
            }
        }

        // Rice, wheat and kidney beans came out of VARCO at 2.6-7k triangles per plant (thin leaves), so their heavy stages plant two.
        public static readonly Crop[] Crops =
        {
            new Crop("Tomato", Layout.Grid2x2, 0.25f, 0.4f, 0.6f),
            new Crop("Chili", Layout.Grid2x2, 0.25f, 0.35f, 0.5f),
            new Crop("Corn", Layout.Grid2x2, 0.25f, 0.6f, 0.94f),
            new Crop("KidneyBean", Layout.Grid2x2, 0.25f, 0.35f, 0.5f, readyPlants: 2),
            new Crop("Potato", Layout.Grid2x2, 0.25f, 0.3f, 0.4f),
            new Crop("Rice", Layout.Grid2x2, 0.25f, 0.45f, 0.6f, growingPlants: 2, readyPlants: 2),
            new Crop("Wheat", Layout.Grid2x2, 0.25f, 0.45f, 0.6f, sproutPlants: 2, growingPlants: 2, readyPlants: 2),
            new Crop("Mushroom", Layout.TwoLogs, 0f, 0f, 0f),
        };

        public static string ModelPath(string crop, string stage) => ModelDir + "Crop_" + crop + "_" + stage + ".fbx";
        public static string PrefabPath(string crop, string stage) => PrefabDir + "P_Crop_" + crop + "_" + stage + ".prefab";

        [MenuItem("MobileMarket/Items/Build Crop Stages")]
        public static void BuildAll()
        {
            foreach (var crop in Crops)
            {
                var prefabs = new GameObject[Stages.Length];
                bool complete = true;
                for (int s = 0; s < Stages.Length; s++)
                {
                    prefabs[s] = BuildStage(crop, s);
                    if (prefabs[s] == null) complete = false;
                }

                var seed = AssetDatabase.LoadAssetAtPath<SeedData>(ItemDir + "Seed_" + crop.name + ".asset");
                if (seed == null) { Debug.LogWarning("[CropBuilder] Missing seed for " + crop.name); continue; }
                if (!complete) { Debug.LogWarning("[CropBuilder] " + crop.name + " is missing a stage model; seed left unchanged."); continue; }

                seed.stagePrefabs = prefabs;
                EditorUtility.SetDirty(seed);
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildStage(Crop crop, int stage)
        {
            string stageName = Stages[stage];
            string modelPath = ModelPath(crop.name, stageName);
            if (AssetImporter.GetAtPath(modelPath) == null) return null;

            string assetName = "Crop_" + crop.name + "_" + stageName;
            VarcoCharacterTools.SetupStaticModel(modelPath, assetName);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            var root = new GameObject("P_" + assetName);
            float height = crop.heights[stage] * WorldScale;
            var random = new System.Random(assetName.GetHashCode());

            switch (crop.layout)
            {
                case Layout.Grid2x2:
                    const float offset = 0.37f;
                    const float jitter = 0.06f;
                    bool diagonalOnly = crop.plants[stage] == 2;
                    for (int x = -1; x <= 1; x += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            if (diagonalOnly && x != z) continue;
                            var pos = new Vector3(x * offset + Range(random, -jitter, jitter), 0f, z * offset + Range(random, -jitter, jitter));
                            Plant(model, root.transform, pos, Range(random, 0f, 360f), height * Range(random, 0.9f, 1.1f));
                        }
                    break;

                case Layout.TwoLogs:
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var pos = new Vector3(Range(random, -0.05f, 0.05f), 0f, z * 0.33f);
                        // Logs keep their length along X; flip one end-for-end so the pair doesn't look copy-pasted.
                        PlantLog(model, root.transform, pos, (z > 0 ? 180f : 0f) + Range(random, -6f, 6f), LogLength * Range(random, 0.95f, 1.05f));
                    }
                    break;
            }

            string prefabPath = PrefabPath(crop.name, stageName);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>One plant: scaled to the given height, centered on position with its base on the ground (y = 0).</summary>
        private static void Plant(GameObject model, Transform parent, Vector3 position, float yaw, float height)
        {
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            plant.transform.localPosition = Vector3.zero;
            // Keep the importer's axis-conversion rotation on the model root; only spin around world up.
            plant.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            plant.transform.localScale = Vector3.one;

            var bounds = WorldBounds(plant);
            if (bounds.size.y <= 0f) return;
            plant.transform.localScale = Vector3.one * (height / bounds.size.y);

            bounds = WorldBounds(plant);
            plant.transform.localPosition = position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        /// <summary>One log: turned so its long side runs along X, scaled to the given length, base on the ground.</summary>
        private static void PlantLog(GameObject model, Transform parent, Vector3 position, float yaw, float length)
        {
            var log = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            log.transform.localPosition = Vector3.zero;
            log.transform.localScale = Vector3.one;

            var bounds = WorldBounds(log);
            float baseYaw = bounds.size.z > bounds.size.x ? 90f : 0f;
            log.transform.localRotation = Quaternion.Euler(0f, baseYaw + yaw, 0f) * model.transform.localRotation;

            bounds = WorldBounds(log);
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest <= 0f) return;
            log.transform.localScale = Vector3.one * (length / longest);

            bounds = WorldBounds(log);
            log.transform.localPosition = position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
