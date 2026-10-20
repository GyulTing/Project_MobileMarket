using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Common;

namespace MobileMarket.UI
{
    /// <summary>
    /// Bottom-centre line for InteractionFeedback messages ("잠겨 있다", note text). Stays longer for longer text, then fades.
    /// </summary>
    public class MessageHUD : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float baseTime = 2.5f;
        [SerializeField] private float timePerChar = 0.06f;
        [SerializeField] private float fadeTime = 0.5f;

        private float hideAt;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        private void OnEnable() => InteractionFeedback.OnMessage += Show;
        private void OnDisable() => InteractionFeedback.OnMessage -= Show;

        private void Show(string message)
        {
            label.text = message;
            hideAt = Time.unscaledTime + baseTime + message.Length * timePerChar;
            group.alpha = 1f;
        }

        private void Update()
        {
            if (group == null || group.alpha <= 0f) return;
            float left = hideAt - Time.unscaledTime;
            group.alpha = Mathf.Clamp01(left / fadeTime);
        }
    }
}
