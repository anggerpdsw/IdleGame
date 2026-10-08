using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class DangerZoneCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.DangerZone;
        public override CardEventType[] SubscribedEvents => System.Array.Empty<CardEventType>();

        private float _radiusFactor;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _radiusFactor = GetParameter("Radius", 0.75f);
        }

        /// <summary>
        /// Returns damage multiplier for enemy inside danger zone.
        /// Radius = AttackRange * radiusFactor, recalculated per hit.
        /// Called per-hit from EnemyAi.TakeDamage pipeline.
        /// </summary>
        public float GetDamageMultiplier(EnemyAi enemy)
        {
            if (enemy == null) return 1f;

            var player = PlayerClass.Instance;
            if (player == null) return 1f;

            float radius = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) * _radiusFactor;
            float sqrDistance = ((Vector2)enemy.transform.position - (Vector2)player.transform.position).sqrMagnitude;

            return sqrDistance <= radius * radius
                ? 1f + (GetCurrentValue() * 0.01f)
                : 1f;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
        }
    }
}
