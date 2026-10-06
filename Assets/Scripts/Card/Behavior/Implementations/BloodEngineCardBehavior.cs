using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BloodEngineCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.BloodEngine;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnLifeSteal };

        private float _bonusExpiry;
        private int _currentStacks;
        private const string ModifierId = "Card:BloodEngine";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _bonusExpiry = 0f;
            _currentStacks = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _bonusExpiry = 0f;
            _currentStacks = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnPlayerLifeSteal(float healAmount, float currentHp, float maxHp)
        {
            float hpPercent = currentHp / maxHp;
            if (hpPercent <= GetParameter("HealthThresholdPercent", 80f) * 0.01f) return;

            _currentStacks = Mathf.Min(
                _currentStacks + 1,
                Mathf.RoundToInt(GetParameter("MaximumStacks", 5f)));

            _bonusExpiry = Time.time + GetParameter("DurationSeconds", 4f);
            RefreshModifier();
        }

        public override void Update(float deltaTime)
        {
            if (_currentStacks <= 0) return;
            if (Time.time >= _bonusExpiry)
            {
                _currentStacks--;
                if (_currentStacks <= 0)
                    _bonusExpiry = 0f;
                RefreshModifier();
            }
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (_currentStacks <= 0) return;

            float conversionPercent = GetParameter("ConversionPercent", 18f);
            float totalBonus = _currentStacks * conversionPercent;

            var modifier = new StatModifier
            {
                Id = ModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackDamage,
                Mode = ModifierMode.Percent,
                Value = totalBonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }
    }
}
