using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerUltimateController : MonoBehaviour
    {
        private readonly List<TankInstance> _activeTanks = new();

        private Player _player;
        private UltimateManager _manager;

        public void Configure(Player player) => _player = player;
        public void Initialize() => _manager = UltimateManager.Instance;

        public void Tick()
        {
            if (_manager == null) return;

            Vector3 pos = transform.position;
            _manager.TrySpawn(DamageSource.Void.ToString(), pos, _player);
            _manager.TrySpawn(DamageSource.Root.ToString(), pos, _player);
            _manager.TrySpawn(DamageSource.Fountain.ToString(), pos, _player);
            _manager.TrySpawn(DamageSource.Shockwave.ToString(), pos, _player);
        }

        public bool ManualCastUltimate(string id)
        {
            return _manager != null &&
                   !string.IsNullOrEmpty(id) &&
                   _manager.TryCastAllReadyStacks(id, _player);
        }

        public void SpawnTank()
        {
            if (_manager == null) return;

            string id = DamageSource.Tank.ToString();
            if (!_manager.TryGetUltimate(id, out _)) return;

            _activeTanks.RemoveAll(x => x == null);

            if (TryGetTankSpawnPosition(out Vector3 position))
                _manager.TryGenerateStack(id, _player, position);
        }

        public bool TryGetTankSpawnPosition(out Vector3 position)
        {
            position = default;
            if (PlayerStatsManager.Instance == null) return false;

            float playerRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            if (playerRange <= 0f) return false;

            float tankRange = playerRange * .75f;

            for (int i = 0; i < 20; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                Vector2 candidate =
                    (Vector2)transform.position + direction * playerRange;

                bool valid = true;
                foreach (TankInstance tank in _activeTanks)
                {
                    if (tank == null) continue;

                    float distance = Vector2.Distance(
                        candidate, tank.transform.position);

                    if (distance < tankRange + tank.TankAttackRange)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                position = candidate;
                return true;
            }

            return false;
        }
    }
}