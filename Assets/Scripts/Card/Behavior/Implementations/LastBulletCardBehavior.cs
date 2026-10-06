using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class LastBulletCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.LastBullet;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        private int _attackCount;
        private int _bulletInterval;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attackCount = 0;
            _bulletInterval = Mathf.Max(1, Mathf.RoundToInt(GetCurrentValue()));
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _attackCount = 0;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            _bulletInterval = Mathf.Max(1, Mathf.RoundToInt(GetCurrentValue()));
        }

        public override void OnPlayerAttack()
        {
            _attackCount++;
            if (_attackCount >= _bulletInterval)
            {
                MarkLastBullet();
                _attackCount = 0;
            }
        }

        private void MarkLastBullet()
        {
            // Mark next projectile as "last bullet" — store state for Projectile to read
            var player = Player.Player.Instance;
            if (player != null)
                player.SetNextProjectileAsLastBullet(IsGuaranteedHit());
        }

        public float GetDamageMultiplier()
            => GetParameter("DamageMultiplier", 2f);

        public float GetCriticalChanceBonus()
            => GetParameter("CriticalChanceBonus", 25f);

        public bool IsGuaranteedHit()
            => GetParameter("GuaranteedHit", 1f) > 0.5f;
    }
}
