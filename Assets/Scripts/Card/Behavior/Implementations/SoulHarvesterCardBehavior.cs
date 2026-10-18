using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class SoulHarvesterCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.SoulHarvester;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        private int _soulCount;
        private int _maxSouls;
        private int _initialKillThreshold;
        private int _killIncrement;
        private int _remainingKills;

        private const string ModifierId = "Card:SoulHarvester";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _soulCount = 0;
            _maxSouls = Mathf.RoundToInt(GetParameter("MaxSouls", 66f));
            _initialKillThreshold = Mathf.RoundToInt(GetParameter("InitialKillThreshold", 666f));
            _killIncrement = Mathf.RoundToInt(GetParameter("KillIncrement", 66f));
            _remainingKills = _initialKillThreshold;
            RefreshModifier();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _soulCount = 0;
            _remainingKills = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (damageSource != DamageSource.Player.ToString()) return;
            if (_soulCount >= _maxSouls) return;

            _remainingKills -= count;

            while (_remainingKills <= 0 && _soulCount < _maxSouls)
            {
                _soulCount++;
                if (_soulCount < _maxSouls)
                {
                    _remainingKills += _killIncrement;
                }
                else
                {
                    _remainingKills = 0;
                }
            }

            RefreshModifier();
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            // Handled in count-based overload
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
        public int GetRemainingKills() => _remainingKills;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.SoulHarvester;

        public CardHUDData GetHUDData()
        {
            int maxSouls = Mathf.RoundToInt(GetParameter("MaxSouls", 66f));
            string countPart = $"{_soulCount}/{maxSouls}";
            string remainingPart = _soulCount < maxSouls ? $"{GetRemainingKills()} kills left\n" : "MAX\n";
            return new CardHUDData(GetCardIcon(), $"{remainingPart}{countPart}");
        }
    }
}