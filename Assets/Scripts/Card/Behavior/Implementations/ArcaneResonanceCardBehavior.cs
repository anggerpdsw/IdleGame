using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ArcaneResonanceCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.ArcaneResonance;

        private int _attacksRemaining;
        private float _bonusExpiry;
        private const string ModifierId = "Card:ArcaneResonance";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attacksRemaining = 0;
            _bonusExpiry = 0f;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _attacksRemaining = 0;
            _bonusExpiry = 0f;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public void OnUltimateUsed()
        {
            _attacksRemaining = Mathf.RoundToInt(GetParameter("AttackCount", 8f));
            _bonusExpiry = Time.time + GetParameter("DurationSeconds", 6f);
            RefreshModifier();
        }

        public override void OnPlayerAttack()
        {
            if (_attacksRemaining > 0)
            {
                _attacksRemaining--;
                if (_attacksRemaining <= 0)
                {
                    _bonusExpiry = 0f;
                    RefreshModifier();
                }
            }
        }

        public override void Update(float deltaTime)
        {
            if (_attacksRemaining <= 0) return;
            if (Time.time >= _bonusExpiry)
            {
                _attacksRemaining = 0;
                RefreshModifier();
            }
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (_attacksRemaining <= 0) return;

            float damageBonus = GetParameter("AttackDamageBonusPercent", 18f);

            var modifier = new StatModifier
            {
                Id = ModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackDamage,
                Mode = ModifierMode.Percent,
                Value = damageBonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);

            // Apply crit chance bonus
            float critBonus = GetParameter("CriticalChanceBonusPercent", 8f);
            var critModifier = new StatModifier
            {
                Id = ModifierId + ":Crit",
                Source = ModifierSource.Card,
                Stat = SkillType.CriticalChance,
                Mode = ModifierMode.Flat,
                Value = critBonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(critModifier);
        }
    }
}
