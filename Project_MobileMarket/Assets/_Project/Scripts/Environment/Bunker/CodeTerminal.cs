using System;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The 2F command-room lockdown terminal (P3). F asks for the code entry screen (UIManager opens it); the right code
    /// lifts the lockdown and opens the linked shutters (stair B). The code's two halves are written on notes in the bunker.
    /// </summary>
    public class CodeTerminal : MonoBehaviour, IInteractable, IResolvable
    {
        /// <summary>Raised when the player wants to type a code into this terminal.</summary>
        public static event Action<CodeTerminal> OnCodeRequested;

        [SerializeField] private string code = "0417";
        [SerializeField] private LockdownShutter[] unlocks = Array.Empty<LockdownShutter>();
        [SerializeField] private string unlockedText = "봉쇄 프로토콜 해제. 계단 B 격벽이 열린다.";
        [SerializeField] private string alreadyText = "단말기: 봉쇄 해제됨.";

        public bool IsUnlocked { get; private set; }
        public bool IsResolved => IsUnlocked;
        public int CodeLength => code.Length;

        public void Interact(GameObject interactor)
        {
            if (IsUnlocked)
            {
                InteractionFeedback.Show(alreadyText);
                return;
            }
            OnCodeRequested?.Invoke(this);
        }

        /// <summary>Checks an entered code; on success unlocks and opens the linked shutters.</summary>
        public bool TrySubmit(string entered)
        {
            if (IsUnlocked) return true;
            if (entered != code) return false;
            IsUnlocked = true;
            foreach (var s in unlocks) if (s != null) s.Open();
            InteractionFeedback.Show(unlockedText);
            return true;
        }
    }
}
