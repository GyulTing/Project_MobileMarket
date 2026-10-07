using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using MobileMarket.Enemy;
using MobileMarket.Items;

namespace MobileMarket.EditorTools
{
    /// <summary>Turns the VARCO enemy exports into prefabs: humanoid models, shared humanoid clips, one controller per enemy family.</summary>
    public static class EnemyBuilder
    {
        public enum Family { Zombie, Bandit, Paramilitary }

        private const string ModelDir = "Assets/_Project/Models/Enemy/";
        private const string AnimDir = "Assets/_Project/Animations/Enemy/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Enemy/";

        public static readonly (string name, Family family)[] Enemies =
        {
            ("Zombie_Gaunt", Family.Zombie), ("Zombie_Bloated", Family.Zombie), ("Zombie_Ghoul", Family.Zombie),
            ("Bandit_Hood", Family.Bandit), ("Bandit_Raider", Family.Bandit),
            ("Paramilitary_Soldier", Family.Paramilitary), ("Paramilitary_Leader", Family.Paramilitary),
        };

        public static string ModelPath(string enemy) => ModelDir + "Enemy_" + enemy + ".fbx";
        public static string PrefabPath(string enemy) => PrefabDir + "P_Enemy_" + enemy + ".prefab";

        [MenuItem("MobileMarket/Enemy/1. Setup Models")]
        public static void SetupModels()
        {
            foreach (var (enemy, _) in Enemies) VarcoCharacterTools.SetupHumanoidModel(ModelPath(enemy), "Enemy_" + enemy);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Bakes a VARCO GLB animation (made on the NPC source rig) into Animations/Enemy/{clipName}.anim.</summary>
        public static AnimationClip BakeHumanoidClip(string glbPath, string clipName, bool loop, float from = 0f, float to = float.MaxValue) =>
            VarcoCharacterTools.BakeHumanoidClip(glbPath, NpcBuilder.ModelPath(NpcBuilder.SourceRig), AnimDir + clipName + ".anim", loop, from, to);

        [MenuItem("MobileMarket/Enemy/2. Build Controllers And Prefabs")]
        public static void BuildControllersAndPrefabs()
        {
            // Hit (light flinch) and Death (fall forward) are shared by every enemy.
            // Zombies get their random attack/hit states from "4. Setup Zombie Attacks" and "5. Setup Zombie Hits"
            // (run 3, 4 and 5 again after this rebuild).
            var zombie = BuildController("AC_Enemy_Zombie",
                Clip("Enemy_Zombie_Idle"), Clip("Enemy_Zombie_Walk"), Clip("Enemy_Zombie_Run"),
                Clip("Enemy_Zombie_Attack_Pound"), Clip("Enemy_Hit"), Clip("Enemy_Death"));
            var human = BuildController("AC_Enemy_Human",
                Clip("Enemy_Human_Idle"), AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/NPC/NPC_Walk.anim"), Clip("Enemy_Human_Run"),
                Clip("Enemy_Human_Aim"), Clip("Enemy_Hit"), Clip("Enemy_Death"));

            // Write the freshly created controllers to disk and reload them, otherwise the first prefab can end up with a null reference.
            AssetDatabase.SaveAssets();
            zombie = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "AC_Enemy_Zombie.controller");
            human = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "AC_Enemy_Human.controller");

            foreach (var (enemy, family) in Enemies)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(enemy));
                if (model == null) { Debug.LogWarning("[EnemyBuilder] Missing " + ModelPath(enemy)); continue; }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "P_Enemy_" + enemy;
                var animator = inst.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.runtimeAnimatorController = family == Family.Zombie ? zombie : human;

                // Capsule from the T-pose mesh: full height, body depth for the radius (the bloated zombie gets a fatter one).
                var bounds = inst.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
                var col = inst.AddComponent<CapsuleCollider>();
                col.height = bounds.size.y;
                col.radius = Mathf.Clamp(bounds.size.z * 0.5f, 0.3f, 0.6f);
                col.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);

                PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath(enemy));
                Object.DestroyImmediate(inst);

                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(enemy));
                if (saved.GetComponent<Animator>().runtimeAnimatorController == null) Debug.LogError("[EnemyBuilder] Controller missing on " + enemy);
            }
            AssetDatabase.SaveAssets();
        }

        private struct AiStats
        {
            public Faction faction;
            public float speed, sight, damage;
            public float range;      // melee reach or gun range
            public float fireRate;   // guns only
            public string weapon;    // WeaponData asset name, null = melee
            public AiStats(Faction faction, float speed, float sight, float damage, float range, float fireRate = 0f, string weapon = null)
            {
                this.faction = faction; this.speed = speed; this.sight = sight; this.damage = damage;
                this.range = range; this.fireRate = fireRate; this.weapon = weapon;
            }
        }

        private static readonly (string name, AiStats stats)[] Ai =
        {
            ("Zombie_Gaunt", new AiStats(Faction.Zombie, 2.6f, 12f, 12f, 1.6f)),
            ("Zombie_Ghoul", new AiStats(Faction.Zombie, 3.0f, 12f, 15f, 1.6f)),
            ("Zombie_Bloated", new AiStats(Faction.Zombie, 1.6f, 12f, 25f, 1.8f)),
            ("Bandit_Hood", new AiStats(Faction.Bandit, 3.2f, 16f, 10f, 12f, 2f, "MP-40")),
            ("Bandit_Raider", new AiStats(Faction.Bandit, 3.2f, 16f, 12f, 12f, 1.5f, "STG-44")),
            ("Paramilitary_Soldier", new AiStats(Faction.Paramilitary, 3.4f, 18f, 14f, 14f, 2f, "AK-47")),
            ("Paramilitary_Leader", new AiStats(Faction.Paramilitary, 3.4f, 18f, 16f, 14f, 2f, "K-2")),
        };

        /// <summary>Adds faction, NavMesh movement, the brain and the right attack (zombie melee / human gun) to every enemy prefab.</summary>
        [MenuItem("MobileMarket/Enemy/3. Setup AI")]
        public static void SetupAi()
        {
            // Enemies fire their own bullet variant (slower than the player's P_Bullet, same look).
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/P_Bullet_Enemy.prefab").GetComponent<Bullet>();
            foreach (var (enemy, s) in Ai)
            {
                string path = PrefabPath(enemy);
                var root = PrefabUtility.LoadPrefabContents(path);

                var capsule = root.GetComponent<CapsuleCollider>();
                var agent = GetOrAdd<NavMeshAgent>(root);
                agent.radius = Mathf.Min(capsule.radius, 0.45f);
                agent.height = capsule.height;
                agent.speed = s.speed;
                agent.angularSpeed = 540f;
                agent.acceleration = 16f;
                agent.stoppingDistance = 0f;
                agent.autoBraking = true;

                Set(GetOrAdd<FactionMember>(root), "faction", p => p.enumValueIndex = (int)s.faction);
                Set(GetOrAdd<FactionMember>(root), "aimOffset", p => p.vector3Value = new Vector3(0f, capsule.height * 0.6f, 0f));
                Set(GetOrAdd<EnemyMovement>(root), "moveSpeed", p => p.floatValue = s.speed);
                // Zombies shuffle at 30% speed while a hit reaction plays.
                Set(GetOrAdd<EnemyMovement>(root), "hitSlowMultiplier", p => p.floatValue = s.faction == Faction.Zombie ? 0.3f : 1f);
                Set(GetOrAdd<EnemyBrain>(root), "sightRange", p => p.floatValue = s.sight);

                // Exactly one attack component per enemy.
                foreach (var old in root.GetComponents<EnemyAttack>()) Object.DestroyImmediate(old);
                if (s.weapon == null)
                {
                    var melee = root.AddComponent<EnemyMeleeAttack>();
                    Set(melee, "damage", p => p.floatValue = s.damage);
                    Set(melee, "range", p => p.floatValue = s.range);
                    if (s.faction == Faction.Zombie) ApplyZombieSwings(melee);
                }
                else
                {
                    var gun = root.AddComponent<EnemyGunAttack>();
                    var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/Resources/Items/" + s.weapon + ".asset");
                    Set(gun, "weapon", p => p.objectReferenceValue = weapon);
                    Set(gun, "bulletPrefab", p => p.objectReferenceValue = bullet);
                    Set(gun, "damage", p => p.floatValue = s.damage);
                    Set(gun, "range", p => p.floatValue = s.range);
                    Set(gun, "fireRate", p => p.floatValue = s.fireRate);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Zombie attack animations picked at random per swing: Animator state, clip, and when the blow lands (seconds).</summary>
        private static readonly (string state, string clip, float hitTime)[] ZombieSwings =
        {
            ("Attack_Pound", "Enemy_Zombie_Attack_Pound", 1.0f),
            ("Attack_Punch", "Enemy_Zombie_Attack_Punch", 0.4f),
            ("Attack_Headbutt", "Enemy_Zombie_Attack_Headbutt", 0.4f),
        };

        /// <summary>
        /// Replaces the zombie controller's single Attack state with one state per ZombieSwings entry (each plays once and
        /// returns to Locomotion; EnemyMeleeAttack cross-fades into them) and fills the swing list on the zombie prefabs.
        /// Edits the controller in place so prefab references stay intact.
        /// </summary>
        [MenuItem("MobileMarket/Enemy/4. Setup Zombie Attacks")]
        public static void SetupZombieAttacks()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "AC_Enemy_Zombie.controller");
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState locomotion = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == "Locomotion") locomotion = child.state;
                else if (child.state.name == "Attack" || child.state.name.StartsWith("Attack_")) sm.RemoveState(child.state);
            }

            for (int i = 0; i < ZombieSwings.Length; i++)
            {
                var state = sm.AddState(ZombieSwings[i].state, new Vector3(500f, 120f * i, 0f));
                state.motion = Clip(ZombieSwings[i].clip);
                var back = state.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.15f;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            foreach (var (enemy, family) in Enemies)
            {
                if (family != Family.Zombie) continue;
                var root = PrefabUtility.LoadPrefabContents(PrefabPath(enemy));
                var melee = root.GetComponent<EnemyMeleeAttack>();
                if (melee != null) ApplyZombieSwings(melee);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(enemy));
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Zombie hit reactions picked at random per flinch: Animator state and clip.
        /// (Enemy_Zombie_Hit_Heavy is baked too but kept out for now — meant for strong hits later.)</summary>
        private static readonly (string state, string clip)[] ZombieHits =
        {
            ("Hit_Shake", "Enemy_Zombie_Hit_Shake"),
            ("Hit_Flail", "Enemy_Zombie_Hit_Flail"),
            ("Hit_Stagger", "Enemy_Zombie_Hit_Stagger"),
        };

        /// <summary>
        /// Replaces the zombie controller's shared Hit state with one state per ZombieHits entry (each plays once and returns
        /// to Locomotion; EnemyHealth cross-fades into one at random) and fills hitReactions (state + clip length) on the zombie prefabs.
        /// </summary>
        [MenuItem("MobileMarket/Enemy/5. Setup Zombie Hits")]
        public static void SetupZombieHits()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "AC_Enemy_Zombie.controller");
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState locomotion = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == "Locomotion") locomotion = child.state;
                else if (child.state.name == "Hit" || child.state.name.StartsWith("Hit_")) sm.RemoveState(child.state);
            }

            for (int i = 0; i < ZombieHits.Length; i++)
            {
                var state = sm.AddState(ZombieHits[i].state, new Vector3(800f, 120f * i, 0f));
                state.motion = Clip(ZombieHits[i].clip);
                var back = state.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.25f; // some reactions end twisted, so blend back a little slower
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            foreach (var (enemy, family) in Enemies)
            {
                if (family != Family.Zombie) continue;
                var root = PrefabUtility.LoadPrefabContents(PrefabPath(enemy));
                Set(root.GetComponent<EnemyHealth>(), "hitReactions", p =>
                {
                    p.arraySize = ZombieHits.Length;
                    for (int i = 0; i < ZombieHits.Length; i++)
                    {
                        var e = p.GetArrayElementAtIndex(i);
                        e.FindPropertyRelative("state").stringValue = ZombieHits[i].state;
                        e.FindPropertyRelative("duration").floatValue = Clip(ZombieHits[i].clip).length;
                    }
                });
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(enemy));
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ApplyZombieSwings(EnemyMeleeAttack melee)
        {
            Set(melee, "swings", p =>
            {
                p.arraySize = ZombieSwings.Length;
                for (int i = 0; i < ZombieSwings.Length; i++)
                {
                    var e = p.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("state").stringValue = ZombieSwings[i].state;
                    e.FindPropertyRelative("duration").floatValue = Clip(ZombieSwings[i].clip).length;
                    e.FindPropertyRelative("hitTime").floatValue = ZombieSwings[i].hitTime;
                }
            });
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void Set(Object target, string property, System.Action<SerializedProperty> apply)
        {
            var so = new SerializedObject(target);
            apply(so.FindProperty(property));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + name + ".anim");

        /// <summary>
        /// Locomotion blend on Speed (idle 0 / walk 1.5 / run 4.5), Attack while Attacking (zombie swipe or rifle aim),
        /// Hit on the Hit trigger, Death once Dead is set. Driven later by the enemy AI.
        /// </summary>
        private static AnimatorController BuildController(string name, AnimationClip idle, AnimationClip walk, AnimationClip run,
            AnimationClip attack, AnimationClip hit, AnimationClip death)
        {
            string path = AnimDir + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attacking", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.5f);
            tree.AddChild(run, 4.5f);
            sm.defaultState = locomotion;

            var sAttack = sm.AddState("Attack"); sAttack.motion = attack;
            var sHit = sm.AddState("Hit"); sHit.motion = hit;
            var sDeath = sm.AddState("Death"); sDeath.motion = death;

            var toAttack = locomotion.AddTransition(sAttack);
            toAttack.hasExitTime = false; toAttack.duration = 0.15f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attacking");
            var fromAttack = sAttack.AddTransition(locomotion);
            fromAttack.hasExitTime = false; fromAttack.duration = 0.2f;
            fromAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Attacking");

            var toHit = sm.AddAnyStateTransition(sHit);
            toHit.hasExitTime = false; toHit.duration = 0.05f; toHit.canTransitionToSelf = false;
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            var fromHit = sHit.AddTransition(locomotion);
            fromHit.hasExitTime = true; fromHit.exitTime = 0.9f; fromHit.duration = 0.15f;

            var toDeath = sm.AddAnyStateTransition(sDeath);
            toDeath.hasExitTime = false; toDeath.duration = 0.1f; toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
