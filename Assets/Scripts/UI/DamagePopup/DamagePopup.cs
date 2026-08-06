using System.Collections;
using UnityEngine;
using TMPro;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Individual damage popup component that displays animated damage numbers.
    /// Uses coroutine-based animations and returns to pool when complete.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DamagePopup : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        [Tooltip("TextMeshProUGUI component for displaying damage text.")]
        private TextMeshProUGUI _damageText;

        [SerializeField]
        [Tooltip("CanvasGroup for controlling fade animations.")]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        [Tooltip("Distance the popup travels upward.")]
        private float _moveDistance = 2f;

        [SerializeField]
        [Tooltip("Initial scale pop animation size multiplier.")]
        private float _popScaleMultiplier = 1.2f;

        [SerializeField]
        [Tooltip("Duration of the initial pop scale animation.")]
        private float _popDuration = 0.2f;

        private DamagePopupPool _pool;
        private Coroutine _animationCoroutine;
        private RectTransform _rectTransform;

        private void Awake()
        {
            // Cache components
            if (_damageText == null) _damageText = GetComponent<TextMeshProUGUI>();

            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            _rectTransform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Initialize the popup with data and start animation.
        /// Called by DamagePopupManager when popup is retrieved from pool.
        /// </summary>
        public void Initialize(Vector3 screenPosition, DamagePopupData data, DamagePopupPool pool)
        {
            _pool = pool;

            // Set position
            transform.position = screenPosition;

            // Set text content
            _damageText.text = data.GetDisplayText();

            // Set color (use override if provided, otherwise type-based color)
            _damageText.color = data.OverrideColor ?? data.GetTypeColor();

            // Set font size with camera scaling to maintain readability across different zoom levels
            float fontSize = data.GetScale();
            _damageText.fontSize = fontSize;

            // Reset state
            _canvasGroup.alpha = 1f;
            _rectTransform.localScale = Vector3.one;

            // Start animation
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(AnimatePopup(data.Duration));
        }

        /// <summary>
        /// Main animation coroutine: pop scale -> move up -> fade out -> return to pool.
        /// Menggunakan alternating left/right untuk menghindari penumpukan.
        /// </summary>
        private IEnumerator AnimatePopup(float duration)
        {
            // Offset horizontal sudah ditentukan oleh DamagePopupManager per target
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + new Vector3(0f, _moveDistance, 0f);

            float elapsed = 0f;

            // Phase 1: Initial pop scale animation
            float popElapsed = 0f;
            Vector3 initialScale = _rectTransform.localScale;
            Vector3 popScale = initialScale * _popScaleMultiplier;

            while (popElapsed < _popDuration)
            {
                popElapsed += Time.deltaTime;
                float popProgress = popElapsed / _popDuration;

                // Elastic pop effect (scale up then back down)
                float scaleProgress = popProgress < 0.5f
                    ? Mathf.Lerp(1f, _popScaleMultiplier, popProgress * 2f)
                    : Mathf.Lerp(_popScaleMultiplier, 1f, (popProgress - 0.5f) * 2f);

                _rectTransform.localScale = initialScale * scaleProgress;

                yield return null;
            }

            _rectTransform.localScale = initialScale;

            // Phase 2: Move up and fade out
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Ease-out movement (starts fast, slows down)
                float easeProgress = 1f - Mathf.Pow(1f - progress, 2f);
                transform.position = Vector3.Lerp(startPosition, endPosition, easeProgress);

                // Fade out in the last 60% of the animation
                if (progress > 0.4f)
                {
                    float fadeProgress = (progress - 0.4f) / 0.6f;
                    _canvasGroup.alpha = 1f - fadeProgress;
                }

                yield return null;
            }

            // Animation complete - return to pool
            ReturnToPool();
        }

        /// <summary>
        /// Return this popup to the pool for reuse.
        /// </summary>
        private void ReturnToPool()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            if (_pool != null)
            {
                _pool.Return(this);
            }
            else
            {
                Debug.LogWarning("[DamagePopup] No pool reference - destroying instead of returning.");
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            // Clean up coroutine when disabled
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_damageText == null) _damageText = GetComponent<TextMeshProUGUI>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_popDuration < 0.05f) _popDuration = 0.05f;
        }
#endif
    }
}
