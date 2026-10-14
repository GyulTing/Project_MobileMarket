using UnityEngine;

namespace MobileMarket.Data
{
    public enum RegionType { Home, Settlement, Combat }

    /// <summary>
    /// One place on the world map: what it's called, which scene it loads, where its marker sits on the map and which
    /// places it's drawn connected to. Locked regions show as "NOT AVAILABLE" and can't be travelled to.
    /// </summary>
    [CreateAssetMenu(menuName = "MobileMarket/Region", fileName = "Region_")]
    public class RegionData : ScriptableObject
    {
        [SerializeField] private string displayName = "REGION";
        [SerializeField] private RegionType type = RegionType.Settlement;
        [Tooltip("Scene loaded when travelling here (must be in Build Settings).")]
        [SerializeField] private string sceneName;
        [Tooltip("Marker position on the world map image, normalized (0,0 = bottom-left, 1,1 = top-right).")]
        [SerializeField] private Vector2 mapPosition = new Vector2(0.5f, 0.5f);
        [SerializeField] private bool locked;
        [Tooltip("Places drawn joined to this one by a dotted route.")]
        [SerializeField] private RegionData[] connections;

        public string DisplayName => displayName;
        public RegionType Type => type;
        public string SceneName => sceneName;
        public Vector2 MapPosition => mapPosition;
        public bool IsLocked => locked || string.IsNullOrEmpty(sceneName);
        public RegionData[] Connections => connections;
    }
}
