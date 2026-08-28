using UnityEngine;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Camera
{
    /// <summary>
    /// Camera that follows player and adjusts orthographic size based on player's attack range.
    /// Provides visual feedback: enemies entering the attack range are visible on screen.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the Player script to get attack range.")]
        [SerializeField] private Player.Player _player;

        [Header("Zoom Settings")]
        [Tooltip("Margin added around attack range for better visibility.")]
        [SerializeField] private float _margin = 1f;
        [Tooltip("Smoothing speed for orthographic size changes.")]
        [SerializeField] private float _smoothSpeed = 4f;
        [Tooltip("Minimum camera size (zoom in limit).")]
        [SerializeField] private float _minSize = 2.5f;
        [Tooltip("Maximum camera size (zoom out limit).")]
        [SerializeField] private float _maxSize = 30f;

        [Header("Follow Settings")]
        [Tooltip("Smoothing speed for position following.")]
        [SerializeField] private float _followSpeed = 10f;
        [Tooltip("Offset from player position (useful for 2D top-down).")]
        [SerializeField] private Vector3 _positionOffset = new(0f, 0f, -10f);

        private UnityEngine.Camera _camera;
        private float _targetSize;
        private Vector3 _targetPosition;

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

        private void Update()
        {
            if (_player == null)
            {
                // Retry finding player
                _player = FindFirstObjectByType<Player.Player>();
                if (_player == null) return;
            }

            UpdateTargets();
            SmoothFollow();
        }

        /// <summary>
        /// Calculate target orthographic size based on player's attack range.
        /// Orthographic size in Unity is half the height of the viewport.
        /// </summary>
        private float CalculateTargetSize()
        {
            float targetSize = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) + _margin;
            return Mathf.Clamp(targetSize, _minSize, _maxSize);
        }

        /// <summary>
        /// Update target position and size.
        /// </summary>
        private void UpdateTargets()
        {
            // Target position follows player
            _targetPosition = _player.transform.position + _positionOffset;

            // Target size based on player attack range
            _targetSize = CalculateTargetSize();
        }

        /// <summary>
        /// Smoothly move camera position and adjust orthographic size.
        /// </summary>
        private void SmoothFollow()
        {
            // Smooth position
            transform.position = Vector3.Lerp(
                transform.position,
                _targetPosition,
                _followSpeed * Time.deltaTime
            );

            // Smooth orthographic size
            _camera.orthographicSize = Mathf.Lerp(
                _camera.orthographicSize,
                _targetSize,
                _smoothSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Manually trigger camera update (useful when player stats change significantly).
        /// </summary>
        public void RefreshCamera()
        {
            if (_player != null)
            {
                UpdateTargets();
                _camera.orthographicSize = _targetSize;
                transform.position = _targetPosition;
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
            if (_margin < 0f)
                _margin = 0f;
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
