using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ArcaneResonanceCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.ArcaneResonance;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        private int _attacksRemaining;
        private float _bonusExpiry;
        private const string DamageModifierId = "Card:ArcaneResonance";
        private const string CritModifierId = "Card:ArcaneResonance:Crit";

        // UI accessors
        public int AttacksRemaining => _attacksRemaining;
        public float RemainingDuration => Mathf.Max(0f, _bonusExpiry - Time.time);
        public bool IsActive => _attacksRemaining > 0 && Time.time < _bonusExpiry;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attacksRemaining = 0;
            _bonusExpiry = 0f;
            ModifierManager.Instance.RemoveModifier(DamageModifierId);
            ModifierManager.Instance.RemoveModifier(CritModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _attacksRemaining = 0;
            _bonusExpiry = 0f;
            ModifierManager.Instance.RemoveModifier(DamageModifierId);
            ModifierManager.Instance.RemoveModifier(CritModifierId);
        }

        public void OnUltimateUsed()
        {
            if (IsActive) return;
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
            ModifierManager.Instance.RemoveModifier(DamageModifierId);
            ModifierManager.Instance.RemoveModifier(CritModifierId);
            if (_attacksRemaining <= 0) return;

            // Use CurrentValue (BaseValue + ValuePerLevel scaling) for damage bonus
            float damageBonus = GetCurrentValue();
            var modifier = new StatModifier
            {
                Id = DamageModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackDamage,
                Mode = ModifierMode.Percent,
                Value = damageBonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);

            // Crit bonus from Parameters (static value)
            float critBonus = GetParameter("CriticalChanceBonusPercent", 8f);
            var critModifier = new StatModifier
            {
                Id = CritModifierId,
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
