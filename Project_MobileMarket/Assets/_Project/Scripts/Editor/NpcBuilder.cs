using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MobileMarket.EditorTools
{
    /// <summary>Turns the VARCO NPC exports into game-ready prefabs: humanoid models, shared humanoid clips, controllers and prefabs.</summary>
    public static class NpcBuilder
    {
        public enum Role { Merchant, Guard, Farmer, Resident }

        private const string ModelDir = "Assets/_Project/Models/NPC/";
        private const string AnimDir = "Assets/_Project/Animations/NPC/";
        private const string PrefabDir = "Assets/_Project/Prefabs/NPC/";

        // The GLB animations were retargeted in VARCO onto the first Merchant_Food model's rig (its auto-rig is clean).
        // That model was kept as NPC_SourceRig.fbx when the NPCs were remade, so the GLBs still bake against the right skeleton.
        public const string SourceRig = "SourceRig";

        public static readonly (string name, Role role)[] Npcs =
        {
            ("Merchant_Scrap", Role.Merchant), ("Merchant_Food", Role.Merchant), ("Merchant_Arms", Role.Merchant),
            ("Merchant_Cloth", Role.Merchant), ("Merchant_Water", Role.Merchant),
            ("Guard_01", Role.Guard), ("Guard_02", Role.Guard), ("Guard_03", Role.Guard),
            ("Farmer_01", Role.Farmer), ("Farmer_02", Role.Farmer),
            ("Resident_01", Role.Resident), ("Resident_02", Role.Resident), ("Resident_03", Role.Resident),
            ("Resident_04", Role.Resident), ("Resident_05", Role.Resident),
            ("TwinsMechanic1", Role.Resident), ("TwinsMechanic2", Role.Resident),
        };

        // Avatar fixes for VARCO auto-rigs that place a bone name on the wrong joint (checked by bone world positions):
        // human bone -> rig bone, or null to leave it unmapped.
        // This rig got an extra leg joint: *UpLeg sits at the hip center, *Leg on the hip, *Foot on the knee, *ToeBase on the ankle.
        private static readonly (string, string)[] ShiftedLegs =
        {
            ("LeftUpperLeg", "LeftLeg"), ("LeftLowerLeg", "LeftFoot"), ("LeftFoot", "LeftToeBase"), ("LeftToes", null),
            ("RightUpperLeg", "RightLeg"), ("RightLowerLeg", "RightFoot"), ("RightFoot", "RightToeBase"), ("RightToes", null),
        };

        private static readonly Dictionary<string, (string human, string bone)[]> BoneOverrides = new Dictionary<string, (string, string)[]>
        {
            ["Merchant_Cloth"] = ShiftedLegs,
        };

        public static string ModelPath(string npc) => ModelDir + "NPC_" + npc + ".fbx";
        public static string PrefabPath(string npc) => PrefabDir + "P_NPC_" + npc + ".prefab";

        [MenuItem("MobileMarket/NPC/1. Setup Models")]
        public static void SetupModels()
        {
            foreach (var (npc, _) in Npcs)
            {
                BoneOverrides.TryGetValue(npc, out var overrides);
                VarcoCharacterTools.SetupHumanoidModel(ModelPath(npc), "NPC_" + npc, overrides);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Bakes a VARCO GLB animation (made on SourceRig) into Animations/NPC/{clipName}.anim.</summary>
        public static AnimationClip BakeHumanoidClip(string glbPath, string clipName, bool loop) =>
            VarcoCharacterTools.BakeHumanoidClip(glbPath, ModelPath(SourceRig), AnimDir + clipName + ".anim", loop);

        // ---------------------------------------------------------------- controllers & prefabs

        [MenuItem("MobileMarket/NPC/2. Build Controllers And Prefabs")]
        public static void BuildControllersAndPrefabs()
        {
            var idle = LoadClip("NPC_Idle");
            var walk = LoadClip("NPC_Walk");
            var baseCtrl = BuildBaseController(idle, walk, LoadClip("NPC_Talk"));
            var guardCtrl = BuildOverride("AOC_NPC_Guard", baseCtrl, LoadClip("NPC_GuardIdle"));
            var farmerCtrl = BuildOverride("AOC_NPC_Farmer", baseCtrl, LoadClip("NPC_PickUp"));

            // Write the freshly created controller to disk and reload it, otherwise the first prefabs can end up with a null reference.
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AnimDir + "AC_NPC.controller", ImportAssetOptions.ForceSynchronousImport);
            baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "AC_NPC.controller");
            guardCtrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(AnimDir + "AOC_NPC_Guard.overrideController");
            farmerCtrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(AnimDir + "AOC_NPC_Farmer.overrideController");

            foreach (var (npc, role) in Npcs)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(npc));
                if (model == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "P_NPC_" + npc;
                var animator = inst.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.runtimeAnimatorController = role == Role.Guard ? guardCtrl : role == Role.Farmer ? farmerCtrl : (RuntimeAnimatorController)baseCtrl;

                var col = inst.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.35f;
                col.center = new Vector3(0f, 1f, 0f);

                PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath(npc));
                Object.DestroyImmediate(inst);
            }
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip LoadClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + name + ".anim");

        /// <summary>Idle loops, now and then plays the role action, and walks while Speed > 0.1 (for a future movement script).</summary>
        private static AnimatorController BuildBaseController(AnimationClip idle, AnimationClip walk, AnimationClip action)
        {
            string path = AnimDir + "AC_NPC.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sAction = sm.AddState("Action"); sAction.motion = action;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            sm.defaultState = sIdle;

            var toAction = sIdle.AddTransition(sAction);
            toAction.hasExitTime = true; toAction.exitTime = 1f; toAction.duration = 0.3f;
            var back = sAction.AddTransition(sIdle);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.3f;

            foreach (var from in new[] { sIdle, sAction })
            {
                var t = from.AddTransition(sWalk);
                t.hasExitTime = false; t.duration = 0.2f;
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }
            var stop = sWalk.AddTransition(sIdle);
            stop.hasExitTime = false; stop.duration = 0.2f;
            stop.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static AnimatorOverrideController BuildOverride(string name, AnimatorController baseCtrl, AnimationClip action)
        {
            string path = AnimDir + name + ".overrideController";
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(baseCtrl);
                AssetDatabase.CreateAsset(aoc, path);
            }
            aoc.runtimeAnimatorController = baseCtrl;
            aoc[LoadClip("NPC_Talk")] = action;
            EditorUtility.SetDirty(aoc);
            return aoc;
        }
    }
}
