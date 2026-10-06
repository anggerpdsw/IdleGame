using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Camera
{
    /// <summary>
    /// Camera that follows player position and adjusts orthographic size based on player's attack range.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the Player script to get attack range.")]
        [SerializeField] private Player.Player _player;

        [Header("Follow Settings")]
        [Tooltip("How smoothly the camera follows the player.")]
        [SerializeField] private float _followSpeed = 8f;

        [Header("Zoom Settings")]
        [Tooltip("Margin added around attack range for better visibility.")]
        [SerializeField] private float _margin = 2f;
        [Tooltip("Minimum camera size (zoom in limit).")]
        [SerializeField] private float _minSize = 2.5f;
        [Tooltip("Maximum camera size (zoom out limit).")]
        [SerializeField] private float _maxSize = 30f;

        private UnityEngine.Camera _camera;
        private float _targetSize;

        // Cache last-seen attack range to avoid reacting to tiny fluctuations (e.g., OverkillConversion stacks)
        private float _lastAttackRange = -1f;

        // Hysteresis thresholds: larger threshold to COMMIT a new target, smaller to REVERT
        // Prevents oscillation when range fluctuates around a boundary (e.g., 9.9 ↔ 10.1)
        private const float RangeChangeThresholdUp = 1.0f;   // require +1.0 to zoom OUT
        private const float RangeChangeThresholdDown = 0.7f; // require -0.7 to zoom IN

        // Stability window: require N consecutive frames over threshold before committing
        private const int StabilityFramesRequired = 3;
        private int _stabilityCounter = 0;
        private float _pendingTargetSize = -1f;

        // orthographic size units / sec - slower for smoother feel
        private const float MaxZoomDeltaPerSec = 3f; 

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            if (_camera == null)
            {
                Debug.LogError("CameraFollow requires a Camera component!");
                return;
            }

            // If player reference not set, try to find it
            if (_player == null)
            {
                _player = FindFirstObjectByType<Player.Player>();
                if (_player == null)
                {
                    Debug.LogWarning("CameraFollow: Player not found. Will try again later.");
                }
            }
        }

        private void Start()
        {
            if (_player != null)
            {
                _targetSize = CalculateTargetSize();
                _camera.orthographicSize = _targetSize;
            }
        }

        private void LateUpdate()
        {
            if (_player == null)
            {
                // Retry finding player
                _player = FindFirstObjectByType<Player.Player>();
                if (_player == null) return;
            }

            FollowPlayer();
            UpdateZoom();
        }

        /// <summary>
        /// Smoothly follow player position while keeping Z fixed for orthographic camera.
        /// </summary>
        private void FollowPlayer()
        {
            Vector3 targetPos = _player.transform.position;
            targetPos.z = -10f;
            transform.position = Vector3.Lerp(transform.position, targetPos, _followSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Calculate target orthographic size based on player's attack range.
        /// Orthographic size in Unity is half the height of the viewport.
        /// </summary>
        private float CalculateTargetSize()
        {
            float targetSize = PlayerStatsManager.Instance.GetBaseStat(SkillType.AttackRange) + _margin;
            return Mathf.Clamp(targetSize, _minSize, _maxSize);
        }

        /// <summary>
        /// Update orthographic size based on player attack range.
        /// Uses hysteresis + frame-stability window to prevent oscillation.
        /// </summary>
        private void UpdateZoom()
        {
            float currentRange = PlayerStatsManager.Instance.GetBaseStat(SkillType.AttackRange);

            if (_lastAttackRange < 0f)
            {
                _lastAttackRange = currentRange;
                _targetSize = Mathf.Clamp(currentRange + _margin, _minSize, _maxSize);
                _camera.orthographicSize = _targetSize;
                return;
            }

            float diff = currentRange - _lastAttackRange;
            float absDiff = Mathf.Abs(diff);

            // Choose threshold based on direction (hysteresis)
            float threshold = diff > 0 ? RangeChangeThresholdUp : RangeChangeThresholdDown;

            if (absDiff >= threshold)
            {
                // Potential target change detected — start stability counter
                float newTarget = Mathf.Clamp(currentRange + _margin, _minSize, _maxSize);

                if (Mathf.Abs(newTarget - _pendingTargetSize) < 0.01f)
                {
                    // Same pending target, increment stability
                    _stabilityCounter++;
                }
                else
                {
                    // New pending target, reset counter
                    _pendingTargetSize = newTarget;
                    _stabilityCounter = 1;
                }

                // Commit after N consecutive frames over threshold
                if (_stabilityCounter >= StabilityFramesRequired)
                {
                    _targetSize = _pendingTargetSize;
                    _lastAttackRange = currentRange;
                    _stabilityCounter = 0;
                    _pendingTargetSize = -1f;
                }
            }
            else
            {
                // Below threshold — reset stability
                _stabilityCounter = 0;
                _pendingTargetSize = -1f;
            }

            // Always animate toward committed target, capped per second
            float maxDelta = MaxZoomDeltaPerSec * Time.deltaTime;
            _camera.orthographicSize = Mathf.MoveTowards(
                _camera.orthographicSize,
                _targetSize,
                maxDelta
            );
        }

        /// <summary>
        /// Manually trigger camera update (useful when player stats change significantly).
        /// </summary>
        public void RefreshCamera()
        {
            if (_player != null)
            {
                _targetSize = CalculateTargetSize();
                _camera.orthographicSize = _targetSize;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure orthographic camera
            if (_camera == null)
                _camera = GetComponent<UnityEngine.Camera>();

            if (_camera != null && !_camera.orthographic)
            {
                Debug.LogWarning("CameraFollow works best with orthographic camera.");
            }

            // Validate margins
            if (_margin < 0f) _margin = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (_player != null)
            {
                // Draw attack range circle in scene view
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_player.transform.position, _player.AttackRange);

                // Draw camera view bounds
                if (_camera != null && _camera.orthographic)
                {
                    float height = _camera.orthographicSize * 2f;
                    float width = height * _camera.aspect;
                    Vector3 center = transform.position + new Vector3(0f, 0f, 10f);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(center, new Vector3(width, height, 0.1f));
                }
            }
        }
#endif
    }
}