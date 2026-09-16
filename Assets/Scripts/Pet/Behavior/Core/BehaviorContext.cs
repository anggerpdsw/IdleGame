using UnityEngine;
using IdleDefenseSurvival.Enemy;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Concrete implementation of behavior context.
    /// Created once per pet update and passed through behavior evaluation pipeline.
    /// Caches frequently accessed values to avoid repeated queries.
    /// </summary>
    public class BehaviorContext : IBehaviorContext
    {
        public PetRuntime Pet { get; private set; }
        public Transform PetTransform { get; private set; }
        public Vector3 PetPosition { get; private set; }

        public Player.Player Player { get; private set; }
        public Transform PlayerTransform { get; private set; }
        public Vector3 PlayerPosition { get; private set; }
        public float PlayerHealthPercent { get; private set; }

        public float BattleTime { get; private set; }
        public int AliveEnemyCount { get; private set; }
        public int EliteCount { get; private set; }
        public int BossCount { get; private set; }
        public bool IsEmergencyMode { get; private set; }

        public Transform CurrentTarget { get; private set; }
        public bool HasValidTarget { get; private set; }

        private static int _cachedEnemyCount;
        private static int _cachedEliteCount;
        private static int _cachedBossCount;
        private static float _lastEnemyCountUpdate;
        private const float ENEMY_COUNT_CACHE_DURATION = 0.2f; // Cache for 0.2s

        public static BehaviorContext Create(PetRuntime pet, Player.Player player, float battleTime)
        {
            var context = new BehaviorContext
            {
                Pet = pet,
                PetTransform = pet.Transform,
                PetPosition = pet.Position,
                Player = player,
                PlayerTransform = player?.transform,
                BattleTime = battleTime,
                IsEmergencyMode = pet.IsEmergencyMode,
                CurrentTarget = pet.Target
            };

            if (player != null)
            {
                context.PlayerPosition = player.transform.position;
                context.PlayerHealthPercent = player.CurrentHealth / player.MaxHealth;
            }

            context.HasValidTarget = pet.IsTargetValid();

            // Cache enemy counts across all pets for performance
            float now = Time.time;
            if (now - _lastEnemyCountUpdate > ENEMY_COUNT_CACHE_DURATION)
            {
                CountEnemies(out _cachedEnemyCount, out _cachedEliteCount, out _cachedBossCount);
                _lastEnemyCountUpdate = now;
            }

            context.AliveEnemyCount = _cachedEnemyCount;
            context.EliteCount = _cachedEliteCount;
            context.BossCount = _cachedBossCount;

            return context;
        }

        private static void CountEnemies(out int total, out int elites, out int bosses)
        {
            total = 0;
            elites = 0;
            bosses = 0;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemyObj in enemies)
            {
                if (!enemyObj.activeInHierarchy) continue;
                if (!enemyObj.TryGetComponent<EnemyAi>(out var enemy)) continue;
                if (enemy.CurrentHealth <= 0) continue;

                total++;
                if (enemy.Role == Role.BOSS) bosses++;
                // Note: No Elite role in current enum - reserved for future
            }
        }
    }
}
