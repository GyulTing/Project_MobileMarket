using UnityEngine;

namespace MobileMarket.Player
{
    /// <summary>
    /// Feeds the player's position to the MobileMarket/WallSeeThrough shader, which dithers a hole in any wall standing
    /// between the camera and the player so the quarter-view camera never loses them behind a wall.
    /// </summary>
    public class WallSeeThrough : MonoBehaviour
    {
        private static readonly int TargetId = Shader.PropertyToID("_SeeThroughTarget");
        private static readonly int ParamsId = Shader.PropertyToID("_SeeThroughParams");

        [Tooltip("Point kept visible, above the player's feet (chest height).")]
        [SerializeField] private float targetHeight = 1.0f;
        [Tooltip("Radius (m) of the clear hole around the camera-to-player line.")]
        [SerializeField] private float radius = 1.3f;
        [Tooltip("Width (m) of the dithered edge outside the clear hole.")]
        [SerializeField] private float softEdge = 0.8f;

        private void OnEnable() => Shader.SetGlobalVector(ParamsId, new Vector4(radius, softEdge, 0f, 0f));

        private void OnDisable() => Shader.SetGlobalVector(TargetId, Vector4.zero);

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            Shader.SetGlobalVector(TargetId, new Vector4(p.x, p.y + targetHeight, p.z, 1f));
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Shader.SetGlobalVector(ParamsId, new Vector4(radius, softEdge, 0f, 0f));
        }
    }
}
