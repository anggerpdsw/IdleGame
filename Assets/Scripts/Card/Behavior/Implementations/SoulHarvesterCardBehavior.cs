using System;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class SoulHarvesterCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.SoulHarvester;

        private int _soulCount;
        private int _initialSoulCap;
        private int _maxStack;
        private long _totalSoulsEarned;
        private const string ModifierId = "Card:SoulHarvester";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _soulCount = 0;
            _initialSoulCap = Mathf.RoundToInt(GetParameter("InitialSoulCap"));
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
            if (damageSource != DamageSource.Player.ToString()) return;
            _totalSoulsEarned += count;
            int soulsPerCapIncrease = Mathf.RoundToInt(GetParameter("SoulsPerCapIncrease"));
            int capIncrease = Mathf.RoundToInt(GetParameter("SoulCapIncrease"));
            if (_maxStack <= 0) return;
            if (soulsPerCapIncrease > 0 && capIncrease > 0)
            {
                long expandedCap = _initialSoulCap
                    + (_totalSoulsEarned / soulsPerCapIncrease) * capIncrease;
                _maxStack = (int)Math.Min(expandedCap, int.MaxValue);
            }
            _soulCount = (int)Math.Min(_totalSoulsEarned, _maxStack);

            RefreshModifier();
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
    }
}