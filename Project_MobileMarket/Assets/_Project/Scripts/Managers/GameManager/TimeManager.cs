using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobileMarket.Managers
{
    /// <summary>Tracks the in-game day. A day passes after secondsPerDay of real time, or when AdvanceDay() is called.</summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }
        public static event Action<int> OnDayChanged;

        [SerializeField] private float secondsPerDay = 300f;
        [SerializeField] private bool enableDebugSkip = true;

        public int CurrentDay { get; private set; } = 1;

        private float dayTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (enableDebugSkip && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
            {
                AdvanceDay();
                return;
            }

            dayTimer += Time.deltaTime;
            if (dayTimer >= secondsPerDay) AdvanceDay();
        }

        public void AdvanceDay()
        {
            dayTimer = 0f;
            CurrentDay++;
            Debug.Log($"[TimeManager] Day {CurrentDay}");
            OnDayChanged?.Invoke(CurrentDay);
        }
    }
}
