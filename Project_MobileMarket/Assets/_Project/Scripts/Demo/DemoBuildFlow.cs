using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MobileMarket.Enemy;
using MobileMarket.Player;

namespace MobileMarket.Demo
{
    /// <summary>
    /// TEMPORARY (submission demo build only): game-over screen when the player dies in the boss fight (retry / main menu),
    /// and an automatic return to the main menu some time after the boss falls. Only listens to existing events, so deleting
    /// the DEMO_BuildFlow object and this folder removes it cleanly.
    /// </summary>
    public class DemoBuildFlow : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private BossController boss;
        [SerializeField] private string retryScene = "BunkerBoss";
        [SerializeField] private string menuScene = "Tmp_Menu";

        [Header("Game over")]
        [SerializeField] private CanvasGroup gameOverGroup;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private float gameOverDelay = 2f;
        [SerializeField] private float fadeTime = 1f;

        [Header("Boss defeated")]
        [Tooltip("Seconds after the boss falls before quietly returning to the main menu.")]
        [SerializeField] private float returnDelay = 20f;

        private bool finished;

        private void Awake()
        {
            gameOverGroup.alpha = 0f;
            gameOverGroup.gameObject.SetActive(false);
            retryButton.onClick.AddListener(() => Load(retryScene));
            menuButton.onClick.AddListener(() => Load(menuScene));
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
            if (boss != null) boss.OnDefeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
            if (boss != null) boss.OnDefeated -= HandleBossDefeated;
        }

        private void HandlePlayerDied()
        {
            if (finished) return;
            finished = true;
            StartCoroutine(ShowGameOver());
        }

        private void HandleBossDefeated()
        {
            if (finished) return;
            finished = true;
            StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ShowGameOver()
        {
            yield return new WaitForSecondsRealtime(gameOverDelay);
            gameOverGroup.gameObject.SetActive(true);
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                gameOverGroup.alpha = t / fadeTime;
                yield return null;
            }
            gameOverGroup.alpha = 1f;
        }

        private IEnumerator ReturnToMenu()
        {
            yield return new WaitForSecondsRealtime(returnDelay);
            Load(menuScene);
        }

        private void Load(string scene)
        {
            retryButton.interactable = menuButton.interactable = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
