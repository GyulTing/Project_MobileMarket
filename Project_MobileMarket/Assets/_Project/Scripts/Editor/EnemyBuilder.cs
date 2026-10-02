using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
            var zombie = BuildController("AC_Enemy_Zombie",
                Clip("Enemy_Zombie_Idle"), Clip("Enemy_Zombie_Walk"), Clip("Enemy_Zombie_Run"),
                Clip("Enemy_Zombie_Attack"), Clip("Enemy_Hit"), Clip("Enemy_Death"));
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
