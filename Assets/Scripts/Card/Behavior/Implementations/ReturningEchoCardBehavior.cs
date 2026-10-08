using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ReturningEchoCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.ReturningEcho;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        private float _triggerDistance;
        private float _returnDamagePercent;
        private float _returnChancePercent;
        private int _maximumReturnTargets;
        private float _returnCooldownSeconds;

        private float _cooldownTimer;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _triggerDistance = GetParameter("TriggerDistance", 8f);
            _returnDamagePercent = GetParameter("ReturnDamagePercent", 50f) / 100f;
            _returnChancePercent = GetParameter("ReturnChancePercent", 20f);
            _maximumReturnTargets = Mathf.RoundToInt(GetParameter("MaximumReturnTargets", 5f));
            _returnCooldownSeconds = GetParameter("ReturnCooldownSeconds", 0.5f);
        }

        public override void OnPlayerAttack()
        {
            // This card works through projectile logic - projectiles track their distance
            // and trigger return effect when they exceed trigger distance
            // The actual return logic is handled in Projectile.cs
        }

        public override void Update(float deltaTime)
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
            }
        }

        public bool CanTriggerReturn()
        {
            return _cooldownTimer <= 0f && Utilityku.Chance(_returnChancePercent);
        }

        public void TriggerReturnCooldown()
        {
            _cooldownTimer = _returnCooldownSeconds;
        }

        public float GetReturnDamageMultiplier(float baseDamage)
        {
            return baseDamage * _returnDamagePercent;
        }

        public int GetMaximumReturnTargets() => _maximumReturnTargets;

        public float GetTriggerDistance() => _triggerDistance;

        public CardHUDData GetHUDData()
        {
            if (_cooldownTimer > 0f)
            {
                float fill = _returnCooldownSeconds > 0f ? 1f - (_cooldownTimer / _returnCooldownSeconds) : 1f;
                return new CardHUDData(GetCardIcon(), $"{_cooldownTimer:F1}s", fill);
            }
            return new CardHUDData(GetCardIcon(), string.Empty, 1f);
        }
    }
}