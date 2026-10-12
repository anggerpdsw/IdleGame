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
        private long _killCount;
        private int _maxSouls;
        private int _threshold;
        private int _increment;

        private const string ModifierId = "Card:SoulHarvester";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _soulCount = 0;
            _killCount = 0;

            _maxSouls = Mathf.RoundToInt(GetParameter("MaxSouls", 66f));
            _threshold = Mathf.RoundToInt(GetParameter("InitialKillThreshold", 666f));
            _increment = Mathf.RoundToInt(GetParameter("KillIncrement", 66f));
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _soulCount = 0;
            _killCount = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (damageSource != DamageSource.Player.ToString()) return;

            _killCount += count;

            int calculated = 0;
            if (_killCount >= _threshold && _increment > 0)
            {
                calculated = 1 + (int)((_killCount - _threshold) / _increment);
            }

            _soulCount = Mathf.Min(calculated, _maxSouls);
            RefreshModifier();
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            // Soul counting handled in count-based overload
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
        public int GetMaxStack() => _maxSouls;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.SoulHarvester;

        public CardHUDData GetHUDData()
        {
            float totalBonus = _soulCount * GetCurrentValue();
            int maxSouls = Mathf.RoundToInt(GetParameter("MaxSouls", 66f));
            return new CardHUDData(GetCardIcon(), $"{_soulCount}/{maxSouls}\n{totalBonus:F1}%");
        }
    }
}
