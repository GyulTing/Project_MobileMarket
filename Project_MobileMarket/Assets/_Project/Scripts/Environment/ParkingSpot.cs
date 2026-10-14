using System.Collections.Generic;
using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Where the player's truck parks in this scene, placed with the spot's rotation (so the truck's front faces the spot's
    /// -Z, like the truck prefab; the gizmo line shows it). Settlements have one; combat
    /// zones have several and SceneLoader picks one at random. Spots register themselves so nothing has to search the scene.
    /// </summary>
    public class ParkingSpot : MonoBehaviour
    {
        private static readonly List<ParkingSpot> active = new List<ParkingSpot>();

        /// <summary>Spots in the loaded scene(s).</summary>
        public static IReadOnlyList<ParkingSpot> Active => active;

        [Tooltip("Where the player stands on arrival, relative to the spot (beside the truck). Mirrored to the other side of " +
                 "the truck when that side is nearer the camera, so the truck never hides the player.")]
        [SerializeField] private Vector3 playerOffset = new Vector3(-2.4f, 0f, 0f);

        /// <summary>The camera looks north (+Z) from the south, so the side with the lower Z is the visible one.</summary>
        public Vector3 PlayerPosition
        {
            get
            {
                var a = transform.TransformPoint(playerOffset);
                var b = transform.TransformPoint(new Vector3(-playerOffset.x, playerOffset.y, playerOffset.z));
                return b.z < a.z - 0.01f ? b : a;
            }
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, 1.3f, 0f), new Vector3(2.6f, 2.6f, 6.4f));
            Gizmos.DrawLine(Vector3.zero, Vector3.back * 4f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerOffset + Vector3.up, 0.4f);
        }
    }
}
