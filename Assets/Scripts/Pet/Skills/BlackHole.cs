using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Pet.Skills
{
    /// <summary>
    /// Voidling evolution skill - creates black hole that pulls enemies and deals DoT.
    /// After duration, explodes for massive damage.
    /// Requires Voidling evolution stage 1+.
    /// </summary>
    public class BlackHole : PetSkill
    {
        private const float SKILL_RADIUS = 4.5f;
        private const float PULL_FORCE = 8f;
        private const float DOT_TICK_RATE = 0.5f; // Damage every 0.5s
        private const float SKILL_DURATION = 3f;
        private const float EXPLOSION_MULTIPLIER = 3f; // 300% pet attack

        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public BlackHole()
        {
            SkillId = "black_hole";
            BaseCooldown = 25f;
            BaseDamageMultiplier = 0.6f; // 60% pet attack per tick (DoT)
        }

        protected override bool CheckCustomConditions()
        {
            // Requires evolution stage 1+
            if (Pet.EvolutionStage < 1) return false;

            // Find enemy cluster
            var player = Player.Player.Instance;
            if (player == null) return false;

            // Cast if there are multiple enemies nearby
            Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, SKILL_RADIUS * 1.5f, EnemyLayerMask);
            int enemyCount = 0;
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var enemy) && enemy.CurrentHealth > 0)
                    enemyCount++;
            }

            return enemyCount >= 5; // Cast if 5+ enemies present
        }

        protected override void OnExecute()
        {
            var player = Player.Player.Instance;
            if (player == null) return;

            Vector3 centerPosition = player.transform.position;

            // Start black hole coroutine
            PetManager.Instance?.StartCoroutine(BlackHoleRoutine(centerPosition));
        }

        private IEnumerator BlackHoleRoutine(Vector3 centerPosition)
        {
            float elapsed = 0f;
            float nextDotTick = 0f;

            HashSet<EnemyAi> affectedEnemies = new HashSet<EnemyAi>();

            // TODO: Spawn black hole visual effect at centerPosition

            while (elapsed < SKILL_DURATION)
            {
                elapsed += Time.deltaTime;
                nextDotTick += Time.deltaTime;

                // Find enemies in radius
                Collider2D[] hits = Physics2D.OverlapCircleAll(centerPosition, SKILL_RADIUS, EnemyLayerMask);

                foreach (var hit in hits)
                {
                    if (!hit.TryGetComponent<EnemyAi>(out var enemy) || enemy.CurrentHealth <= 0)
                        continue;

                    affectedEnemies.Add(enemy);

                    // Apply pull force toward center
                    Vector2 directionToCenter = (centerPosition - enemy.transform.position).normalized;

                    // Reduce pull force for bosses
                    float pullMultiplier = enemy.Role == Role.BOSS ? 0.3f : 1f;

                    if (enemy.TryGetComponent<Rigidbody2D>(out var rb))
                    {
                        rb.AddForce(PULL_FORCE * pullMultiplier * Time.deltaTime * directionToCenter, ForceMode2D.Impulse);
                    }
                }

                // DoT tick
                if (nextDotTick >= DOT_TICK_RATE)
                {
                    nextDotTick = 0f;
                    float tickDamage = CalculateDamage();

                    foreach (var enemy in affectedEnemies)
                    {
                        if (enemy == null || enemy.CurrentHealth <= 0) continue;

                        var damageData = new DamageData(
                            damage: tickDamage,
                            type: DamageType.Normal,
                            crit: CriticalType.None,
                            source: "Pet_BlackHole_DoT"
                        )
                        {
                            Element = Element.Wind
                        };

                        enemy.TakeDamage(damageData);
                    }
                }

                yield return null;
            }

            // Final explosion
            float explosionDamage = Pet.GetAttack() * EXPLOSION_MULTIPLIER;

            Collider2D[] explosionHits = Physics2D.OverlapCircleAll(centerPosition, SKILL_RADIUS, EnemyLayerMask);

            foreach (var hit in explosionHits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var enemy) && enemy.CurrentHealth > 0)
                {
                    var damageData = new DamageData(
                        damage: explosionDamage,
                        type: DamageType.Normal,
                        crit: CriticalType.None,
                        source: "Pet_BlackHole_Explosion"
                    )
                    {
                        Element = Element.Wind
                    };

                    enemy.TakeDamage(damageData);
                }
            }

            // TODO: Spawn explosion visual effect
            // TODO: Play explosion sound

            Debug.Log($"[BlackHole] Exploded, hit {explosionHits.Length} enemies");
        }
    }
}
