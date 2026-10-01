using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Stats;
using System.Linq;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class EchoShotCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.EchoShot;

        private int _attackCount;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attackCount = 0;
        }

        public override void OnPlayerAttack()
        {
            _attackCount++;
            int threshold = Mathf.RoundToInt(GetCurrentValue());
            if (_attackCount >= threshold)
            {
                FireEcho();
                _attackCount = 0;
            }
        }

        private void FireEcho()
        {
            var player = Player.Player.Instance;
            if (player == null) return;

            float damageMultiplier = GetParameter("EchoDamageMultiplier", 0.65f);

            // Find nearest enemy to target echo
            float range = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, range, LayerMask.GetMask("Enemy"));
            if (hits.Length == 0) return;

            Transform target = hits
                .OrderBy(h => Vector2.Distance(player.transform.position, h.transform.position))
                .First().transform;

            Projectile projectile = ProjectilePool.Instance.Get();
            if (projectile != null)
            {
                projectile.transform.position = player.transform.position;
                projectile.Initialize(target, player, damageMultiplier, false);
            }
        }
    }
}
