using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Applies this map's fog while enabled, so an atmosphere prefab dropped into any scene brings its own haze
    /// (the scene's own fog settings come back when it is disabled).
    /// </summary>
    public class AtmosphereFog : MonoBehaviour
    {
        [ColorUsage(false, false)] [SerializeField] private Color color = new Color(0.78f, 0.68f, 0.52f);
        [Tooltip("Distance from the camera where the haze starts.")]
        [SerializeField] private float start = 25f;
        [Tooltip("Distance where it is fully opaque.")]
        [SerializeField] private float end = 170f;

        private bool savedFog;
        private FogMode savedMode;
        private Color savedColor;
        private float savedStart, savedEnd;

        private void OnEnable()
        {
            savedFog = RenderSettings.fog;
            savedMode = RenderSettings.fogMode;
            savedColor = RenderSettings.fogColor;
            savedStart = RenderSettings.fogStartDistance;
            savedEnd = RenderSettings.fogEndDistance;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = color;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
        }

        private void OnDisable()
        {
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedMode;
            RenderSettings.fogColor = savedColor;
            RenderSettings.fogStartDistance = savedStart;
            RenderSettings.fogEndDistance = savedEnd;
        }
    }
}
