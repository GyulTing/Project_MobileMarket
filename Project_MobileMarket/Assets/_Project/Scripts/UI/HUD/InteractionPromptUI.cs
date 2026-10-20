using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Player;

namespace MobileMarket.UI
{
    /// <summary>
    /// "[F] 비상 발전기" label floating over whatever the player would use with F (PlayerInteractor's focus).
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text label;
        [Tooltip("Height above the target's collider top (m).")]
        [SerializeField] private float heightAboveTop = 0.3f;
        [Tooltip("Never float higher than this above the target's pivot (huge doors).")]
        [SerializeField] private float maxHeight = 2.4f;

        private Transform target;
        private float height;
        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;
            panel.gameObject.SetActive(false);
        }

        private void OnEnable() => PlayerInteractor.OnFocusChanged += HandleFocusChanged;
        private void OnDisable() => PlayerInteractor.OnFocusChanged -= HandleFocusChanged;

        private void HandleFocusChanged(Transform newTarget, string text)
        {
            target = newTarget;
            panel.gameObject.SetActive(target != null);
            if (target == null) return;
            label.text = "[F] " + text;
            float top = target.TryGetComponent(out Collider col) ? col.bounds.max.y - target.position.y : 1f;
            height = Mathf.Min(top + heightAboveTop, maxHeight);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (!target.gameObject.activeInHierarchy)
            {
                HandleFocusChanged(null, null);
                return;
            }
            if (cam == null) cam = Camera.main;
            Vector3 screen = cam.WorldToScreenPoint(target.position + Vector3.up * height);
            panel.position = screen;
        }
    }
}
