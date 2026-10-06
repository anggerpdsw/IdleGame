using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class WarMachineCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.WarMachine;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        private float _continuousAttackStartTime;
        private float _lastAttackTime = float.NegativeInfinity;
        private bool _active;
        private const string ModifierPrefix = "Card:WarMachine";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _continuousAttackStartTime = 0f;
            _lastAttackTime = float.NegativeInfinity;
            _active = false;
            RemoveModifiers();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            RemoveModifiers();
        }

        public override void OnPlayerAttack()
        {
            float now = Time.time;
            float idleThreshold = GetParameter("IdleResetSeconds");
            if (now - _lastAttackTime > idleThreshold)
            {
                _continuousAttackStartTime = now;
                if (_active)
                {
                    _active = false;
                    RefreshModifiers();
                }
            }
            _lastAttackTime = now;

            float threshold = GetParameter("ContinuitySeconds");
            bool shouldBeActive = now - _continuousAttackStartTime >= threshold;
            if (shouldBeActive != _active)
            {
                _active = shouldBeActive;
                RefreshModifiers();
            }
        }

        public override void Update(float deltaTime)
        {
            if (_active && Time.time - _lastAttackTime > GetParameter("IdleResetSeconds"))
            {
                _active = false;
                _continuousAttackStartTime = 0f;
                _lastAttackTime = float.NegativeInfinity;
                RefreshModifiers();
            }
        }

        private void RefreshModifiers()
        {
            RemoveModifiers();
            if (!_active) return;

            float bonusPercent = GetCurrentValue();

            var stats = new[]
            {
                (SkillType.AttackDamage, ModifierMode.Percent, bonusPercent),
                (SkillType.AttackSpeed, ModifierMode.Percent, bonusPercent),
                (SkillType.CriticalChance, ModifierMode.Percent, bonusPercent)
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
            ModifierManager.Instance.RemoveModifier($"{ModifierPrefix}_CriticalChance");
        }

        public bool IsActive() => _active;
    }
}