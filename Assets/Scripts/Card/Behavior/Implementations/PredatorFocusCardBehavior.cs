using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class PredatorFocusCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.PredatorFocus;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        private const string ModifierId = "Card:PredatorFocus";
        private int _enemyThreshold;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _enemyThreshold = Mathf.RoundToInt(GetParameter("EnemyThreshold", 3f));
        }

        public override void OnPlayerAttack()
        {
            UpdateModifier();
        }

        private void UpdateModifier()
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            // Count enemies within attack range
            int enemyCount = CountEnemiesInRange(player.transform.position, player.AttackRange);
            bool isActive = enemyCount <= _enemyThreshold && enemyCount > 0;

            ModifierManager.Instance.RemoveModifier(ModifierId);

            if (isActive)
            {
                float bonusPercent = GetCurrentValue();
                var modifier = new StatModifier
                {
                    Id = ModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackDamage,
                    Mode = ModifierMode.Percent,
                    Value = bonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        private int CountEnemiesInRange(Vector2 center, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, LayerMask.GetMask("Enemy"));
            int count = 0;
            foreach (var hit in hits)
            {
                if (hit != null && hit.GetComponent<EnemyAi>() != null)
                    count++;
            }
            return count;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            UpdateModifier();
        }
    }
}