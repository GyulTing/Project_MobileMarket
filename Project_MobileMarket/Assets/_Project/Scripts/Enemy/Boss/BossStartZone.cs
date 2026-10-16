using System;
using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>Trigger area in front of the boss: fires once when the player walks in.</summary>
    [RequireComponent(typeof(Collider))]
    public class BossStartZone : MonoBehaviour
    {
        public event Action<Transform> OnPlayerEntered;

        private bool triggered;

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;
            var member = other.GetComponent<FactionMember>();
            if (member == null || member.Faction != Faction.Player) return;
            triggered = true;
            OnPlayerEntered?.Invoke(other.transform);
        }
    }
}
