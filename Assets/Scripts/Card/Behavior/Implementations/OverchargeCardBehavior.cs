using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class OverchargeCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.Overcharge;
        public override CardEventType[] SubscribedEvents => System.Array.Empty<CardEventType>();

        private const string AttackSpeedModifierId = "Card:Overcharge_AttackSpeed";
        private const string AttackDamageModifierId = "Card:Overcharge_AttackDamage";

        private float _highManaThresholdPercent;
        private float _lowManaThresholdPercent;
        private float _attackSpeedBonusPercent;
        private float _attackDamageBonusPercent;
        private float _durationSeconds;

        private bool _wasHighMana = false;
        private bool _lowManaEffectActive = false;
        private float _lowManaEffectTimer = 0f;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _highManaThresholdPercent = GetParameter("HighManaThresholdPercent", 80f);
            _lowManaThresholdPercent = GetParameter("LowManaThresholdPercent", 20f);
            _attackSpeedBonusPercent = GetCurrentValue();
            _attackDamageBonusPercent = _attackSpeedBonusPercent * GetParameter("AttackDamageBonusPercent", 1.91f);
            _durationSeconds = GetParameter("DurationSeconds", 4f);
        }

        public override void Update(float deltaTime)
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            float currentMana = player.CurrentMana;
            float maxMana = player.MaxMana;
            if (maxMana <= 0f) return;

            float manaPercent = currentMana / maxMana * 100f;

            // High mana: grant attack speed
            bool isHighMana = manaPercent >= _highManaThresholdPercent;
            bool isLowMana = manaPercent <= _lowManaThresholdPercent;

            // Handle high mana attack speed bonus
            if (isHighMana != _wasHighMana)
            {
                _wasHighMana = isHighMana;
                UpdateAttackSpeedModifier(isHighMana);
            }

            // Handle low mana: convert charge to attack damage bonus
            if (isLowMana && _wasHighMana && !_lowManaEffectActive)
            {
                ActivateLowManaEffect();
            }

            if (_lowManaEffectActive)
            {
                _lowManaEffectTimer -= deltaTime;
                if (_lowManaEffectTimer <= 0f)
                {
                    DeactivateLowManaEffect();
                }
            }
        }

        private void UpdateAttackSpeedModifier(bool active)
        {
            ModifierManager.Instance.RemoveModifier(AttackSpeedModifierId);

            if (active && _attackSpeedBonusPercent > 0f)
            {
                var modifier = new StatModifier
                {
                    Id = AttackSpeedModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackSpeed,
                    Mode = ModifierMode.Percent,
                    Value = _attackSpeedBonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        private void ActivateLowManaEffect()
        {
            _lowManaEffectActive = true;
            _lowManaEffectTimer = _durationSeconds;

            var modifier = new StatModifier
            {
                Id = AttackDamageModifierId,
                Source = ModifierSource.Card,
                Stat = SkillType.AttackDamage,
                Mode = ModifierMode.Percent,
                Value = _attackDamageBonusPercent,
                Permanent = false
            };
            ModifierManager.Instance.AddModifier(modifier);
        }

        private void DeactivateLowManaEffect()
        {
            _lowManaEffectActive = false;
            ModifierManager.Instance.RemoveModifier(AttackDamageModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(AttackSpeedModifierId);
            ModifierManager.Instance.RemoveModifier(AttackDamageModifierId);
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            // Reapply modifiers with new values
            var player = PlayerClass.Instance;
            if (player != null)
            {
                float currentMana = player.CurrentMana;
                float maxMana = player.MaxMana;
                if (maxMana > 0f)
                {
                    float manaPercent = currentMana / maxMana * 100f;
                    bool isHighMana = manaPercent >= _highManaThresholdPercent;
                    UpdateAttackSpeedModifier(isHighMana);

                    if (_lowManaEffectActive)
                    {
                        DeactivateLowManaEffect();
                        ActivateLowManaEffect();
                    }
                }
            }
        }

        public CardHUDData GetHUDData()
        {
            if (_lowManaEffectActive)
            {
                float fill = _durationSeconds > 0f ? _lowManaEffectTimer / _durationSeconds : 1f;
                return new CardHUDData(GetCardIcon(), $"{_lowManaEffectTimer:F1}s", fill);
            }
            if (_wasHighMana)
                return new CardHUDData(GetCardIcon(), "⚡", 1f);
            return new CardHUDData(GetCardIcon(), string.Empty, 1f);
        }
    }
}