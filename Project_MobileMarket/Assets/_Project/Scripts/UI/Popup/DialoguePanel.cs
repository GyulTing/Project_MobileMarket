using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MobileMarket.Environment;

namespace MobileMarket.UI
{
    /// <summary>Minimal placeholder talk window — shows the merchant's name and a greeting line.</summary>
    public class DialoguePanel : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text lineText;
        [SerializeField] private float closeDistance = 2.5f;

        private MerchantNPC currentMerchant;
        private Transform interactorTransform;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (Vector3.Distance(interactorTransform.position, currentMerchant.transform.position) > closeDistance)
            {
                Close();
            }
        }

        public void Open(MerchantNPC merchant, GameObject interactor)
        {
            currentMerchant = merchant;
            interactorTransform = interactor.transform;
            nameText.text = merchant.MerchantName;
            lineText.text = merchant.Greeting;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
