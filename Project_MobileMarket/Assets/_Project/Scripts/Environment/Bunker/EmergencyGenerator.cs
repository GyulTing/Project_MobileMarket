using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The 2F emergency diesel generator (second half of P1). With the switchboard's fuse in, F starts it and the bunker
    /// gets emergency power (stair A's shutter opens).
    /// </summary>
    public class EmergencyGenerator : MonoBehaviour, IInteractable, IResolvable
    {
        [SerializeField] private BunkerPower power;
        [SerializeField] private FuseSwitchboard switchboard;
        [SerializeField] private AudioSource runningSound;
        [SerializeField] private string noFuseText = "시동이 걸리지 않는다. 배전반을 확인해 보자.";
        [SerializeField] private string startedText = "발전기가 굉음을 내며 돌아간다. 비상전원이 들어왔다.";
        [SerializeField] private string runningText = "발전기가 돌아가고 있다.";

        public bool IsResolved => power != null && power.EmergencyOn;

        public void Interact(GameObject interactor)
        {
            if (power == null) return;
            if (power.EmergencyOn)
            {
                InteractionFeedback.Show(runningText);
                return;
            }
            if (switchboard == null || !switchboard.FuseInstalled)
            {
                InteractionFeedback.Show(noFuseText);
                return;
            }
            power.RestoreEmergency();
            if (runningSound != null) runningSound.Play();
            InteractionFeedback.Show(startedText);
        }
    }
}
