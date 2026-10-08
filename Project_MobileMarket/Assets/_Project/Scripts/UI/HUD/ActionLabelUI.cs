using UnityEngine;
using UnityEngine.UI;

namespace MobileMarket.UI
{
    /// <summary>
    /// World-space text shown above the owner's head (e.g. "심는 중..."), with an optional progress gauge under it.
    /// Always faces the camera.
    /// </summary>
    public class ActionLabelUI : MonoBehaviour
    {
        [SerializeField] private GameObject labelRoot;
        [SerializeField] private Text label;
        [Tooltip("Gauge shown under the text once SetProgress is called.")]
        [SerializeField] private GameObject gaugeRoot;
        [Tooltip("Fill bar; its right anchor follows the progress (0..1).")]
        [SerializeField] private RectTransform gaugeFill;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
            Hide();
        }

        private void LateUpdate()
        {
            if (mainCamera != null) transform.rotation = mainCamera.transform.rotation;
        }

        /// <summary>Shows the text only (call SetProgress afterwards to add the gauge).</summary>
        public void Show(string text)
        {
            label.text = text;
            labelRoot.SetActive(true);
            if (gaugeRoot != null) gaugeRoot.SetActive(false);
        }

        /// <summary>Shows the gauge filled to progress (0..1).</summary>
        public void SetProgress(float progress)
        {
            if (gaugeRoot == null || gaugeFill == null) return;
            if (!gaugeRoot.activeSelf) gaugeRoot.SetActive(true);
            gaugeFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        public void Hide()
        {
            labelRoot.SetActive(false);
            if (gaugeRoot != null) gaugeRoot.SetActive(false);
        }
    }
}
