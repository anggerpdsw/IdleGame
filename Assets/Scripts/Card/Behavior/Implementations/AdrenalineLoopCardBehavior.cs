using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class AdrenalineLoopCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.AdrenalineLoop;

        private bool _effectActive;
        private const string ModifierId = "Card:AdrenalineLoop";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _effectActive = false;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _effectActive = false;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnPlayerDamaged(float damage, float currentHp, float maxHp)
        {
            float hpPercent = currentHp / maxHp;
            float threshold = GetParameter("HealthThresholdPercent", 40f) * 0.01f;

            bool shouldBeActive = hpPercent <= threshold;
            if (shouldBeActive != _effectActive)
            {
                _effectActive = shouldBeActive;
                RefreshModifier();
            }
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (!_effectActive || damageSource != DamageSource.Player.ToString()) return;

            float healPercent = GetParameter("HealOnKillPercent", 1.5f);
            float player_maxHp = Player.Player.Instance.MaxHealth;
            float healAmount = player_maxHp * (healPercent * 0.01f);

            Player.Player.Instance.Heal(healAmount, false);
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (!_effectActive) return;

            float bonusPercent = GetParameter("AttackSpeedBonusPercent", 20f);

            var modifier = new StatModifier
            {
                Id = ModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackSpeed,
                Mode = ModifierMode.Percent,
                Value = bonusPercent,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }
    }
}
