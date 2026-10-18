using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MobileMarket.Enemy;
using MobileMarket.Environment;
using MobileMarket.VFX;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Builds the bunker boss's phase effects: prefabs P_BK_FX_VentSmoke, P_BK_FX_FireSpot and P_BK_RotatingBeacon (fire and
    /// smoke taken from P_BK_FX_WreckBurning), then places them in the open BunkerBoss scene under PhaseFX and wires
    /// BossPhaseEffects. Also gives the vents their break blast and the explosion prefab its camera shake.
    /// </summary>
    public static class BossFxBuilder
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/Bunker/";
        private const string MatDir = "Assets/_Project/Materials/";
        private const string ExplosionPath = PrefabDir + "P_BK_FX_Explosion.prefab";
        private const string WreckBurningPath = PrefabDir + "P_BK_FX_WreckBurning.prefab";
        private const string VentPath = PrefabDir + "P_BK_Vent.prefab";
        private const string VentSmokePath = PrefabDir + "P_BK_FX_VentSmoke.prefab";
        private const string FireSpotPath = PrefabDir + "P_BK_FX_FireSpot.prefab";
        private const string BeaconPath = PrefabDir + "P_BK_RotatingBeacon.prefab";

        private const float ExplosionShake = 0.25f;
        private const float BeaconHeight = 6.5f;
        private static readonly Color BeaconOrange = new Color(1f, 0.5f, 0.08f);

        // Phase 2 fires around the arena floor (beside cargo, cabinets and the side walls, clear of the player's entrance).
        private static readonly Vector3[] ArenaFirePoints =
        {
            new Vector3(-17.2f, 0f, -7.2f), new Vector3(-8.6f, 0f, 2.4f), new Vector3(9.4f, 0f, -4.6f), new Vector3(17.4f, 0f, -5.2f),
            new Vector3(26.6f, 0f, -9.2f), new Vector3(-25.5f, 0f, 10f), new Vector3(25.5f, 0f, 6f), new Vector3(-26f, 0f, -15.2f),
        };

        // Phase 3 fires: two more on the floor, two on the boss unit's monitor face (z = 14), three on each side wall.
        private static readonly Vector3[] Phase3FirePoints =
        {
            new Vector3(4f, 0f, 8f), new Vector3(-4.5f, 0f, -10f),
            new Vector3(-4f, 8f, 13.6f), new Vector3(5f, 10.5f, 13.6f),
            new Vector3(-29f, 3.5f, -5f), new Vector3(-29f, 4.5f, 1f), new Vector3(-29f, 3f, -12f),
            new Vector3(29f, 4f, -2f), new Vector3(29f, 3.2f, -13f), new Vector3(29f, 4.6f, 0f),
        };
        // Wall and monitor fires burn a bit smaller than the floor ones.
        private const float RaisedFireScale = 0.75f;

        [MenuItem("MobileMarket/Bunker/Build Boss Phase FX")]
        public static void Build()
        {
            var boss = Object.FindFirstObjectByType<BossController>();
            if (boss == null) { Debug.LogError("[BossFxBuilder] Open the BunkerBoss scene first (no BossController)."); return; }

            var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
            SetExplosionShake();
            SetVentBreakBlast(explosion);
            BuildVentSmokePrefab();
            BuildFireSpotPrefab(explosion);
            BuildBeaconPrefab();
            PlaceInScene(boss);
            // The prefabs above were regenerated from scratch; put their sounds back.
            BossSfxBuilder.Build();
        }

        // ---------------------------------------------------------------- existing prefabs

        private static void SetExplosionShake()
        {
            var root = PrefabUtility.LoadPrefabContents(ExplosionPath);
            var so = new SerializedObject(root.GetComponent<PooledEffect>());
            so.FindProperty("shakeStrength").floatValue = ExplosionShake;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ExplosionPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetVentBreakBlast(GameObject explosion)
        {
            var root = PrefabUtility.LoadPrefabContents(VentPath);
            var vent = root.GetComponentInChildren<VentHeat>(true);
            if (vent != null)
            {
                var so = new SerializedObject(vent);
                so.FindProperty("breakExplosionPrefab").objectReferenceValue = explosion;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, VentPath);
            }
            else Debug.LogWarning("[BossFxBuilder] P_BK_Vent has no VentHeat; set the break blast on the scene vents instead.");
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------------------------------------------------------------- new prefabs

        /// <summary>A column of dark smoke rising from a vent (the wreck smoke, narrowed and made taller).</summary>
        private static void BuildVentSmokePrefab()
        {
            var root = new GameObject("P_BK_FX_VentSmoke");
            var smoke = CopyChild(WreckBurningPath, "Smoke", root.transform);
            var main = smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.gravityModifier = -0.05f;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // straight up
            shape.scale = Vector3.one;
            var emission = smoke.emission;
            emission.rateOverTime = 14f;
            LightenSmoke(smoke);
            Save(root, VentSmokePath);
        }

        /// <summary>Flames, smoke and embers with a flickering firelight (from the wreck fire), lit by FireSpot.</summary>
        private static void BuildFireSpotPrefab(GameObject explosion)
        {
            var root = new GameObject("P_BK_FX_FireSpot");
            foreach (var part in new[] { "Fire", "Smoke", "Sparks" })
            {
                var ps = CopyChild(WreckBurningPath, part, root.transform);
                var main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var shape = ps.shape;
                var emission = ps.emission;
                if (part == "Fire")
                {
                    // Bigger, denser flames than the wreck's so a floor fire reads from the top-down camera.
                    shape.scale = new Vector3(0.9f, 0.3f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                    main.maxParticles = 150;
                    emission.rateOverTime = 50f;
                }
                else if (part == "Smoke")
                {
                    shape.scale = new Vector3(0.9f, 0.3f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
                    emission.rateOverTime = 6f;
                    LightenSmoke(ps);
                }
            }

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 7f;
            light.shadows = LightShadows.None;

            var spot = root.AddComponent<FireSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("igniteExplosionPrefab").objectReferenceValue = explosion;
            so.FindProperty("fireLight").objectReferenceValue = light;
            so.FindProperty("lightIntensity").floatValue = 60f;   // candela, like the rest of the bunker's lights
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, FireSpotPath);
        }

        /// <summary>Ceiling-hung orange beacon: rod, housing, glowing lens and a rotor with two opposite beams angled down.</summary>
        private static void BuildBeaconPrefab()
        {
            var housingMat = LitMaterial("M_BK_BeaconHousing", new Color(0.12f, 0.12f, 0.13f), Color.black);
            var lensMat = LitMaterial("M_BK_BeaconLens", new Color(1f, 0.45f, 0.05f), Color.black);

            var root = new GameObject("P_BK_RotatingBeacon");
            Primitive(PrimitiveType.Cylinder, "Rod", root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.06f, 1.5f, 0.06f), housingMat);
            Primitive(PrimitiveType.Cylinder, "Housing", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.55f, 0.08f, 0.55f), housingMat);
            var lens = Primitive(PrimitiveType.Sphere, "Lens", root.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.45f, 0.4f, 0.45f), lensMat);

            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(root.transform, false);
            rotor.localPosition = new Vector3(0f, -0.15f, 0f);
            var beams = new List<Light>();
            foreach (float yaw in new[] { 0f, 180f })
            {
                var beamGo = new GameObject(yaw == 0f ? "Beam_A" : "Beam_B");
                beamGo.transform.SetParent(rotor, false);
                beamGo.transform.localRotation = Quaternion.Euler(35f, yaw, 0f);
                var beam = beamGo.AddComponent<Light>();
                beam.type = LightType.Spot;
                beam.color = BeaconOrange;
                // Lights here are in candela; the floodlights are 170 cd, so the beams need far more to show over them.
                beam.intensity = 2500f;
                beam.range = 40f;
                beam.spotAngle = 45f;
                beam.innerSpotAngle = 18f;
                beam.shadows = LightShadows.None;
                beams.Add(beam);
            }
            // A soft orange glow around the beacon itself, so the room tints even between sweeps.
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(rotor, false);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = BeaconOrange;
            glow.intensity = 90f;
            glow.range = 14f;
            glow.shadows = LightShadows.None;
            beams.Add(glow);

            var beacon = root.AddComponent<RotatingBeacon>();
            var so = new SerializedObject(beacon);
            so.FindProperty("rotor").objectReferenceValue = rotor;
            so.FindProperty("lensGlow").colorValue = new Color(10f, 3f, 0.3f);
            so.FindProperty("lens").objectReferenceValue = lens.GetComponent<Renderer>();
            var beamProp = so.FindProperty("beams");
            beamProp.arraySize = beams.Count;
            for (int i = 0; i < beams.Count; i++) beamProp.GetArrayElementAtIndex(i).objectReferenceValue = beams[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, BeaconPath);
        }

        // ---------------------------------------------------------------- scene

        private static void PlaceInScene(BossController boss)
        {
            var arena = boss.transform.parent;
            var old = arena.Find("PhaseFX");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var fx = new GameObject("PhaseFX").transform;
            fx.SetParent(arena, false);

            var smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VentSmokePath);
            var firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireSpotPath);
            var beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeaconPath);

            var vents = arena.GetComponentsInChildren<VentHeat>(true);
            var smokes = new List<ParticleSystem>();
            var defeatFires = new List<FireSpot>();
            var ventBlasts = new List<Transform>();
            var smokeGroup = Group("VentSmoke", fx);
            var defeatGroup = Group("DefeatFires", fx);
            var blastGroup = Group("VentBlasts", fx);
            foreach (var vent in vents)
            {
                var smoke = Place(smokePrefab, smokeGroup, "VentSmoke_" + vent.name, vent.FrontPoint + Vector3.up * 0.4f);
                smokes.Add(smoke.GetComponentInChildren<ParticleSystem>());
                var blast = Group("VentBlast_" + vent.name, blastGroup);
                blast.position = vent.FrontPoint;
                ventBlasts.Add(blast);
                defeatFires.Add(Place(firePrefab, defeatGroup, "Fire_" + vent.name, vent.FrontPoint, 0.8f).GetComponent<FireSpot>());
            }
            // Weapons: the 20 mm guns, 40 mm cannons and rocket pods (not the security cameras, which also aim).
            var weapons = new List<Component>();
            weapons.AddRange(arena.GetComponentsInChildren<BossGun20>(true));
            weapons.AddRange(arena.GetComponentsInChildren<BossCannon40>(true));
            weapons.AddRange(arena.GetComponentsInChildren<BossRocketPod>(true));
            foreach (var weapon in weapons)
                defeatFires.Add(Place(firePrefab, defeatGroup, "Fire_" + weapon.name, weapon.transform.position, 0.6f).GetComponent<FireSpot>());

            var arenaFires = new List<FireSpot>();
            var arenaGroup = Group("ArenaFires", fx);
            for (int i = 0; i < ArenaFirePoints.Length; i++)
                arenaFires.Add(Place(firePrefab, arenaGroup, "ArenaFire_" + (i + 1), ArenaFirePoints[i]).GetComponent<FireSpot>());

            var phase3Fires = new List<FireSpot>();
            var phase3Group = Group("Phase3Fires", fx);
            for (int i = 0; i < Phase3FirePoints.Length; i++)
            {
                var p = Phase3FirePoints[i];
                float scale = p.y > 0.5f ? RaisedFireScale : 1f;
                phase3Fires.Add(Place(firePrefab, phase3Group, "Phase3Fire_" + (i + 1), p, scale).GetComponent<FireSpot>());
            }

            var beacon = Place(beaconPrefab, fx, "RotatingBeacon", new Vector3(0f, BeaconHeight, 0f)).GetComponent<RotatingBeacon>();

            var effects = fx.gameObject.AddComponent<BossPhaseEffects>();
            var so = new SerializedObject(effects);
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("arenaLights").objectReferenceValue = Object.FindFirstObjectByType<ArenaLights>();
            Fill(so.FindProperty("ventSmoke"), smokes);
            Fill(so.FindProperty("phase2Fires"), arenaFires);
            Fill(so.FindProperty("defeatFires"), defeatFires);
            so.FindProperty("beacon").objectReferenceValue = beacon;
            so.FindProperty("explosionPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
            Fill(so.FindProperty("ventBlastPoints"), ventBlasts);
            Fill(so.FindProperty("phase3Fires"), phase3Fires);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);
            Debug.Log($"[BossFxBuilder] Placed {smokes.Count} vent smokes, {arenaFires.Count} arena fires, {phase3Fires.Count} phase 3 fires, {defeatFires.Count} defeat fires and the beacon.");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Copies one particle child of a prefab (with its renderer settings) under parent.</summary>
        private static ParticleSystem CopyChild(string prefabPath, string childName, Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform.Find(childName);
            var copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = childName;
            return copy.GetComponent<ParticleSystem>();
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>M_BK_FX_SmokeLight: the smoke material in a lighter gray, so the smoke shows over the dark bunker floor.</summary>
        private static Material LightSmokeMaterial()
        {
            string path = MatDir + "M_BK_FX_SmokeLight.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_BK_FX_Smoke.mat"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.75f, 0.72f, 0.68f, 0.85f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Replaces the copied smoke's near-black color-over-lifetime (made for the wreck's dark smoke) with a mid gray that
        /// fades in, thins out and disappears, so it reads over the dark floor.
        /// </summary>
        private static void LightenSmoke(ParticleSystem smoke)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.57f, 0.54f), 0f), new GradientColorKey(new Color(0.42f, 0.42f, 0.42f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(0f, 1f) });
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = LightSmokeMaterial();
        }

        private static Material LitMaterial(string name, Color baseColor, Color emission)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Place(GameObject prefab, Transform parent, string name, Vector3 position, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static void Fill<T>(SerializedProperty array, List<T> items) where T : Object
        {
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
