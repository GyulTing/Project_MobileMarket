using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// An AI-lockdown shutter or door that opens by itself when the bunker gets the power it waits for: its leaves move
    /// open (the DoorLeafMotion of its door set) and it stops blocking the way.
    /// </summary>
    public class LockdownShutter : MonoBehaviour
    {
        private enum PowerStage { Emergency, Main }

        [SerializeField] private BunkerPower power;
        [SerializeField] private PowerStage opensOn = PowerStage.Emergency;
        [Tooltip("Collider that blocks the way while closed (disabled when open).")]
        [SerializeField] private Collider blocker;

        private DoorLeafMotion motion;

        public bool IsOpen { get; private set; }

        private void Awake() => motion = GetComponentInChildren<DoorLeafMotion>();

        private void OnEnable()
        {
            if (power == null) return;
            if (opensOn == PowerStage.Emergency) power.OnEmergencyPowerRestored += Open; else power.OnMainPowerRestored += Open;
            if (opensOn == PowerStage.Emergency ? power.EmergencyOn : power.MainOn) Open();
        }

        private void OnDisable()
        {
            if (power == null) return;
            power.OnEmergencyPowerRestored -= Open;
            power.OnMainPowerRestored -= Open;
        }

        [ContextMenu("Open (test)")]
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (blocker != null) blocker.enabled = false;
            if (motion != null) motion.Open();
        }
    }
}
