using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class DeathChainCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.DeathChain;

        private int _currentStack;
        private float _lastKillTime;
        private const string ModifierId = "Card:DeathChain";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _currentStack = 0;
            _lastKillTime = 0f;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _currentStack = 0;
            _lastKillTime = 0f;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (damageSource != DamageSource.Player.ToString()) return;

            float now = Time.time;
            if (_currentStack > 0 && now - _lastKillTime >= GetParameter("WindowSeconds"))
                _currentStack = 0;

            _lastKillTime = now;
            _currentStack = Mathf.Min(
                _currentStack + count,
                Mathf.RoundToInt(GetParameter("MaxStacks")));
            RefreshModifier();
        }

        public override void Update(float deltaTime)
        {
            if (_currentStack <= 0 || Time.time - _lastKillTime < GetParameter("WindowSeconds")) return;
            _currentStack = 0;
            RefreshModifier();
        }

        private void RefreshModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);
            if (_currentStack <= 0) return;

            float percentPerStack = GetCurrentValue();
            float totalBonus = _currentStack * percentPerStack;

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

        public int GetStackCount() => _currentStack;
    }
}