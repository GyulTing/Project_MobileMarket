using UnityEngine;
using UnityEngine.UI;

namespace MobileMarket.UI
{
    /// <summary>World-space text shown above the owner's head (e.g. "심는 중..."). Always faces the camera.</summary>
    public class ActionLabelUI : MonoBehaviour
    {
        [SerializeField] private GameObject labelRoot;
        [SerializeField] private Text label;

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

        public void Show(string text)
        {
            label.text = text;
            labelRoot.SetActive(true);
        }

        public void Hide()
        {
            labelRoot.SetActive(false);
        }
    }
}
