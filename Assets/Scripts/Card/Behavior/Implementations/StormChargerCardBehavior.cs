using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class StormChargerCardBehavior : CardBehaviorBase, ICardHUDProvider
    {
        public override CardEffectType EffectType => CardEffectType.StormCharger;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerAttack };

        private int _attackCount;
        private int _chargePerAttack;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attackCount = 0;
            _chargePerAttack = Mathf.Max(1, Mathf.RoundToInt(GetCurrentValue()));
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _attackCount = 0;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            _chargePerAttack = Mathf.Max(1, Mathf.RoundToInt(GetCurrentValue()));
        }

        public override void OnPlayerAttack()
        {
            _attackCount++;
            if (_attackCount >= _chargePerAttack)
            {
                TriggerStorm();
                _attackCount = 0;
            }
        }

        private void TriggerStorm()
        {
            var player = Player.Player.Instance;
            if (player == null) return;

            var playerPos = player.transform.position;
            float range = GetParameter("ChainRange", 4f);
            var enemies = Physics2D.OverlapCircleAll(playerPos, range, LayerMask.GetMask("Enemy"));

            int targetCount = Mathf.Min(enemies.Length, Mathf.RoundToInt(GetParameter("MaximumTargets", 3f)));
            float damageMultiplier = GetParameter("DamageMultiplier", 1.5f);
            float baseDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage);
            float damage = baseDamage * damageMultiplier;

            for (int i = 0; i < targetCount; i++)
            {
                if (enemies[i].TryGetComponent<EnemyAi>(out var enemy))
                {
                    var damageData = new DamageData(
                        damage,
                        DamageType.Normal,
                        CriticalType.None,
                        DamageSource.Player.ToString());
                    enemy.TakeDamage(damageData);
                }
            }
        }

        CardEffectType ICardHUDProvider.EffectType => CardEffectType.StormCharger;

        public CardHUDData GetHUDData()
        {
            int remaining = _chargePerAttack - _attackCount;
            if (remaining <= 0) return new CardHUDData(null, string.Empty);
            return new CardHUDData(GetCardIcon(), remaining.ToString());
        }
    }
}
