using UnityEngine;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class CrazyGamblerCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.CrazyGambler;

        private float _bonus;
        private int _maxStack;
        private float _positiveValue;
        private float _negativeValue;
        private const string ModifierId = "Card:CrazyGambler_AttackDamage";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _bonus = 0f;
            _maxStack = Mathf.RoundToInt(GetParameter("MaxStacks"));
            _positiveValue = GetParameter("PositiveBonusPercent") * 0.01f;
            _negativeValue = GetParameter("NegativeBonusPercent") * 0.01f;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnWaveComplete(int waveNumber)
        {
            if (waveNumber <= GetParameter("ActivationWave")) return;

            float chancePercent = GetCurrentValue();
            float bonusPercent = Utilityku.Chance(chancePercent) ? _positiveValue : _negativeValue;

            _bonus += bonusPercent;
            _bonus = Mathf.Clamp(_bonus, _negativeValue * _maxStack, _positiveValue * _maxStack);

            RefreshModifier();
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (Mathf.Approximately(_bonus, 0f)) return;

            var modifier = new StatModifier
            {
                Id = ModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackDamage,
                Mode = ModifierMode.Percent,
                Value = _bonus * 100f,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }

        public float GetBonus() => _bonus;
    }
}