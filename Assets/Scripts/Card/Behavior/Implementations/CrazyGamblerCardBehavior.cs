using UnityEngine;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class CrazyGamblerCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.CrazyGambler;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnWaveComplete };

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
                Stat = SkillType.AttackDamage,
                Mode = ModifierMode.Percent,
                Value = _bonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }

        public float GetBonus() => _bonus;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.CrazyGambler;

        public CardHUDData GetHUDData()
        {
            if (_bonus == 0f) return new CardHUDData(null, string.Empty);
            return new CardHUDData(GetCardIcon(), $"{_bonus:0}%");
        }
    }
}