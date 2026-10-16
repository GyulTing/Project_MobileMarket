using System;
using System.Collections;
using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// The boss's heat vents as a group. OpenAll() starts a cooling window; vents broken in the current phase stay broken
    /// (they reopen shorting out), so the player only has to finish the rest. When every vent is broken the covers come
    /// down after a short pause and OnAllVentsBroken fires; NewPhase() makes them all breakable again.
    /// </summary>
    public class BossVents : MonoBehaviour
    {
        [Tooltip("Seconds the broken vents keep shorting out before the covers come down.")]
        [SerializeField] private float closeDelay = 1.5f;

        /// <summary>A vent broke; the argument is how many are broken this phase.</summary>
        public event Action<int> OnVentBroken;
        public event Action OnAllVentsBroken;

        public bool IsOpen { get; private set; }
        public int Total => vents.Length;
        public int BrokenCount { get; private set; }
        public bool AllBroken => BrokenCount >= vents.Length;

        private VentHeat[] vents;
        private WaitForSeconds closeWait;

        private void Awake()
        {
            vents = GetComponentsInChildren<VentHeat>(true);
            closeWait = new WaitForSeconds(closeDelay);
        }

        private void OnEnable()
        {
            foreach (var vent in vents) vent.OnBroken += HandleVentBroken;
        }

        private void OnDisable()
        {
            foreach (var vent in vents) vent.OnBroken -= HandleVentBroken;
        }

        [ContextMenu("Open All Vents")]
        public void OpenAll()
        {
            if (IsOpen) return;
            IsOpen = true;
            foreach (var vent in vents) vent.Open();
        }

        [ContextMenu("Close All Vents")]
        public void CloseAll()
        {
            IsOpen = false;
            foreach (var vent in vents) vent.Close();
        }

        /// <summary>Next boss phase: all vents breakable again.</summary>
        public void NewPhase()
        {
            BrokenCount = 0;
            foreach (var vent in vents) vent.ResetForNewPhase();
        }

        /// <summary>Test helper: breaks the first open vent.</summary>
        [ContextMenu("Break One Vent (test)")]
        private void BreakOneForTest()
        {
            foreach (var vent in vents)
            {
                if (vent.CurrentState != VentHeat.State.Open) continue;
                vent.TakeDamage(vent.Health);
                return;
            }
        }

        private void HandleVentBroken(VentHeat vent)
        {
            BrokenCount++;
            OnVentBroken?.Invoke(BrokenCount);
            if (AllBroken) StartCoroutine(CloseAfterDelay());
        }

        private IEnumerator CloseAfterDelay()
        {
            yield return closeWait;
            CloseAll();
            OnAllVentsBroken?.Invoke();
        }
    }
}
