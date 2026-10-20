using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobileMarket.Demo
{
    /// <summary>
    /// TEMPORARY (submission demo build only) main menu: Start shows the controls screen for a few seconds while the playable
    /// scene (the boss fight build) loads in the background, then enters it. Exit quits the game.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;
        [Tooltip("Scene loaded by Start (must be in the build scene list).")]
        [SerializeField] private string startScene = "BunkerBoss";

        [Header("Controls screen")]
        [SerializeField] private GameObject controlsPanel;
        [Tooltip("Filled (horizontal) bar showing the time left on the controls screen.")]
        [SerializeField] private Image controlsProgress;
        [SerializeField] private float controlsTime = 5f;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (controlsPanel != null) controlsPanel.SetActive(false);
            startButton.onClick.AddListener(StartGame);
            exitButton.onClick.AddListener(ExitGame);
        }

        private void StartGame()
        {
            startButton.interactable = exitButton.interactable = false;
            StartCoroutine(ShowControlsThenStart());
        }

        private IEnumerator ShowControlsThenStart()
        {
            var load = SceneManager.LoadSceneAsync(startScene);
            load.allowSceneActivation = false;
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(true);
                for (float t = 0f; t < controlsTime; t += Time.unscaledDeltaTime)
                {
                    if (controlsProgress != null) controlsProgress.fillAmount = t / controlsTime;
                    yield return null;
                }
            }
            load.allowSceneActivation = true;
        }

        private void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
