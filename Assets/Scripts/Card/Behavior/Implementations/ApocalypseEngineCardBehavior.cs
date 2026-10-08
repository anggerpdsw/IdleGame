using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ApocalypseEngineCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.ApocalypseEngine;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        private int _killCount;
        private int _stacks;
        private const string ModifierPrefix = "Card:ApocalypseEngine";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _killCount = 0;
            _stacks = 0;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            RemoveModifiers();
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (damageSource != DamageSource.Player.ToString()) return;
            _killCount += count;
            int killsPerStack = Mathf.RoundToInt(GetParameter("KillsPerStack"));
            bool changed = false;
            while (killsPerStack > 0 && _killCount >= killsPerStack)
            {
                _killCount -= killsPerStack;
                _stacks++;
                changed = true;
            }
            if (changed) RefreshModifiers();
        }

        public override void OnWaveStart(int waveNumber)
        {
            _killCount = 0;
            _stacks = 0;
            RemoveModifiers();
        }

        private void RefreshModifiers()
        {
            RemoveModifiers();
            if (_stacks <= 0) return;

            float atkPerStack = GetCurrentValue();
            float asPerStack = atkPerStack * GetParameter("AttackSpeedPerAttackDamage");
            float critDmgPerStack = atkPerStack * GetParameter("CriticalDamagePerAttackDamage");

            var stats = new[]
            {
                (SkillType.AttackDamage, ModifierMode.Percent, _stacks * atkPerStack),
                (SkillType.AttackSpeed, ModifierMode.Percent, _stacks * asPerStack),
                (SkillType.CriticalDamage, ModifierMode.Percent, _stacks * critDmgPerStack)
            };

            foreach (var (stat, mode, value) in stats)
            {
                var modifier = new StatModifier
                {
                    Id = $"{ModifierPrefix}_{stat}",
                    Source = ModifierSource.Card,
                    Stat = stat,
                    Mode = mode,
                    Value = value,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        private void RemoveModifiers()
        {
            ModifierManager.Instance.RemoveModifier($"{ModifierPrefix}_AttackDamage");
            ModifierManager.Instance.RemoveModifier($"{ModifierPrefix}_AttackSpeed");
            ModifierManager.Instance.RemoveModifier($"{ModifierPrefix}_CriticalDamage");
        }

        public int GetStacks() => _stacks;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.ApocalypseEngine;

        public CardHUDData GetHUDData()
        {
            if (_stacks <= 0) return new CardHUDData(null, string.Empty);
            return new CardHUDData(GetCardIcon(), _stacks.ToString());
        }
    }
}