using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class StaggerMasterCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.StaggerMaster;
        public override CardEventType[] SubscribedEvents => System.Array.Empty<CardEventType>();

        private float _durationSeconds;

        // Track bonus expiry per enemy
        private readonly Dictionary<EnemyAi, float> _bonusExpires = new();

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _durationSeconds = GetParameter("DurationSeconds", 3f);
        }

        /// <summary>
        /// Returns damage multiplier for staggered enemy.
        /// Bonus persists for DurationSeconds after stagger ends.
        /// Called per-hit from EnemyAi.TakeDamage pipeline.
        /// </summary>
        public float GetDamageMultiplier(EnemyAi enemy)
        {
            if (enemy == null) return 1f;

            bool isStaggered = enemy.IsKnockedBack || enemy.IsStunned;

            // If currently staggered: refresh duration and apply bonus
            if (isStaggered)
            {
                _bonusExpires[enemy] = Time.time + _durationSeconds;
                return 1f + (GetCurrentValue() * 0.01f);
            }

            // If bonus duration still active: keep bonus
            if (_bonusExpires.TryGetValue(enemy, out float expireTime) && Time.time < expireTime)
            {
                return 1f + (GetCurrentValue() * 0.01f);
            }

            // Duration expired: cleanup and return 1x
            _bonusExpires.Remove(enemy);
            return 1f;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            _bonusExpires.Clear();
        }
    }
}
