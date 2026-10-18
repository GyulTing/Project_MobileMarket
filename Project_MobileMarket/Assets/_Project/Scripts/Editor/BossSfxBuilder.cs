using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MobileMarket.Enemy;
using MobileMarket.Environment;
using MobileMarket.Managers;
using MobileMarket.VFX;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Hooks the bunker boss's sound effects (Audio/SFX/SFX_BK_*) into its prefabs (turrets, rounds, ring marker, explosion)
    /// with volumes and limits, then gives the open BunkerBoss scene its floodlight sound and a SoundManager.
    /// Re-run after replacing a clip.
    /// </summary>
    public static class BossSfxBuilder
    {
        private const string SfxDir = "Assets/_Project/Audio/SFX/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Bunker/";
        private const float MinDistance = 12f;
        private const float MaxDistance = 60f;

        [MenuItem("MobileMarket/Bunker/Assign Boss SFX")]
        public static void Build()
        {
            foreach (var name in new[] { "P_BK_Turret20_Hanging", "P_BK_Turret20" })
                EditPrefab(name, root =>
                {
                    AddServo(root);
                    var gun = root.GetComponentInChildren<BossGun20>(true);
                    if (gun != null) SetRef(gun, "fireLoop", LoopSource(gun.gameObject, Clip("SFX_BK_Gun20_Fire_Loop"), 0.7f, true));
                });

            foreach (var name in new[] { "P_BK_Turret40_Hanging", "P_BK_Turret40" })
                EditPrefab(name, root =>
                {
                    AddServo(root);
                    var cannon = root.GetComponentInChildren<BossCannon40>(true);
                    if (cannon == null) return;
                    SetSound(cannon, "lockOnSound", Clip("SFX_BK_Laser_LockOn"), 0.7f, 0f, 0.2f, 2);
                    SetSound(cannon, "fireSound", Clip("SFX_BK_Cannon40_Fire"), 0.9f, 1.5f, 0.08f, 3);
                });

            foreach (var name in new[] { "P_BK_TurretRocket_Hanging", "P_BK_TurretRocket" })
                EditPrefab(name, root =>
                {
                    AddServo(root);
                    var pod = root.GetComponentInChildren<BossRocketPod>(true);
                    if (pod != null) SetSound(pod, "launchSound", Clip("SFX_BK_Rocket_Launch"), 0.7f, 1.2f, 0.1f, 3);
                });

            EditPrefab("P_BK_Bullet20", root =>
            {
                var bullet = root.GetComponent<BossProjectile>();
                SetSound(bullet, "impactSound", Clip("SFX_BK_Bullet20_Hit"), 0.5f, 0f, 0.05f, 4);
                SetSound(bullet, "passSound", Clip("SFX_BK_Bullet20_Pass"), 0.6f, 0f, 0.12f, 2);
            });

            EditPrefab("P_BK_Shell40", root =>
                SetRef(root.GetComponent<BossArtilleryRound>(), "flightSound", LoopSource(root, Clip("SFX_BK_Shell40_Flight"), 0.6f, false)));
            EditPrefab("P_BK_RocketS5", root =>
                SetRef(root.GetComponent<BossArtilleryRound>(), "flightSound", LoopSource(root, Clip("SFX_BK_Rocket_Flight"), 0.35f, false)));

            // The warning clip is long; each ring cuts it off when the ring ends (at most 1.2 s), and a salvo of rings shares a few voices.
            EditPrefab("P_BK_AreaMarker", root =>
                SetSound(root.GetComponent<AreaMarker>(), "warningSound", Clip("SFX_BK_Impact_Warning"), 0.55f, 1.2f, 0.3f, 2));

            EditPrefab("P_BK_FX_Explosion", root =>
            {
                var effect = root.GetComponent<PooledEffect>();
                SetSound(effect, "sound", Clip("SFX_BK_Explosion"), 1f, 0f, 0.04f, 6);
                SetSound(effect, "followUpSound", Clip("SFX_BK_Debris_Fall"), 0.45f, 0f, 0.3f, 2);
            });

            // Vents: sizzle while hot (volume follows the glow), crackle while shorting, steam on opening, clank on hits.
            // Eight vents run these at once, so the loops stay quiet.
            EditPrefab("P_BK_Vent", root =>
            {
                var vent = root.GetComponentInChildren<VentHeat>(true);
                if (vent == null) return;
                SetRef(vent, "heatLoop", LoopSource(vent.gameObject, Clip("SFX_BK_Vent_Heat_Loop"), 0f, true));
                SetRef(vent, "shortLoop", LoopSource(vent.gameObject, Clip("SFX_BK_Vent_Short_Loop"), 0.4f, true));
                SetSound(vent, "steamSound", Clip("SFX_BK_Vent_Steam"), 0.6f, 2.5f, 0.15f, 3);
                // The clank is spent after ~0.6 s; cutting the silent tail frees voices for rapid fire.
                SetSound(vent, "hitSound", Clip("SFX_BK_Vent_Hit"), 0.5f, 0.6f, 0.06f, 4);
                SetSound(vent, "coverSound", Clip("SFX_BK_Vent_Cover"), 0.6f, 0f, 0.05f, 3);
                SetSound(vent, "damagedSound", Clip("SFX_BK_Vent_Damaged"), 0.6f, 2f, 0.1f, 3);
            });

            // Fires and vent smoke: many burn at once, so their loops are quiet and only heard up close.
            EditPrefab("P_BK_FX_FireSpot", root =>
            {
                var fire = root.GetComponent<FireSpot>();
                SetSound(fire, "igniteSound", Clip("SFX_BK_Fire_Ignite"), 0.6f, 2f, 0.1f, 3);
                SetRef(fire, "burnLoop", LoopSource(root, Clip("SFX_BK_Fire_Loop"), 0f, true, 3f, 20f));
                SetFloat(fire, "burnVolume", 0.3f);
            });
            EditPrefab("P_BK_FX_VentSmoke", root => LoopSource(root, Clip("SFX_BK_Vent_Smoke_Loop"), 0.25f, true, 3f, 18f));

            EditPrefab("P_BK_RotatingBeacon", root =>
            {
                var so = new SerializedObject(root.GetComponent<RotatingBeacon>());
                var loops = so.FindProperty("loops");
                loops.arraySize = 2;
                loops.GetArrayElementAtIndex(0).objectReferenceValue = LoopSource(root, Clip("SFX_BK_Beacon_Motor_Loop"), 0.4f, true, 15f, 70f);
                loops.GetArrayElementAtIndex(1).objectReferenceValue = LoopSource(root, Clip("SFX_BK_Beacon_Siren_Loop"), 0.5f, true, 15f, 70f);
                so.ApplyModifiedPropertiesWithoutUndo();
            });

            AssignScene();
            Debug.Log("[BossSfxBuilder] Boss sound effects assigned.");
        }

        private static void AssignScene()
        {
            var lights = Object.FindFirstObjectByType<ArenaLights>();
            if (lights != null)
            {
                SetSound(lights, "switchSound", Clip("SFX_BK_Floodlight_On"), 0.8f, 1.2f, 0.1f, 3);
                SetSound(lights, "dieSound", Clip("SFX_BK_Bulb_Burst"), 0.7f, 0f, 0.08f, 3);
                SetSound(lights, "glitchSound", Clip("SFX_BK_Light_Buzz"), 0.4f, 0f, 0.05f, 4);
                SetSound(lights, "blackoutSound", Clip("SFX_BK_Power_Down"), 0.9f, 0f, 1f, 1, true);
            }

            var destruction = Object.FindFirstObjectByType<BossDestruction>();
            if (destruction != null)
            {
                SetSound(destruction, "alarmSound", Clip("SFX_BK_System_Error"), 0.8f, 0f, 1f, 1, true);
                SetSound(destruction, "finalBlastSound", Clip("SFX_BK_Big_Explosion"), 1f, 0f, 1f, 1, true);
            }

            var boss = Object.FindFirstObjectByType<BossController>();
            if (boss != null)
            {
                var bossAudio = boss.GetComponent<BossAudio>();
                if (bossAudio == null) bossAudio = boss.gameObject.AddComponent<BossAudio>();
                var heat = LoopSource(boss.gameObject, Clip("SFX_BK_Heat_Rise_Loop"), 0f, true);
                heat.spatialBlend = 0f;   // the boss's state, heard the same anywhere in the room
                var so = new SerializedObject(bossAudio);
                so.FindProperty("boss").objectReferenceValue = boss;
                so.FindProperty("music").objectReferenceValue = Music();
                so.FindProperty("heatLoop").objectReferenceValue = heat;
                so.ApplyModifiedPropertiesWithoutUndo();
                SetSound(bossAudio, "overheatSiren", Clip("SFX_BK_Overheat_Siren"), 0.7f, 3f, 1f, 1, true);
                SetSound(bossAudio, "phaseAlarm", Clip("SFX_BK_Phase_Alarm"), 0.9f, 0f, 1f, 1, true);
            }

            if (Object.FindFirstObjectByType<SoundManager>() == null)
                new GameObject("SoundManager").AddComponent<SoundManager>();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>The battle music, kept compressed in memory (small) but seekable without hitches for the section loops.</summary>
        private static AudioClip Music()
        {
            const string path = "Assets/_Project/Audio/BGM/BGM_BK_Battle.mp3";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning("[BossSfxBuilder] Missing " + path); return null; }
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.CompressedInMemory)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---------------------------------------------------------------- helpers

        private static void AddServo(GameObject root)
        {
            var aim = root.GetComponentInChildren<TurretAim>(true);
            if (aim == null) return;
            var source = LoopSource(aim.gameObject, Clip("SFX_BK_Turret_Servo_Loop"), 0f, true);
            var so = new SerializedObject(aim);
            so.FindProperty("servoSource").objectReferenceValue = source;
            so.FindProperty("servoMaxVolume").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetSound(aim, "powerDownSound", Clip("SFX_BK_Turret_PowerDown"), 0.6f, 3f, 0.05f, 3);
        }

        private static void SetFloat(Object target, string property, float value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A 3D AudioSource on the object for a clip (reused if one already holds that clip, so re-running doesn't stack them).
        /// </summary>
        private static AudioSource LoopSource(GameObject go, AudioClip clip, float volume, bool loop,
            float minDistance = MinDistance, float maxDistance = MaxDistance)
        {
            AudioSource source = null;
            foreach (var s in go.GetComponents<AudioSource>()) if (s.clip == clip) source = s;
            if (source == null) source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        private static void SetSound(Object target, string property, AudioClip clip, float volume, float maxDuration, float minInterval, int maxVoices,
            bool global = false)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            p.FindPropertyRelative("clip").objectReferenceValue = clip;
            p.FindPropertyRelative("volume").floatValue = volume;
            p.FindPropertyRelative("pitchJitter").floatValue = 0.06f;
            p.FindPropertyRelative("maxDuration").floatValue = maxDuration;
            p.FindPropertyRelative("minInterval").floatValue = minInterval;
            p.FindPropertyRelative("maxVoices").intValue = maxVoices;
            p.FindPropertyRelative("global").boolValue = global;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Object target, string property, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + name + ".wav");
            if (clip == null) Debug.LogWarning("[BossSfxBuilder] Missing clip " + name);
            return clip;
        }

        private static void EditPrefab(string name, System.Action<GameObject> edit)
        {
            string path = PrefabDir + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
