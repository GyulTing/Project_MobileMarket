using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Shared import steps for VARCO-generated characters: humanoid avatar + extracted textures/material,
    /// and humanoid clips baked from VARCO's animated GLBs (its FBX animation export imports with no takes in Unity).
    /// </summary>
    public static class VarcoCharacterTools
    {
        // VARCO models are ~1.1 units tall; the player model is scaled x2, so characters match it.
        private const float ImportScale = 2f;
        private const float SampleRate = 30f;
        private const string MatDir = "Assets/_Project/Materials/";

        /// <summary>
        /// Humanoid import at player scale, embedded textures extracted as T_{name}_BaseColor/Normal next to the model,
        /// and an M_{name} URP Lit material remapped onto VARCO's single "material_0".
        /// </summary>
        public static void SetupHumanoidModel(string modelPath, string name, (string human, string bone)[] boneOverrides = null)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null) { Debug.LogWarning("[VarcoCharacterTools] Missing " + modelPath); return; }

            importer.globalScale = ImportScale;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            string dir = Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/";
            ExtractTextures(importer, dir, name);

            string matPath = MatDir + "M_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(dir, name, "BaseColor")));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(dir, name, "Normal"));
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material_0"), mat);
            importer.SaveAndReimport();
            if (boneOverrides != null) ApplyBoneOverrides(importer, boneOverrides);

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
            if (avatar == null || !avatar.isHuman) Debug.LogWarning("[VarcoCharacterTools] Humanoid avatar failed for " + name);
        }

        /// <summary>Remaps humanoid bones when VARCO's auto-rig misnames joints; null bone clears the slot.</summary>
        private static void ApplyBoneOverrides(ModelImporter importer, (string human, string bone)[] overrides)
        {
            var desc = importer.humanDescription;
            var human = new List<HumanBone>(desc.human);
            foreach (var (humanName, boneName) in overrides)
            {
                human.RemoveAll(h => h.humanName == humanName);
                if (boneName != null) human.Add(new HumanBone { humanName = humanName, boneName = boneName, limit = new HumanLimit { useDefaultValues = true } });
            }
            desc.human = human.ToArray();
            importer.humanDescription = desc;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        /// <summary>Every VARCO export embeds the same texture file names, so extract one model at a time and rename right away.</summary>
        private static void ExtractTextures(ModelImporter importer, string dir, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(dir, name, "BaseColor")) != null) return;

            importer.ExtractTextures(dir);
            AssetDatabase.Refresh();
            AssetDatabase.RenameAsset(dir + "base_color.png", "T_" + name + "_BaseColor");
            AssetDatabase.RenameAsset(dir + "normal.png", "T_" + name + "_Normal");
            AssetDatabase.Refresh();

            if (AssetImporter.GetAtPath(TexturePath(dir, name, "Normal")) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }

        private static string TexturePath(string dir, string name, string suffix) => dir + "T_" + name + "_" + suffix + ".png";

        // ---------------------------------------------------------------- animation baking

        /// <summary>
        /// Reads the skeletal animation from a VARCO GLB, plays it on the rig it was retargeted to (sourceModelPath)
        /// and records it as a humanoid (muscle) clip that any humanoid avatar can play.
        /// from/to (seconds) keep only part of a long VARCO take, e.g. one swing out of a 30s attack sequence.
        /// </summary>
        public static AnimationClip BakeHumanoidClip(string glbPath, string sourceModelPath, string clipPath, bool loop,
            float from = 0f, float to = float.MaxValue)
        {
            var gltf = Glb.Load(glbPath);
            var anim = gltf.json.animations[0];

            var inst = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelPath));
            try
            {
                var animator = inst.GetComponent<Animator>();
                var bones = new Dictionary<string, Transform>();
                foreach (var t in inst.GetComponentsInChildren<Transform>()) bones[t.name] = t;

                // GLB units -> imported units (import scale), measured on a bone that exists in both.
                float unit = bones["Spine1"].localPosition.magnitude / Glb.Length(gltf.json.nodes[IndexOf(gltf.json.nodes, "Spine1")].translation);

                var tracks = new List<Track>();
                float start = float.MaxValue, end = 0f;
                foreach (var ch in anim.channels)
                {
                    var node = gltf.json.nodes[ch.target.node];
                    if (!bones.TryGetValue(node.name, out var bone)) continue;
                    var sampler = anim.samplers[ch.sampler];
                    var times = gltf.ReadFloats(sampler.input, out _);
                    var values = gltf.ReadFloats(sampler.output, out int comps);
                    start = Mathf.Min(start, times[0]);
                    end = Mathf.Max(end, times[times.Length - 1]);
                    tracks.Add(new Track { bone = bone, path = ch.target.path, isRoot = node.name == "Root", times = times, values = values, comps = comps, unit = unit });
                }

                start = Mathf.Max(start, from);
                end = Mathf.Min(end, to);

                var handler = new HumanPoseHandler(animator.avatar, inst.transform);
                var pose = new HumanPose();
                int muscleCount = HumanTrait.MuscleCount;
                var curves = new AnimationCurve[7 + muscleCount];
                for (int i = 0; i < curves.Length; i++) curves[i] = new AnimationCurve();

                int frames = Mathf.Max(2, Mathf.RoundToInt((end - start) * SampleRate) + 1);

                // VARCO's root height leaves the feet hovering; lower the root so the lowest foot touches the rest-pose ground level.
                var feet = new[] { bones["LeftFoot"], bones["RightFoot"], bones["LeftToeBase"], bones["RightToeBase"] };
                var rootBone = bones["Root"];
                float restGround = LowestY(feet), animGround = float.MaxValue;
                for (int f = 0; f < frames; f++)
                {
                    foreach (var track in tracks) track.Apply(start + (end - start) * f / (frames - 1));
                    animGround = Mathf.Min(animGround, LowestY(feet));
                }
                float drop = animGround - restGround;

                for (int f = 0; f < frames; f++)
                {
                    float t = start + (end - start) * f / (frames - 1);
                    foreach (var track in tracks) track.Apply(t);
                    rootBone.position -= Vector3.up * drop;
                    handler.GetHumanPose(ref pose);

                    float k = t - start;
                    curves[0].AddKey(k, pose.bodyPosition.x);
                    curves[1].AddKey(k, pose.bodyPosition.y);
                    curves[2].AddKey(k, pose.bodyPosition.z);
                    curves[3].AddKey(k, pose.bodyRotation.x);
                    curves[4].AddKey(k, pose.bodyRotation.y);
                    curves[5].AddKey(k, pose.bodyRotation.z);
                    curves[6].AddKey(k, pose.bodyRotation.w);
                    for (int m = 0; m < muscleCount; m++) curves[7 + m].AddKey(k, pose.muscles[m]);
                }
                handler.Dispose();

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clip.ClearCurves();
                clip.frameRate = SampleRate;

                string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                for (int i = 0; i < curves.Length; i++)
                {
                    string attr = i < 7 ? rootNames[i] : HumanTrait.MuscleName[i - 7];
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), attr), curves[i]);
                }

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                return clip;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        private static float LowestY(Transform[] bones)
        {
            float y = float.MaxValue;
            foreach (var b in bones) y = Mathf.Min(y, b.position.y);
            return y;
        }

        private static int IndexOf(Glb.Node[] nodes, string name)
        {
            for (int i = 0; i < nodes.Length; i++) if (nodes[i].name == name) return i;
            return -1;
        }

        /// <summary>One animated property of one bone, converted from glTF (right-handed, Y-up) to the imported FBX bone space.</summary>
        private class Track
        {
            public Transform bone;
            public string path;
            public bool isRoot;
            public float[] times, values;
            public int comps;
            public float unit;

            public void Apply(float t)
            {
                int i = 0;
                while (i < times.Length - 2 && times[i + 1] < t) i++;
                int j = Mathf.Min(i + 1, times.Length - 1);
                float span = times[j] - times[i];
                float a = span > 0f ? Mathf.Clamp01((t - times[i]) / span) : 0f;

                if (path == "rotation" && comps == 4)
                {
                    var q0 = new Quaternion(values[i * 4], values[i * 4 + 1], values[i * 4 + 2], values[i * 4 + 3]);
                    var q1 = new Quaternion(values[j * 4], values[j * 4 + 1], values[j * 4 + 2], values[j * 4 + 3]);
                    var q = Quaternion.Slerp(q0, q1, a);
                    // Below Root the FBX import keeps the Z-up bone space: (x, y, z) -> (-x, -z, y), a mirror, so axes flip sign.
                    if (!isRoot) bone.localRotation = new Quaternion(q.x, q.z, -q.y, q.w);
                }
                else if (path == "translation" && comps == 3 && isRoot)
                {
                    var p0 = new Vector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
                    var p1 = new Vector3(values[j * 3], values[j * 3 + 1], values[j * 3 + 2]);
                    var p = Vector3.Lerp(p0, p1, a) * unit;
                    bone.localPosition = new Vector3(-p.x, p.y, p.z);
                }
            }
        }

        // ---------------------------------------------------------------- minimal GLB reader (animations only)

        private class Glb
        {
            public Root json;
            private byte[] bin;

            [Serializable] public class Root { public Node[] nodes; public Anim[] animations; public Accessor[] accessors; public View[] bufferViews; }
            [Serializable] public class Node { public string name; public int[] children; public float[] translation; }
            [Serializable] public class Anim { public Channel[] channels; public Sampler[] samplers; }
            [Serializable] public class Channel { public int sampler; public Target target; }
            [Serializable] public class Target { public int node; public string path; }
            [Serializable] public class Sampler { public int input; public int output; }
            [Serializable] public class Accessor { public int bufferView; public int byteOffset; public int count; public string type; }
            [Serializable] public class View { public int byteOffset; public int byteLength; public int byteStride; }

            public static Glb Load(string path)
            {
                var data = File.ReadAllBytes(path);
                var glb = new Glb();
                int offset = 12;
                while (offset < data.Length)
                {
                    int len = BitConverter.ToInt32(data, offset);
                    uint type = BitConverter.ToUInt32(data, offset + 4);
                    if (type == 0x4E4F534A) glb.json = JsonUtility.FromJson<Root>(Encoding.UTF8.GetString(data, offset + 8, len));
                    else if (type == 0x004E4942) { glb.bin = new byte[len]; Buffer.BlockCopy(data, offset + 8, glb.bin, 0, len); }
                    offset += 8 + len;
                }
                return glb;
            }

            public float[] ReadFloats(int accessorIndex, out int comps)
            {
                var acc = json.accessors[accessorIndex];
                var view = json.bufferViews[acc.bufferView];
                comps = acc.type == "SCALAR" ? 1 : acc.type == "VEC2" ? 2 : acc.type == "VEC3" ? 3 : 4;
                int stride = view.byteStride > 0 ? view.byteStride : comps * 4;
                var result = new float[acc.count * comps];
                int baseOffset = view.byteOffset + acc.byteOffset;
                for (int i = 0; i < acc.count; i++)
                    for (int c = 0; c < comps; c++)
                        result[i * comps + c] = BitConverter.ToSingle(bin, baseOffset + i * stride + c * 4);
                return result;
            }

            public static float Length(float[] v) => v == null ? 0f : Mathf.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        }
    }
}
