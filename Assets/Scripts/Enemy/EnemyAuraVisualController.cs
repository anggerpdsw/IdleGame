using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Handles enemy aura visualization (range circle + pulse animation).
    /// Runs independently, no allocations in hot path.
    /// Pulse uses unscaled time to survive pause.
    /// </summary>
    public class EnemyAuraVisualController : MonoBehaviour
    {
        [Header("Aura Range")]
        [Tooltip("Renderer for dashed circle showing aura boundary.")]
        [SerializeField] private SpriteRenderer _auraRangeRenderer;
        [Tooltip("Rotation speed of boundary visual.")]
        [SerializeField] private float _auraRotationSpeed = 2f;
        [Tooltip("Scale multiplier for boundary sprite.")]
        [SerializeField] private float _auraScaleRange = 1f;

        [Header("Aura Pulse")]
        [Tooltip("Renderer for expanding pulse wave.")]
        [SerializeField] private SpriteRenderer _auraPulseRenderer;
        [Tooltip("Duration of one pulse from center to boundary.")]
        [SerializeField] private float _auraPulseDuration = 1.2f;
        [Tooltip("Interval between pulses.")]
        [SerializeField] private float _auraPulseInterval = 0.35f;
        [Tooltip("Maximum alpha of pulse wave.")]
        [SerializeField, Range(0f, 1f)] private float _auraPulseMaxAlpha = 0.45f;
        [Tooltip("Color of pulse wave (set by effect type).")]
        [SerializeField] private Color _auraPulseColor = new(0.1f, 0.55f, 1f, 1f);

        private float _auraPulseTimer;
        private float _auraRadius;
        private bool _hasAura;

        private void Update()
        {
            // Pulse animation runs with unscaled time (survives pause)
            UpdateAuraPulse();

            // Rotate boundary visual
            if (_auraRangeRenderer != null && _auraRangeRenderer.enabled)
                _auraRangeRenderer.transform.Rotate(0, 0, _auraRotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Refresh aura visuals based on enemy effect data.
        /// Reads max radius from all aura effects, sets color based on effect type.
        /// </summary>
        public void RefreshAuraVisual(EnemyData enemyData)
        {
            if (_auraRangeRenderer == null) return;

            _auraRadius = 0f;
            if (enemyData?.effects != null)
            {
                foreach (var ef in enemyData.effects)
                {
                    if (ef?.aura == null) continue;
                    foreach (var act in ef.aura)
                    {
                        if (act == null) continue;
                        _auraRadius = Mathf.Max(_auraRadius, act.radius);

                        _auraPulseColor = act.effect switch
                        {
                            StatusEffectType.Slow => GameColors.rareBlue,
                            StatusEffectType.DamageReduction => GameColors.gemRuby,
                            StatusEffectType.Regeneration => GameColors.green,
                            StatusEffectType.Unregeneration => GameColors.ancientPurple,
                            _ => GameColors.empty,
                        };
                    }
                }
            }

            _hasAura = _auraRadius > 0f;

            // Aura boundary
            _auraRangeRenderer.enabled = _hasAura;
            if (_hasAura)
            {
                float diameter = _auraRadius * 2f;
                float scale = diameter * _auraScaleRange;
                _auraRangeRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                _auraRangeRenderer.color = GameColors.debugAtkRangeCyan.WithAlpha(0.09f);
            }

            if (_auraPulseRenderer != null)
            {
                _auraPulseRenderer.enabled = false;
                _auraPulseTimer = 0f;
            }
        }

        /// <summary>
        /// Update expanding pulse animation.
        /// Uses unscaled time to run during pause.
        /// No coroutines (critical for 2000+ enemy performance).
        /// </summary>
        private void UpdateAuraPulse()
        {
            if (!_hasAura || _auraPulseRenderer == null) return;

            float deltaTime = Time.unscaledDeltaTime;
            _auraPulseTimer += deltaTime;

            float cycleDuration = _auraPulseDuration + _auraPulseInterval;
            float cycleTime = _auraPulseTimer % cycleDuration;

            // Interval between pulses
            if (cycleTime >= _auraPulseDuration)
            {
                _auraPulseRenderer.enabled = false;
                return;
            }

            _auraPulseRenderer.enabled = true;

            // Progress through pulse (0 → 1)
            float t = cycleTime / _auraPulseDuration;

            // Smooth easing: starts slow, expands faster
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            // Scale from center to boundary
            float diameter = _auraRadius * 2f;
            float scale = diameter * easedT;
            _auraPulseRenderer.transform.localScale = new Vector3(scale, scale, 1f);

            // Alpha: fade in → fade out (sine wave)
            float alpha = Mathf.Sin(t * Mathf.PI) * _auraPulseMaxAlpha;
            Color color = _auraPulseColor;
            color.a = alpha;
            _auraPulseRenderer.color = color;
        }

        /// <summary>
        /// Check if enemy has regeneration aura (used by movement logic).
        /// </summary>
        public static bool HasRegenerationAura(EnemyData enemyData)
        {
            if (enemyData?.effects == null) return false;

            foreach (var eff in enemyData.effects)
            {
                if (eff?.aura == null) continue;
                foreach (var act in eff.aura)
                {
                    if (act?.effect == StatusEffectType.Regeneration)
                        return true;
                }
            }
            return false;
        }
    }
}
