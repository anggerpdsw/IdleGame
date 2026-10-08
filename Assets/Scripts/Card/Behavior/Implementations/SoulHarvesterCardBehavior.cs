using System;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class SoulHarvesterCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.SoulHarvester;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        private int _soulCount;
        private int _initialSoulCap;
        private int _maxStack;
        private long _totalSoulsEarned;
        private const string ModifierId = "Card:SoulHarvester";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _soulCount = 0;
            _initialSoulCap = Mathf.RoundToInt(GetParameter("InitialSoulCap", 666f));
            _maxStack = _initialSoulCap;
            _totalSoulsEarned = 0;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _soulCount = 0;
            _totalSoulsEarned = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (damageSource != DamageSource.Player.ToString()) return;

            _totalSoulsEarned += count;

            // Calculate dynamic cap: InitialSoulCap + (totalKills / SoulsPerCapIncrease) * SoulCapIncrease
            int soulsPerCapIncrease = Mathf.RoundToInt(GetParameter("SoulsPerCapIncrease", 666f));
            int capIncrease = Mathf.RoundToInt(GetParameter("SoulCapIncrease", 66f));

            if (soulsPerCapIncrease > 0 && capIncrease > 0)
            {
                long expandedCap = _initialSoulCap + _totalSoulsEarned / soulsPerCapIncrease * capIncrease;
                _maxStack = (int)Math.Min(expandedCap, int.MaxValue);
            }

            _soulCount = (int)Math.Min(_totalSoulsEarned, _maxStack);
            RefreshModifier();
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            // No-op: soul counting happens in overload 1
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (_soulCount <= 0) return;

            float percentPerSoul = GetCurrentValue();
            float totalBonus = _soulCount * percentPerSoul;

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

        public int GetSoulCount() => _soulCount;
        public int GetMaxStack() => _maxStack;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.SoulHarvester;

        public CardHUDData GetHUDData()
        {
            if (_soulCount <= 0) return new CardHUDData(null, string.Empty);
            return new CardHUDData(GetCardIcon(), $"{_soulCount} \r\n {_maxStack}");
        }
    }
}
