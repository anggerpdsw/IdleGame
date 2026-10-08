using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class MomentumCoreCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.MomentumCore;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        private int _currentStack;
        private float _lastKillTime;
        private const string ModifierId = "Card:MomentumCore";

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
            float window = GetParameter("StackDurationSeconds");

            if (_currentStack > 0 && now - _lastKillTime >= window)
                _currentStack = 0;

            _lastKillTime = now;
            _currentStack = Mathf.Min(
                _currentStack + count,
                Mathf.RoundToInt(GetParameter("MaximumStacks")));
            RefreshModifier();
        }

        public override void Update(float deltaTime)
        {
            if (_currentStack <= 0) return;
            float window = GetParameter("StackDurationSeconds");
            if (Time.time - _lastKillTime >= window)
            {
                _currentStack = 0;
                RefreshModifier();
            }
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
                Stat = SkillType.AttackSpeed,
                Mode = ModifierMode.Percent,
                Value = totalBonus,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }

        public int GetStackCount() => _currentStack;

        public CardHUDData GetHUDData()
        {
            if (_currentStack > 0)
            {
                float elapsed = Time.time - _lastKillTime;
                float window = GetParameter("StackDurationSeconds");
                float remaining = Mathf.Max(0f, window - elapsed);
                float fill = window > 0f ? remaining / window : 1f;
                return new CardHUDData(GetCardIcon(), $"x{_currentStack}", fill);
            }
            return new CardHUDData(GetCardIcon(), string.Empty, 1f);
        }
    }
}
