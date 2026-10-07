using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>How an enemy hurts its target. EnemyBrain calls Tick every frame while the target is in range, End when it stops.</summary>
    public abstract class EnemyAttack : MonoBehaviour
    {
        protected static readonly int AttackingParam = Animator.StringToHash("Attacking");

        public abstract float Range { get; }

        public abstract void Tick(FactionMember target);

        public abstract void End();
    }
}
