using System;
using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Power state of the abandoned bunker. The 2F generator restores emergency power (opens stair A's shutter); with
    /// emergency power on, the 1F main lever restores main power (opens the 2F stair-hall lockdown door, lights).
    /// Doors, shutters and lights subscribe to the events; this component only holds the state.
    /// </summary>
    public class BunkerPower : MonoBehaviour
    {
        public event Action OnEmergencyPowerRestored;
        public event Action OnMainPowerRestored;

        public bool EmergencyOn { get; private set; }
        public bool MainOn { get; private set; }

        public void RestoreEmergency()
        {
            if (EmergencyOn) return;
            EmergencyOn = true;
            OnEmergencyPowerRestored?.Invoke();
        }

        /// <summary>Main power needs emergency power first. Returns false when that is still off.</summary>
        public bool RestoreMain()
        {
            if (!EmergencyOn) return false;
            if (MainOn) return true;
            MainOn = true;
            OnMainPowerRestored?.Invoke();
            return true;
        }

        [ContextMenu("Restore Emergency (test)")] private void TestEmergency() => RestoreEmergency();
        [ContextMenu("Restore Main (test)")] private void TestMain() => RestoreMain();
    }
}
