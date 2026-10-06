using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class StaggerMasterCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.StaggerMaster;
        public override CardEventType[] SubscribedEvents => System.Array.Empty<CardEventType>();

        private float _damageBonusPercent;
        private float _durationSeconds;
        private int _maximumStacks;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _damageBonusPercent = GetParameter("DamageBonusPercent", 8f);
            _durationSeconds = GetParameter("DurationSeconds", 2f);
            _maximumStacks = Mathf.RoundToInt(GetParameter("MaximumStacks", 1f));
        }

        public float GetDamageBonusMultiplier(EnemyAi enemy)
        {
            if (enemy == null) return 1f;

            // Check if enemy has knockback or stun status
            bool isStaggered = enemy.IsKnockedBack || enemy.IsStunned;

            if (isStaggered)
            {
                return 1f + (_damageBonusPercent * 0.01f);
            }

            return 1f;
        }
    }
}