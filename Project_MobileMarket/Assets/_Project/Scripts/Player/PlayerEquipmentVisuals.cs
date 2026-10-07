using System.Collections.Generic;
using UnityEngine;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Player
{
    /// <summary>
    /// Shows the gas mask, body armor and backpack from the Inventory's equipment slots on the character.
    /// Each worn model hangs on a bone (Head / upper chest) so it follows the animation. Only rebuilt when a slot changes.
    /// </summary>
    public class PlayerEquipmentVisuals : MonoBehaviour
    {
        private static readonly EquipSlot[] WornSlots = { EquipSlot.Head, EquipSlot.Body, EquipSlot.Backpack };

        private Inventory inventory;
        private Animator animator;
        private readonly Dictionary<EquipSlot, EquipmentData> shown = new Dictionary<EquipSlot, EquipmentData>();
        private readonly Dictionary<EquipSlot, GameObject> instances = new Dictionary<EquipSlot, GameObject>();

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnInventoryChanged += Refresh;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnInventoryChanged -= Refresh;
        }

        private void Refresh()
        {
            foreach (var slot in WornSlots)
            {
                var data = inventory.GetSlot(slot)?.Data as EquipmentData;
                shown.TryGetValue(slot, out var current);
                if (data == current) continue;

                if (instances.TryGetValue(slot, out var old) && old != null) Destroy(old);
                instances.Remove(slot);
                shown[slot] = data;

                if (data != null && data.wornModel != null) instances[slot] = Wear(data, BoneFor(slot));
            }
        }

        /// <summary>The bone a slot's model hangs on.</summary>
        public static HumanBodyBones BoneFor(EquipSlot slot) => slot == EquipSlot.Head ? HumanBodyBones.Head : HumanBodyBones.UpperChest;

        private GameObject Wear(EquipmentData data, HumanBodyBones boneId)
        {
            var bone = animator.GetBoneTransform(boneId);
            if (bone == null && boneId == HumanBodyBones.UpperChest) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (bone == null) return null;

            // Holder: placed relative to the bone, with world scale 1 so wornSize is in meters.
            var holder = new GameObject("Worn_" + data.slot).transform;
            holder.SetParent(bone, false);
            holder.localPosition = data.wornPosition;
            holder.localRotation = Quaternion.Euler(data.wornRotation);
            var boneScale = bone.lossyScale;
            holder.localScale = new Vector3(1f / boneScale.x, 1f / boneScale.y, 1f / boneScale.z);

            var model = Instantiate(data.wornModel, holder, false);
            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);
            FitToSize(model.transform, holder, data.wornSize);
            return holder.gameObject;
        }

        /// <summary>
        /// Scales the model so its largest side is size meters and centers it on the holder.
        /// Measured in the holder's own space (world scale 1), so the result doesn't depend on which way the player faces.
        /// </summary>
        public static void FitToSize(Transform model, Transform holder, float size)
        {
            var bounds = MeshBoundsIn(model, holder);
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest <= 0f) return;

            model.localScale *= size / largest;
            bounds = MeshBoundsIn(model, holder);
            model.localPosition -= bounds.center;
        }

        private static Bounds MeshBoundsIn(Transform model, Transform space)
        {
            var result = new Bounds();
            bool any = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toSpace.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }
    }
}
