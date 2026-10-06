using UnityEngine;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;
using System.Collections.Generic;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Card;

namespace IdleDefenseSurvival.Player
{
    public class AuraCollider : MonoBehaviour
    {
        [SerializeField] private CircleCollider2D _collider;
        private readonly HashSet<EnemyAi> _enemiesInAura = new();
        private readonly Dictionary<EnemyAi, float> _appliedSlow = new(); // Track per-enemy applied slow
        private float _cachedFrostAura;
        private int _enemyLayerMask;
        private float _validateTimer;
        private const float VALIDATE_INTERVAL = 0.2f;

        private void Awake()
        {
            _collider.enabled = false;
            _collider.isTrigger = true; // Ensure trigger mode for OnTriggerEnter2D/Exit2D
            _enemyLayerMask = LayerMask.GetMask("Enemy");

            var player = GameObject.FindWithTag(DamageSource.Player.ToString());
            if (player != null && transform.parent != player.transform)
            {
                transform.SetParent(player.transform);
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
            }

            RefreshCachedValues();
        }

        private void Start()
        {
            UpdateColliderState();
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged += UpdateColliderState;
        }
  
        private void Update()
        {
            if (!_collider.enabled) return;
            _validateTimer += Time.deltaTime;
            if (_validateTimer >= VALIDATE_INTERVAL)
            {
                _validateTimer = 0f;
                ValidateEnemiesInRange();
            }
        }

        private void ValidateEnemiesInRange()
        {
            float radius = _collider.radius;
            var pos = transform.position;
            var toRemove = new List<EnemyAi>();
            foreach (var enemy in _enemiesInAura)
            {
                if (enemy == null) continue;
                float sqrDist = (enemy.transform.position - pos).sqrMagnitude;
                if (sqrDist > radius * radius)
                {
                    RemoveAuraEffects(enemy);
                    toRemove.Add(enemy);
                }
            }
            foreach (var enemy in toRemove)
                _enemiesInAura.Remove(enemy);
        }

        private void RefreshCachedValues()
            => _cachedFrostAura = Mathf.Max(0f, CardModifierService.GetEffectResult(CardEffectType.FrostAura));

        public void UpdateColliderState()
        {
            if (_collider == null) return;
            float oldRadius = _collider.radius;
            bool anyAuraActive = CardModifierService.HasAuraEffect();

            if (_collider.enabled != anyAuraActive)
                _collider.enabled = anyAuraActive;

            if (anyAuraActive)
            {
                float newRadius = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
                _collider.radius = newRadius;
                if (!Mathf.Approximately(oldRadius, newRadius))
                    ForceRefreshEnemies();
            }
            else
                ClearAllAuraEffects();
        }

        private void ForceRefreshEnemies()
        {
            ClearAllAuraEffects();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, _collider.radius, _enemyLayerMask))
                if (hit.TryGetComponent<EnemyAi>(out var enemy))
                {
                    _enemiesInAura.Add(enemy);
                    ApplyAuraEffects(enemy);
                }
        }

        private void ApplyAuraEffects(EnemyAi enemy)
        {
            if (enemy == null || _cachedFrostAura <= 0f) return;

            // Cap at 80% to prevent complete freeze (defensive ceiling against future card balance changes)
            float finalSlow = Mathf.Min(_cachedFrostAura, 80f);

            // Compare against clamped value to avoid redundant reapplication
            if (_appliedSlow.TryGetValue(enemy, out float current) && Mathf.Approximately(current, finalSlow))
                return;

            enemy.RemoveSlow(SlowSource.Card);
            enemy.ApplySlow(SlowSource.Card, SlowType.Aura, finalSlow);
            _appliedSlow[enemy] = finalSlow;  // Track clamped value, not raw
        }

        private void RemoveAuraEffects(EnemyAi enemy)
        {
            if (enemy == null) return;
            enemy.RemoveSlow(SlowSource.Card);
            _appliedSlow.Remove(enemy);
        }

        public void ClearAllAuraEffects()
        {
            _enemiesInAura.RemoveWhere(e => e == null);
            foreach (var e in _enemiesInAura) RemoveAuraEffects(e);
            _enemiesInAura.Clear();
            _appliedSlow.Clear();
        }

        public void RefreshAllAuraEffects()
        {
            _enemiesInAura.RemoveWhere(e => e == null);
            foreach (var e in _enemiesInAura) { RemoveAuraEffects(e); ApplyAuraEffects(e); }
        }

        private void OnModifierChanged()
        {
            RefreshCachedValues();
            UpdateColliderState();
            if (_collider.enabled) RefreshAllAuraEffects();
        }

        private void OnEnable() => CardModifierService.OnModifierChanged += OnModifierChanged;
        private void OnDisable()
        {
            CardModifierService.OnModifierChanged -= OnModifierChanged;
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged -= UpdateColliderState;
        }
        private void OnDestroy() => ClearAllAuraEffects();
        private static bool TryGetEnemy(Collider2D collider, out EnemyAi enemy)
        {
            enemy = collider.GetComponentInParent<EnemyAi>();
            return enemy != null;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!TryGetEnemy(other, out var enemy)) return;
            if (_enemiesInAura.Add(enemy)) ApplyAuraEffects(enemy);
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            if (!TryGetEnemy(other, out var enemy)) return;
            if (_enemiesInAura.Remove(enemy)) RemoveAuraEffects(enemy);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_collider != null && _collider.enabled)
            {
                Gizmos.color = GameColors.debugBlueGizmo.WithAlpha(0.3f);
                Gizmos.DrawWireSphere(transform.position, _collider.radius);
            }
        }
#endif
    }
}