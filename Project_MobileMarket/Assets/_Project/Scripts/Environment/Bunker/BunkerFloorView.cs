using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The bunker's floors are stacked in one scene, so the top-down camera would see the floor above instead of the one the
    /// player is on. Every floor above the player's switches to shadows-only (still darkens the scene, never drawn).
    /// </summary>
    public class BunkerFloorView : MonoBehaviour
    {
        [Serializable]
        private class Floor
        {
            public Transform root;
            [Tooltip("World height of this floor's walking surface.")]
            public float height;
        }

        [SerializeField] private Transform player;
        [Tooltip("Top floor first.")]
        [SerializeField] private Floor[] floors = Array.Empty<Floor>();
        [Tooltip("The player counts as being on a floor once below its height by this much (half a stairwell).")]
        [SerializeField] private float switchMargin = 3f;

        private Renderer[][] renderers;
        private ShadowCastingMode[][] originalModes;
        private int current = -1;

        private void Awake()
        {
            renderers = new Renderer[floors.Length][];
            originalModes = new ShadowCastingMode[floors.Length][];
            for (int i = 0; i < floors.Length; i++)
            {
                renderers[i] = floors[i].root.GetComponentsInChildren<Renderer>(true);
                originalModes[i] = new ShadowCastingMode[renderers[i].Length];
                for (int r = 0; r < renderers[i].Length; r++) originalModes[i][r] = renderers[i][r].shadowCastingMode;
            }
        }

        private void LateUpdate()
        {
            if (player == null || floors.Length == 0) return;
            int index = FloorIndexAt(player.position.y);
            if (index == current) return;
            current = index;
            for (int i = 0; i < floors.Length; i++)
            {
                bool hidden = i < current;
                for (int r = 0; r < renderers[i].Length; r++)
                {
                    if (renderers[i][r] == null) continue;
                    renderers[i][r].shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : originalModes[i][r];
                }
            }
        }

        private int FloorIndexAt(float y)
        {
            int index = 0;
            for (int i = 1; i < floors.Length; i++)
                if (y < floors[i - 1].height - switchMargin) index = i;
            return index;
        }
    }
}
