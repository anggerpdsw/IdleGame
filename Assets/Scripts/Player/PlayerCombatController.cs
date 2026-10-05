using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Card.Behavior;
using IdleDefenseSurvival.Card.Behavior.Implementations;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerCombatController : MonoBehaviour
    {
        private Player _player;
        private float _attackTimer;
        private float _spacingRadius;
        private float _spreadAngle;
        private int _enemyMask;
        private int _shotIndex;

        public Transform CurrentTarget { get; private set; }
        public bool NextAttackGuaranteedHit { get; private set; }
        public float NextAttackDamageMultiplier { get; private set; } = 1f;
        public float NextAttackCriticalChanceBonus { get; private set; }

        public void Configure(Player player, float spacingRadius, float spreadAngle)
        {
            _player = player;
            _spacingRadius = spacingRadius;
            _spreadAngle = spreadAngle;
            _enemyMask = LayerMask.GetMask("Enemy");
        }

        public void Initialize() => _attackTimer = 0f;

        public void Tick()
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;

            Attack();
            float speed = PlayerStatsManager.Instance.GetStat(SkillType.AttackSpeed);
            _attackTimer = speed > 0f ? 1f / speed : float.MaxValue;
        }

        public void SetNextProjectileAsLastBullet(bool guaranteed)
        {
            if (CardRuntimeManager.Instance?.GetBehavior("last_bullet")
                is not LastBulletCardBehavior behavior) return;

            NextAttackGuaranteedHit = guaranteed;
            NextAttackDamageMultiplier = behavior.GetDamageMultiplier();
            NextAttackCriticalChanceBonus = behavior.GetCriticalChanceBonus();
        }

        private void Attack()
        {
            List<Transform> targets = FindTargets();
            if (targets.Count == 0) return;

            CardRuntimeManager.Instance?.DispatchPlayerAttack();

            bool multi = Utilityku.Chance(
                PlayerStatsManager.Instance.GetStat(SkillType.MultiShootChance));

            int count = GetShotCount(targets.Count, multi);
            for (int i = 0; i < count; i++)
                Fire(targets[i], i, multi);

            NextAttackGuaranteedHit = false;
            NextAttackDamageMultiplier = 1f;
            NextAttackCriticalChanceBonus = 0f;
        }

        private int GetShotCount(int available, bool multi)
        {
            if (!multi) return 1;

            float raw = 1f + PlayerStatsManager.Instance.GetStat(SkillType.MultiShootCount);
            int accumulated = PlayerStatsManager.Instance.GetAccumulatedCount(
                raw, AccumulatedCountType.Multi);

            int wanted = Mathf.Min(accumulated, available);
            int mana = Mathf.FloorToInt(_player.CurrentMana);

            return mana < wanted ? (mana < 1 ? 1 : mana) : wanted;
        }

        private List<Transform> FindTargets()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                PlayerStatsManager.Instance.GetStat(SkillType.AttackRange),
                _enemyMask);

            return hits
                .Where(x => x.TryGetComponent<EnemyAi>(out _))
                .OrderBy(x => (x.transform.position - transform.position).sqrMagnitude)
                .Select(x => x.transform)
                .ToList();
        }

        private void Fire(Transform target, int index, bool multi)
        {
            Projectile projectile = ProjectilePool.Instance.Get();
            if (projectile == null) return;

            CurrentTarget = target;
            projectile.transform.SetPositionAndRotation(
                GetSpawnPosition(index), Quaternion.identity);

            projectile.Initialize(
                target, _player, index == 0 ? 1f : .77f, multi);
        }

        public void SpawnBulletStormBurst()
        {
            int count = Mathf.Max(0, Mathf.RoundToInt(
                CardModifierService.GetCardParameter(
                    "bullet_storm", "AdditionalProjectiles")));

            if (count <= 0) return;

            float multiplier = CardModifierService.GetCardParameter(
                "bullet_storm", "ProjectileDamageMultiplier");

            foreach (Transform target in FindTargets().Take(count))
                FireSpecial(target, multiplier, false);
        }

        public void SpawnInfiniteArsenalProjectile()
        {
            Transform target = FindTargets().FirstOrDefault();
            if (target == null) return;

            float multiplier = CardModifierService.GetCardParameter(
                "infinite_arsenal", "ProjectileDamageMultiplier");

            FireSpecial(target, multiplier, true);
        }

        private void FireSpecial(Transform target, float multiplier, bool infinite)
        {
            Projectile projectile = ProjectilePool.Instance.Get();
            if (projectile == null) return;

            projectile.transform.SetPositionAndRotation(
                infinite ? transform.position : GetSpawnPosition(0),
                Quaternion.identity);

            projectile.Initialize(target, _player, multiplier, false, infinite);
        }

        private Vector3 GetSpawnPosition(int index)
        {
            float angle = (index * _spreadAngle + _shotIndex * 7f) * Mathf.Deg2Rad;
            _shotIndex++;

            return transform.position + new Vector3(
                Mathf.Cos(angle) * _spacingRadius,
                Mathf.Sin(angle) * _spacingRadius,
                0f);
        }
    }
}