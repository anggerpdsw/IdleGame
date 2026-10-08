using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class GuardianInstinctCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.GuardianInstinct;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerDamaged };

        private float _cooldownRemaining;
        private float _activeRemaining;
        private bool _active;
        private const string ShieldModifierId = "Card:GuardianInstinct_Shield";
        private const string EvasionModifierId = "Card:GuardianInstinct_Evasion";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _cooldownRemaining = 0f;
            _activeRemaining = 0f;
            _active = false;
            RemoveModifiers();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _cooldownRemaining = 0f;
            _activeRemaining = 0f;
            _active = false;
            RemoveModifiers();
        }

        public override void OnPlayerDamaged(float damage, float currentHp, float maxHp)
        {
            if (_cooldownRemaining > 0f) return;
            if (currentHp > maxHp * GetParameter("HealthThresholdPercent") * 0.01f) return;

            Trigger();
        }

        public bool CanTrigger()
        {
            return _cooldownRemaining <= 0f && !IsActive();
        }

        public bool Trigger()
        {
            if (!CanTrigger()) return false;

            var player = PlayerClass.Instance;
            if (player == null) return false;

            float shieldAmount = player.MaxHealth * GetCurrentValue() * 0.01f;
            player.GrantGuardianShield(shieldAmount);

            float evasionPercent = GetParameter("EvasionBonusPercent");

            var evasionModifier = new StatModifier
            {
                Id = EvasionModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.Evasion,
                Mode = ModifierMode.Flat,
                Value = evasionPercent,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(evasionModifier);

            _cooldownRemaining = GetParameter("CooldownSeconds");
            _activeRemaining = GetParameter("DurationSeconds");
            _active = true;

            return true;
        }

        public override void Update(float deltaTime)
        {
            if (IsActive())
            {
                _activeRemaining -= deltaTime;
                if (_activeRemaining <= 0f)
                {
                    _active = false;
                    _activeRemaining = 0f;
                    ModifierManager.Instance.RemoveModifier(EvasionModifierId);
                    PlayerClass.Instance?.GrantGuardianShield(0f);
                }
            }

            if (_cooldownRemaining > 0f)
                _cooldownRemaining -= deltaTime;
        }

        private void RemoveModifiers()
        {
            ModifierManager.Instance.RemoveModifier(ShieldModifierId);
            ModifierManager.Instance.RemoveModifier(EvasionModifierId);
            PlayerClass.Instance?.GrantGuardianShield(0f);
        }

        public float GetCooldownRemaining() => Mathf.Max(_cooldownRemaining, 0f);
        public bool IsActive() => _active;

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.GuardianInstinct;

        public CardHUDData GetHUDData()
        {
            // Active (shield) state: show remaining active time, fill drains as duration expires
            if (IsActive())
            {
                float maxDuration = GetParameter("DurationSeconds");
                float fillAmount = maxDuration > 0f ? _activeRemaining / maxDuration : 1f;
                return new CardHUDData(GetCardIcon(), $"{_activeRemaining:F1}s", fillAmount);
            }

            // Cooldown state: show remaining cooldown, fill grows as cooldown completes
            if (_cooldownRemaining > 0f)
            {
                float maxCooldown = GetParameter("CooldownSeconds");
                float fillAmount = maxCooldown > 0f ? 1f - (_cooldownRemaining / maxCooldown) : 1f;
                return new CardHUDData(GetCardIcon(), $"{_cooldownRemaining:F1}s", fillAmount);
            }

            // Ready state: icon shown, no text, full fill
            return new CardHUDData(GetCardIcon(), string.Empty, 1f);
        }
    }
}