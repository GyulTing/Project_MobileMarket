using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Managers;

namespace MobileMarket.UI
{
    /// <summary>Top-left "주요 목표" panel showing the current objective summary from QuestManager. Hidden while there is none.</summary>
    public class ObjectiveHUD : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text objectiveText;

        private void OnEnable()
        {
            QuestManager.OnObjectiveChanged += Refresh;
            Refresh(QuestManager.Instance != null ? QuestManager.Instance.CurrentObjective : string.Empty);
        }

        private void OnDisable()
        {
            QuestManager.OnObjectiveChanged -= Refresh;
        }

        private void Refresh(string objective)
        {
            bool has = !string.IsNullOrEmpty(objective);
            if (panel != null) panel.SetActive(has);
            if (objectiveText != null) objectiveText.text = objective;
        }
    }
}
