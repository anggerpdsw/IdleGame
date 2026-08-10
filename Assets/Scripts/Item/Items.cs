using UnityEngine;
using IdleDefenseSurvival.Economy;
using System.Collections;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Item
{
    /// <summary>
    /// Currency item with spread spawn, hover idle state, click-to-attract, and collection mechanics.
    /// 1. Spawns with spread effect from enemy death position
    /// 2. Hovers with idle animation
    /// 3. Click to start magnetic movement toward player
    /// 4. Shrinks while moving to player
    /// 5. Collects when reaching player
    /// </summary>
    public class CurrencyPickup : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private CircleCollider2D _collider;

        [Header("Spawn Spread Animation")]
        [SerializeField] private float _targetVisualWorldSize = 0.27f;
        [SerializeField] private float _spreadRadius = 1.5f;
        [SerializeField] private float _spreadDuration = 0.6f;
        [SerializeField] private AnimationCurve _spreadCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Spawn Scale Animation")]
        [SerializeField] private AnimationCurve _spawnScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Idle Settings")]
        [SerializeField] private float _hoverHeight = 0.2f;
        [SerializeField] private float _hoverSpeed = 3f;

        [Header("Collection Animation")]
        [SerializeField] private float _moveToPlayerSpeed = 2.7f;
        [SerializeField] private float _shrinkDuration = 3f;
        [SerializeField] private AnimationCurve _shrinkCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Magnetic Collection")]
        [SerializeField] private float _clickRadius = 0.5f;

        [Header("Auto-Destroy")]
        [SerializeField] private float _autoDestroyDelay = 60f;

        private CurrencyType _currencyType = CurrencyType.Gem;
        private Transform _player;
        private ItemState _state = ItemState.Spawning;
        private Vector3 _originalPosition;
        private Vector3 _spawnCenterPosition;
        private float _spawnTime;
        private float _collectStartTime;
        private Vector3 _baseVisualScale = Vector3.one;

        private void Awake()
        { 
            if (_collider != null)
            {
                _collider.radius = _clickRadius;
                _collider.enabled = true;
            }
            transform.localScale = Vector3.one;

            _spawnTime = Time.unscaledTime;
            _spawnCenterPosition = transform.position;
            _originalPosition = transform.position;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                _player = playerObj.transform;

            LoadSprite();
            if (_visual != null)
                _visual.localScale = Vector3.zero;
        }

        private void Start()
        {
            StartCoroutine(ItemLifecycle());
        }

        /// <summary>
        /// Single lifecycle coroutine — replaces Update() + multiple coroutines.
        /// Handles spawn → idle → moving → collect in one flow.
        /// More efficient at 100+ items: no native Update callback overhead,
        /// no multiple coroutine tracking per item.
        /// </summary>
        private IEnumerator ItemLifecycle()
        {
            // Phase 1: Spawn spread animation
            yield return StartCoroutine(SpawnWithSpreadAnimation());

            // Phase 2: Auto-collect delay (VIP only)
            if (SaveManager.Instance != null && SaveManager.Instance.IsAutoCollectEnabled())
            {
                float timer = 9f;
                while (timer > 0f)
                {
                    // Exit early if player clicked this item during delay
                    if (_state != ItemState.Idle) break;
                    timer -= Time.unscaledDeltaTime;
                    UpdateIdleHover();
                    yield return null;
                }
                if (_state == ItemState.Idle) StartCollection();
            }

            // Phase 3: Main lifecycle loop — handles Idle and MovingToPlayer
            while (this != null)
            {
                switch (_state)
                {
                    case ItemState.Idle:
                        // Lifetime check
                        if (Time.unscaledTime - _spawnTime > _autoDestroyDelay)
                        {
                            Destroy(gameObject);
                            yield break;
                        }
                        UpdateIdleHover();
                        yield return null;
                        break;

                    case ItemState.Collecting:
                        // Transition state — wait one frame
                        yield return null;
                        break;

                    case ItemState.MovingToPlayer:
                        UpdateMovingToPlayer();
                        // Collect() sets → CollectingCurrency → Despawning, then destroys
                        if (_state == ItemState.Despawning) yield break;
                        yield return null;
                        break;

                    case ItemState.CollectingCurrency:
                    case ItemState.Despawning:
                        yield break;

                    default:
                        yield return null;
                        break;
                }
            }
        }

        /// <summary>
        /// Idle hover animations (sinusoidal up-down).
        /// </summary>
        private void UpdateIdleHover()
        {
            float hoverY = _originalPosition.y + Mathf.Sin(Time.unscaledTime * _hoverSpeed) * _hoverHeight;
            transform.position = new Vector3(_originalPosition.x, hoverY, _originalPosition.z);
        }

        /// <summary>
        /// Move toward player with shrink animation; collect when close.
        /// </summary>
        private void UpdateMovingToPlayer()
        {
            if (_player == null) return;
            transform.position = Vector3.MoveTowards(
                transform.position, _player.position, _moveToPlayerSpeed * Time.unscaledDeltaTime);
            float elapsed = Time.unscaledTime - _collectStartTime;
            float t = Mathf.Clamp01(elapsed / _shrinkDuration);
            float scaleValue = _shrinkCurve.Evaluate(t);
            if (_visual != null)
                _visual.localScale = _baseVisualScale * scaleValue;
            if (Vector2.Distance(transform.position, _player.position) < 0.03f) Collect();
        }

        /// <summary>
        /// Transition to a new state and run any entry logic.
        /// </summary>
        private void TransitionTo(ItemState newState)
        {
            _state = newState;

            if (newState == ItemState.MovingToPlayer)
            {
                _collectStartTime = Time.unscaledTime;
            }
        }

        /// <summary>
        /// Spread animation from center spawn position to final position with scale animation.
        /// </summary>
        private IEnumerator SpawnWithSpreadAnimation()
        {
            float elapsed = 0f;

            while (elapsed < _spreadDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _spreadDuration;
                float curveValue = _spreadCurve.Evaluate(t);

                transform.position = Vector3.Lerp(_spawnCenterPosition, _originalPosition, curveValue);

                float scaleValue = _spawnScaleCurve.Evaluate(t);
                if (_visual != null)
                    _visual.localScale = _baseVisualScale * scaleValue;

                yield return null;
            }

            transform.position = _originalPosition;
            if (_visual != null)
                _visual.localScale = _baseVisualScale;

            // Update original position to final spread position so hover is correct
            _originalPosition = transform.position;

            // After spawn animation, switch to Idle state
            TransitionTo(ItemState.Idle);
        }

        /// <summary>
        /// Initialize item with currency type and spawn amount.
        /// </summary>
        public void Initialize(CurrencyType type, long amount)
        {
            _currencyType = type;
            LoadSprite();
            UpdateName();

            if (amount > 1) SpawnAdditionalItems(amount - 1);
        }

        /// <summary>
        /// Update GameObject name for better hierarchy readability.
        /// </summary>
        private void UpdateName() => gameObject.name = $"{_currencyType}_{GetInstanceID():X8}";

        /// <summary>
        /// Spawn multiple items spreading from center spawn position.
        /// </summary>
        private void SpawnAdditionalItems(long count)
        {
            for (long i = 0; i < count; i++)
            {
                float angle = 360f / count * i + Random.Range(-15f, 15f);
                float randomDistance = Random.Range(_spreadRadius * 0.6f, _spreadRadius);

                Vector2 spreadDir = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector3 spreadPos = _spawnCenterPosition + (Vector3)spreadDir * randomDistance;

                StartCoroutine(SpawnItemWithDelay(spreadPos, 0.05f * i));
            }
        }

        /// <summary>
        /// Spawn a single spread item with delay.
        /// </summary>
        private IEnumerator SpawnItemWithDelay(Vector3 spreadPos, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (this == null) yield break;
            GameObject itemObj = Instantiate(gameObject, _spawnCenterPosition, Quaternion.identity, UIManager.Instance.DropRoot);
            if (itemObj.TryGetComponent<CurrencyPickup>(out var item))
            {
                item._state = ItemState.Spawning;
                item._spawnTime = Time.unscaledTime;
                item._spawnCenterPosition = _spawnCenterPosition;
                item._originalPosition = spreadPos;
                item._currencyType = _currencyType;
                item.transform.position = _spawnCenterPosition;
                item.transform.localScale = Vector3.one;
                if (item._visual != null)
                    item._visual.localScale = Vector3.zero;
                item.LoadSprite();
                item.UpdateName();
            }
        }

        /// <summary>
        /// Start moving toward player. Called by external click manager or magnetic range.
        /// </summary>
        public void StartCollection()
        {
            if (_state != ItemState.Idle) return;

            // Enter Collecting state (preparation phase)
            TransitionTo(ItemState.Collecting);

            // Disable collider to prevent multiple clicks
            if (_collider != null) _collider.enabled = false;

            // Immediately transition to MovingToPlayer
            TransitionTo(ItemState.MovingToPlayer);
        }

        /// <summary>
        /// Collect item and add currency to economy.
        /// </summary>
        private void Collect()
        {
            if (_state == ItemState.Despawning) return;

            // Transition to CollectingCurrency state
            TransitionTo(ItemState.CollectingCurrency);

            // Add currency to economy
            var economy = EconomyManager.Instance;
            // Item => Reason untuk debug "Item collected"
            if (economy != null) {
                economy.AddCurrency(_currencyType, 1);
                if (_currencyType == CurrencyType.Meat) 
                    WaveManager.Instance.RecordMeat(1);
            }

            // Transition to Despawning and destroy
            TransitionTo(ItemState.Despawning);
            Destroy(gameObject);
        }

        private void LoadSprite()
        {
            if (_spriteRenderer == null) return;
            Sprite sprite = ItemResources.GetItemSource(_currencyType.ToString());
            if (sprite == null)
                Debug.LogWarning($"[Items] Sprite not found at Resources/{sprite}");
            _spriteRenderer.sprite = sprite;
            CalculateBaseVisualScale();
        }

        private void CalculateBaseVisualScale()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;
            Vector2 spriteSize = _spriteRenderer.sprite.bounds.size;
            float maxDimension = Mathf.Max(spriteSize.x, spriteSize.y);
            if (maxDimension <= 0f)
            {
                _baseVisualScale = Vector3.one;
                return;
            }
            float scale = _targetVisualWorldSize / maxDimension;
            _baseVisualScale = Vector3.one * scale;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GameColors.debugYellowGizmo.WithAlpha(0.3f);
            Gizmos.DrawWireSphere(transform.position, _spreadRadius);

            Gizmos.color = GameColors.debugCyanGizmo.WithAlpha(0.5f);
            Gizmos.DrawWireSphere(transform.position, _clickRadius);
        }
#endif
    }
}
