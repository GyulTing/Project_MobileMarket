using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Light groups that follow the bunker's power: emergency lights come on with emergency power, main lights with main
    /// power. Lights listed here start switched off.
    /// </summary>
    public class PowerLights : MonoBehaviour
    {
        [SerializeField] private BunkerPower power;
        [SerializeField] private Light[] emergencyLights = System.Array.Empty<Light>();
        [SerializeField] private Light[] mainLights = System.Array.Empty<Light>();

        private void Awake()
        {
            Set(emergencyLights, false);
            Set(mainLights, false);
        }

        private void OnEnable()
        {
            if (power == null) return;
            power.OnEmergencyPowerRestored += HandleEmergency;
            power.OnMainPowerRestored += HandleMain;
            if (power.EmergencyOn) HandleEmergency();
            if (power.MainOn) HandleMain();
        }

        private void OnDisable()
        {
            if (power == null) return;
            power.OnEmergencyPowerRestored -= HandleEmergency;
            power.OnMainPowerRestored -= HandleMain;
        }

        private void HandleEmergency() => Set(emergencyLights, true);
        private void HandleMain() => Set(mainLights, true);

        private static void Set(Light[] lights, bool on)
        {
            foreach (var l in lights) if (l != null) l.enabled = on;
        }
    }
}
