using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MobileMarket.Data;
using MobileMarket.Environment;
using MobileMarket.Player;

namespace MobileMarket.Managers
{
    /// <summary>
    /// Moves the player between regions: fades to a loading screen, loads the region's scene, parks the truck on one of the
    /// scene's ParkingSpots (a random one in combat zones) and puts the player beside it. Survives scene loads; every
    /// playable scene holds a copy and the extra ones remove themselves.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        /// <summary>Raised when a trip starts (true) and when the new scene is ready (false).</summary>
        public static event Action<bool> OnTravelStateChanged;

        [SerializeField] private RegionData[] regions;
        [SerializeField] private RegionData homeRegion;
        [Tooltip("Spawned on the chosen ParkingSpot of every scene.")]
        [SerializeField] private GameObject truckPrefab;

        [Header("Loading screen")]
        [SerializeField] private CanvasGroup loadingScreen;
        [SerializeField] private Text loadingText;
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private string loadingFormat = "{0} (으)로 이동 중...";

        private static PlayerArrival player;

        public IReadOnlyList<RegionData> Regions => regions;
        public RegionData HomeRegion => homeRegion;
        public RegionData CurrentRegion { get; private set; }
        public bool IsTravelling { get; private set; }

        private bool movePlayerOnArrival;
        private int lastPreparedScene = -1;

        public static void RegisterPlayer(PlayerArrival arrival) => player = arrival;

        public static void UnregisterPlayer(PlayerArrival arrival)
        {
            if (player == arrival) player = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetLoadingVisible(0f);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }

        // The first scene may already be loaded before this subscribes, so prepare it here too (once per scene).
        private void Start() => PrepareScene(SceneManager.GetActiveScene());

        public void TravelTo(RegionData region)
        {
            if (IsTravelling || region == null || region.IsLocked) return;
            StartCoroutine(Travel(region));
        }

        /// <summary>Leaving a combat zone always brings the player home.</summary>
        public void ReturnHome() => TravelTo(homeRegion);

        private IEnumerator Travel(RegionData region)
        {
            IsTravelling = true;
            OnTravelStateChanged?.Invoke(true);
            if (loadingText != null) loadingText.text = string.Format(loadingFormat, region.DisplayName);
            yield return Fade(0f, 1f);

            CurrentRegion = region;
            movePlayerOnArrival = true;
            var load = SceneManager.LoadSceneAsync(region.SceneName);
            while (!load.isDone) yield return null;

            yield return Fade(1f, 0f);
            IsTravelling = false;
            OnTravelStateChanged?.Invoke(false);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => PrepareScene(scene);

        private void PrepareScene(Scene scene)
        {
            if (scene.handle == lastPreparedScene) return;
            lastPreparedScene = scene.handle;

            // Started straight from the editor: work out where we are from the scene name.
            if (CurrentRegion == null || CurrentRegion.SceneName != scene.name) CurrentRegion = FindRegion(scene.name);

            var spot = PickSpot();
            if (spot == null) return;
            if (truckPrefab != null) Instantiate(truckPrefab, spot.transform.position, spot.transform.rotation);

            if (movePlayerOnArrival && player != null)
            {
                var toTruck = spot.transform.position - spot.PlayerPosition;
                toTruck.y = 0f;
                player.PlaceAt(spot.PlayerPosition, toTruck.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toTruck) : spot.transform.rotation);
            }
            movePlayerOnArrival = false;
        }

        private ParkingSpot PickSpot()
        {
            var spots = ParkingSpot.Active;
            if (spots.Count == 0) return null;
            bool random = CurrentRegion != null && CurrentRegion.Type == RegionType.Combat;
            return spots[random ? UnityEngine.Random.Range(0, spots.Count) : 0];
        }

        private RegionData FindRegion(string sceneName)
        {
            foreach (var region in regions)
                if (region != null && region.SceneName == sceneName) return region;
            return null;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                SetLoadingVisible(Mathf.Lerp(from, to, t / fadeDuration));
                yield return null;
            }
            SetLoadingVisible(to);
        }

        private void SetLoadingVisible(float alpha)
        {
            if (loadingScreen == null) return;
            loadingScreen.alpha = alpha;
            loadingScreen.blocksRaycasts = alpha > 0f;
            loadingScreen.gameObject.SetActive(alpha > 0f);
        }
    }
}
