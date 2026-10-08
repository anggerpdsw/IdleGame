using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class VampiricFrenzyCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.VampiricFrenzy;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnLifeSteal };

        private float _accumulator;
        private readonly Queue<float> _stackExpiryTimes = new();
        private const string ModifierId = "Card:VampiricFrenzy";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _accumulator = 0f;
            _stackExpiryTimes.Clear();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _accumulator = 0f;
            _stackExpiryTimes.Clear();
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            RefreshModifier();
        }

        public override void OnPlayerLifeSteal(float healAmount, float currentHp, float maxHp)
        {
            if (healAmount <= 0f || maxHp <= 0f) return;
            if (ExpireStacks(Time.time)) RefreshModifier();

            int maxStacks = Mathf.RoundToInt(GetParameter("MaximumStacks"));
            if (_stackExpiryTimes.Count >= maxStacks) return;

            _accumulator += healAmount;
            float healThreshold = maxHp * GetParameter("HealPerStackPercent") * 0.01f;
            if (healThreshold <= 0f) return;

            float expiryTime = Time.time + GetParameter("DurationSeconds");
            int previousStacks = _stackExpiryTimes.Count;
            while (_accumulator >= healThreshold && _stackExpiryTimes.Count < maxStacks)
            {
                _accumulator -= healThreshold;
                _stackExpiryTimes.Enqueue(expiryTime);
            }

            if (_stackExpiryTimes.Count != previousStacks) RefreshModifier();
        }

        public override void Update(float deltaTime)
        {
            if (ExpireStacks(Time.time)) RefreshModifier();
        }

        private bool ExpireStacks(float currentTime)
        {
            bool expired = false;
            while (_stackExpiryTimes.Count > 0 && _stackExpiryTimes.Peek() <= currentTime)
            {
                _stackExpiryTimes.Dequeue();
                expired = true;
            }
            return expired;
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (_stackExpiryTimes.Count <= 0) return;

            float totalBonus = _stackExpiryTimes.Count * GetCurrentValue();

            var modifier = new StatModifier
            {
                Id = ModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackSpeed,
                Mode = ModifierMode.Percent,
                Value = totalBonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.VampiricFrenzy;

        public CardHUDData GetHUDData()
        {
            if (_stackExpiryTimes.Count <= 0) return new CardHUDData(null, string.Empty);
            int maxStacks = Mathf.RoundToInt(GetParameter("MaximumStacks"));
            return new CardHUDData(GetCardIcon(), $"{_stackExpiryTimes.Count} \r\n {maxStacks}");
        }
    }
}