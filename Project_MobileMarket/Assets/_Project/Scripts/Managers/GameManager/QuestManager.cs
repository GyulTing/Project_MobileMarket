using System;
using UnityEngine;

namespace MobileMarket.Managers
{
    /// <summary>
    /// Holds the current objective (a short summary line + optional world target). The HUD shows the line top-left and the
    /// target on the minimap. Quests themselves don't exist yet; whatever gives the player a quest calls SetObjective.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        /// <summary>Raised with the new objective text, or an empty string when the objective is cleared.</summary>
        public static event Action<string> OnObjectiveChanged;

        [Tooltip("Objective set on start (for testing the HUD). Leave empty for none.")]
        [SerializeField] private string startObjective;
        [SerializeField] private Transform startTarget;

        public string CurrentObjective { get; private set; } = string.Empty;
        /// <summary>Where the objective is in the world (shown on the minimap), or null.</summary>
        public Transform ObjectiveTarget { get; private set; }
        public bool HasObjective => !string.IsNullOrEmpty(CurrentObjective);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(startObjective)) SetObjective(startObjective, startTarget);
        }

        public void SetObjective(string summary, Transform target = null)
        {
            CurrentObjective = summary ?? string.Empty;
            ObjectiveTarget = target;
            OnObjectiveChanged?.Invoke(CurrentObjective);
        }

        public void ClearObjective() => SetObjective(string.Empty);
    }
}
