using UnityEngine;

namespace IdleDefenseSurvival.Camera
{
    /// <summary>
    /// Automatically scales a background SpriteRenderer to always cover the camera's viewport.
    /// Works with CameraFollow's orthographic size changes.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundScaler : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the main camera. If null, uses Camera.main.")]
        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Background SpriteRenderer to scale.")]
        [SerializeField] private SpriteRenderer _backgroundRenderer;

        [Header("Settings")]
        [Tooltip("Extra padding around the camera view (in world units).")]
        [SerializeField] private float _padding = 0.5f;

        [Tooltip("Only scale on X axis (useful for infinite horizontal backgrounds).")]
        [SerializeField] private bool _scaleOnlyX = false;

        [Tooltip("Only scale on Y axis.")]
        [SerializeField] private bool _scaleOnlyY = false;

        private void Awake()
        {
            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_backgroundRenderer == null)
                _backgroundRenderer = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (_camera == null || _backgroundRenderer == null || _backgroundRenderer.sprite == null)
                return;

            ScaleBackgroundToCamera();
        }

        /// <summary>
        /// Scales the background to fully cover the camera's orthographic viewport.
        /// </summary>
        public void ScaleBackgroundToCamera()
        {
            // Camera orthographic size = half the height
            float camHeight = _camera.orthographicSize * 2f;
            float camWidth = camHeight * _camera.aspect;

            // Add padding
            float targetWidth = camWidth + _padding * 2f;
            float targetHeight = camHeight + _padding * 2f;

            // Get sprite's world size at scale 1
            var spriteBounds = _backgroundRenderer.sprite.bounds;
            float spriteWidth = spriteBounds.size.x;
            float spriteHeight = spriteBounds.size.y;

            // Calculate required scale
            float scaleX = targetWidth / spriteWidth;
            float scaleY = targetHeight / spriteHeight;

            // Apply scale based on settings
            Vector3 newScale = transform.localScale;

            if (!_scaleOnlyY)
                newScale.x = scaleX;
            if (!_scaleOnlyX)
                newScale.y = scaleY;

            // For infinite horizontal backgrounds, maintain aspect ratio on Y
            if (_scaleOnlyX)
                newScale.y = scaleX;

            transform.localScale = newScale;
        }

        /// <summary>
        /// Call this manually when camera size changes significantly.
        /// </summary>
        public void Refresh()
        {
            ScaleBackgroundToCamera();
        }

        private void OnValidate()
        {
            if (_backgroundRenderer == null)
                _backgroundRenderer = GetComponent<SpriteRenderer>();

            // Preview in editor
            if (Application.isPlaying)
                ScaleBackgroundToCamera();
        }
    }
}