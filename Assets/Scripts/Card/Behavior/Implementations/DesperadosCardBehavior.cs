using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class DesperadosCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.Desperados;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnWaveComplete };

        private float _bonus;
        private int _maxStack;
        private float _positiveValue;
        private float _negativeValue;
        private const string ModifierId = "Card:Desperados_HealthPoint";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _bonus = 0f;
            _maxStack = Mathf.RoundToInt(GetParameter("MaxStacks"));
            _positiveValue = GetParameter("PositiveBonus");
            _negativeValue = GetParameter("NegativeBonus");
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnWaveComplete(int waveNumber)
        {
            if (waveNumber <= GetParameter("ActivationWave")) return;

            float chance = GetCurrentValue();
            float bonus = Utilityku.Chance(chance) ? _positiveValue : _negativeValue;

            _bonus += bonus;
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
                Stat = SkillType.HealthPoint,
                Mode = ModifierMode.Percent,
                Value = _bonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }

        public float GetBonus() => _bonus;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.Desperados;

        public CardHUDData GetHUDData()
        {
            if (_bonus == 0f) return new CardHUDData(null, string.Empty);
            return new CardHUDData(GetCardIcon(), $"{_bonus:0}%");
        }
    }
}