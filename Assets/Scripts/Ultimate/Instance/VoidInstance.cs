using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Void ultimate: pulls enemies to center, applies stunt + permanent slow,
    /// then teleports affected enemies. Repeats for multiple phases (default 3),
    /// each at a new random position within attack range.
    /// </summary>
    public class VoidInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Void.ToString();

        [Header("Damage")]
        [Tooltip("Radius of the void effect")]
        [SerializeField] private float _voidRadius = 1.35f;

        [Header("Tether Settings")]
        [Tooltip("Speed at which enemies are pulled toward void center")]
        [SerializeField] private float _pullSpeed = 3f;
        [Tooltip("Distance from void center where enemies stop being pulled")]
        [SerializeField] private float _stopDistance = 0.05f;

        [Header("Teleport Settings")]
        [Tooltip("Small spread around the selected teleport point to avoid perfect enemy overlap")]
        [SerializeField] private float _teleportSpreadRadius = 0.4f;

        [Header("Phase Settings")]
        [Tooltip("Maximum number of void phases")]
        [SerializeField] private int _maxVoidCount = 3;

        [Header("Visual/Audio")]
        [Tooltip("Rotation speed of the void effect (degrees per second)")]
        [SerializeField] private float _rotationSpeed = 350f;
        [Tooltip("Particle effect prefab for the void")]
        [SerializeField] private GameObject _voidEffect;
        [Tooltip("Audio clip for void sound")]
        [SerializeField] private AudioClip _voidSoundClip;
        [SerializeField] private float _soundVolume = 1f;
        [SerializeField] private float _zoomSize = 1f;

        // Runtime references
        private Player.Player _player;
        private int _enemyLayerMask;
        private bool _isActive = true;

        private DamageData damageData;
        private float _voidDuration;

        // Phase tracking
        private int _currentVoidCount = 1;
        private float _phaseTimer;

        // Enemies currently tethered to void center (move toward center each frame)
        private readonly List<EnemyAi> _tetheredEnemies = new();

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            transform.localScale = new Vector3(_zoomSize, _zoomSize, 1f);
        }

        public void Initialize(Player.Player player, UltimateData voidData)
        {
            _player = player;

            _voidDuration = voidData.GetDuration();
            _phaseTimer = _voidDuration;

            damageData = new(0f, DamageType.Normal, CriticalType.None, UltimateID)
            {
                Element = voidData.GetElement(),
                SlowPercent = voidData.GetSlowPercent(),
                StuntMultiplier = voidData.GetStuntMultiplier(),
                HealthBreak = voidData.GetHealthBreak(),
            };

            SpawnVoidEffects();
        }

        private void Update()
        {
            if (!_isActive) return;

            _phaseTimer -= Time.deltaTime;

            if (_phaseTimer <= 0f)
            {
                NextPhase();
                return;
            }

            // Rotate visual
            transform.Rotate(0, 0, _rotationSpeed * Time.unscaledDeltaTime);

            // Each frame: detect new enemies, pull all tethered enemies, apply no-damage tick
            ApplyFrameEffects();
        }

        // -------------------------------------------------------------------
        // Phase handling
        // -------------------------------------------------------------------

        /// <summary>
        /// Called when a phase ends: teleport tethered enemies, then either move to next phase or destroy.
        /// </summary>
        private void NextPhase()
        {
            // Teleport enemies affected by this phase
            TeleportAffectedEnemies();

            _tetheredEnemies.Clear();

            // If more phases remaining, move to new position and continue
            if (_currentVoidCount < _maxVoidCount)
            {
                _currentVoidCount++;
                MoveToRandomPosition();
                _phaseTimer = _voidDuration;
                SpawnVoidEffects();
                return;
            }

            // All phases complete
            _isActive = false;
            UltimateFactory.OnInstanceDestroyed(UltimateID);
            Destroy(gameObject);
        }

        /// <summary>
        /// Move void to a new random position in player's attack range.
        /// </summary>
        private void MoveToRandomPosition()
        {
            if (_player == null) return;

            float attackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(attackRange, attackRange + 2f);

            Vector2 newPos = (Vector2)_player.transform.position +
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            transform.position = newPos;
        }

        // -------------------------------------------------------------------
        // Core logic
        // -------------------------------------------------------------------

        /// <summary>
        /// Per-frame: detect enemies in radius, pull tethered enemies to center.
        /// </summary>
        private void ApplyFrameEffects()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _voidRadius, _enemyLayerMask);

            foreach (Collider2D col in hits)
            {
                if (!col.TryGetComponent(out EnemyAi enemy)) continue;

                // First time enemy enters void: apply permanent slow + stunt + reduce max health by 10%
                if (!_tetheredEnemies.Contains(enemy))
                {
                    enemy.ApplyStunt(damageData.StuntMultiplier);
                    enemy.ApplySlow(SlowSource.Void, SlowType.Permanent, damageData.SlowPercent);
                    enemy.ReduceMaxHealth(damageData.HealthBreak);
                    _tetheredEnemies.Add(enemy);
                }

                // Pull enemy toward void center
                float dist = Vector2.Distance(enemy.transform.position, transform.position);
                if (dist > _stopDistance)
                {
                    enemy.transform.position = Vector2.MoveTowards(
                        enemy.transform.position,
                        transform.position,
                        _pullSpeed * Time.deltaTime
                    );
                }
                else
                {
                    enemy.transform.position = transform.position;
                }
            }
        }

// -------------------------------------------------------------------
        // Expiry / Teleport
        // -------------------------------------------------------------------

        /// <summary>
        /// Move all tethered enemies to a single random spot at attackRange + buffer from player.
        /// Enemies are spread around that spot with slight jitter to avoid stacking.
        /// </summary>
        private void TeleportAffectedEnemies()
        {
            if (_player == null || _tetheredEnemies.Count == 0) return;

            float attackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);

            // Random position outside the player's attack range, plus configured buffer
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = attackRange + WaveManager.Instance.SpawnBuffer;

            Vector2 centerPos = (Vector2)_player.transform.position +
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            // Spread enemies around the center position with small jitter
            for (int i = 0; i < _tetheredEnemies.Count; i++)
            {
                EnemyAi enemy = _tetheredEnemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

                // Small random offset per enemy to avoid perfect overlap
                float offsetX = Random.Range(-_teleportSpreadRadius, _teleportSpreadRadius);
                float offsetY = Random.Range(-_teleportSpreadRadius, _teleportSpreadRadius);

                Vector2 newPos = centerPos + new Vector2(offsetX, offsetY);
                enemy.transform.position = (Vector3)newPos;

                // Set facing to face player after teleport
                // Jika posisi enemy > player, enemy harus menghadap kiri (faceLeft = true)
                bool shouldFaceLeft = newPos.x > _player.transform.position.x;
                enemy.SetFacing(shouldFaceLeft);
            }
        }

        // -------------------------------------------------------------------
        // Visual
        // -------------------------------------------------------------------

        private void SpawnVoidEffects()
        {
            if (_voidEffect != null) _voidEffect.SetActive(true);
            Utilityku.PlaySfx(_player.SfxSource, _voidSoundClip, _soundVolume);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GameColors.debugLightningPurple.WithAlpha(0.3f); // Purple for void
            Gizmos.DrawWireSphere(transform.position, _voidRadius);
        }
#endif
    }
}
