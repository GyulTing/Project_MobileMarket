using System.Collections.Generic;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Marks something that takes part in fights (enemies and the player) and which side it is on.
    /// Every active member is kept in Active so the AI can look for targets without Find calls or physics queries.
    /// </summary>
    public class FactionMember : MonoBehaviour
    {
        private static readonly List<FactionMember> active = new List<FactionMember>();
        public static IReadOnlyList<FactionMember> Active => active;

        [SerializeField] private Faction faction;
        [Tooltip("Point other fighters aim at, relative to this object (chest height).")]
        [SerializeField] private Vector3 aimOffset = new Vector3(0f, 1.2f, 0f);

        public Faction Faction => faction;
        public Vector3 AimPoint => transform.position + aimOffset;

        private IHealth health;

        /// <summary>Alive and targetable. Members without any health component are treated as always alive.</summary>
        public bool IsAlive => isActiveAndEnabled && (health == null || !health.IsDead);

        private void Awake()
        {
            health = GetComponent<IHealth>();
        }

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        public bool IsHostileTo(FactionMember other) => other != null && FactionRelations.IsHostile(faction, other.faction);
    }
}
