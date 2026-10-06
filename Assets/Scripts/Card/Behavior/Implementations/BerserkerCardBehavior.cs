using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;

using PlayerClass = IdleDefenseSurvival.Player.Player;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BerserkerCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.Berserker;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerDamaged, CardEventType.OnPlayerHealed };

        private float _maxPercent;
        private bool _subscribed;
        private const string ModifierId = "Card:BerserkerDynamic";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _maxPercent = state.CurrentValue;
            EnsureSubscription();
            UpdateModifier();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            Cleanup();
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            _maxPercent = state.CurrentValue;
            UpdateModifier();
        }

        public override void OnPlayerDamaged(float damage, float currentHp, float maxHp)
        {
            UpdateModifier();
        }

        public override void OnPlayerHealed(float healAmount, float currentHp, float maxHp)
        {
            UpdateModifier();
        }

        private void EnsureSubscription()
        {
            if (_subscribed) return;
            var player = PlayerClass.Instance;
            if (player != null)
            {
                player.OnHealthChanged += OnHealthChanged;
                _subscribed = true;
            }
        }

        private void OnHealthChanged() => UpdateModifier();

        private void UpdateModifier()
        {
            var player = PlayerClass.Instance;
            if (player == null || _maxPercent <= 0f)
            {
                ModifierManager.Instance.RemoveModifier(ModifierId);
                player?.SetBerserkerEffect(false);
                return;
            }

            float maxHp = player.MaxHealth;
            float currentHp = player.CurrentHealth;
            if (maxHp <= 0f) return;

            float missingPercent = (maxHp - currentHp) / maxHp * 100f;
            float bonusPercent = Mathf.Min(missingPercent, _maxPercent);

            ModifierManager.Instance.RemoveModifier(ModifierId);
            bool isActive = bonusPercent > 0f;

            if (isActive)
            {
                var modifier = new StatModifier
                {
                    Id = ModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackDamage,
                    Mode = ModifierMode.Percent,
                    Value = bonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
            player.SetBerserkerEffect(isActive);
        }

        private void Cleanup()
        {
            if (_subscribed)
            {
                var player = PlayerClass.Instance;
                if (player != null)
                    player.OnHealthChanged -= OnHealthChanged;
                _subscribed = false;
            }
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void Dispose() => Cleanup();
    }
}