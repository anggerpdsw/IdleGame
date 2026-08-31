using UnityEngine;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Camera
{
    /// <summary>
    /// Camera that stays fixed at origin and adjusts orthographic size based on player's attack range.
    /// Does NOT follow player position — player moves within static camera view.
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

        private UnityEngine.Camera _camera;
        private float _targetSize;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            if (_camera == null)
            {
                Debug.LogError("CameraFollow requires a Camera component!");
                return;
            }

            // Camera stays fixed at origin
            transform.position = new Vector3(0f, 0f, -10f);

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

            UpdateZoom();
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
        /// Update only orthographic size based on player attack range.
        /// Camera position stays fixed at origin.
        /// </summary>
        private void UpdateZoom()
        {
            _targetSize = CalculateTargetSize();

            // Smooth orthographic size only
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