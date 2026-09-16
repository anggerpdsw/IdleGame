using UnityEngine;
using System.Collections;
using TMPro;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Grave spawned when Necromancer dies.
    /// Shows countdown timer, triggers revive after duration.
    /// Not targetable (no collider/health).
    /// </summary>
    public class NecromancerGrave : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private RectTransform _rectTransform;
        private NecromancerBehavior _owner;
        private float _reviveTime;
        private Coroutine _reviveRoutine;
        private Vector3 _worldPosition;
        private UnityEngine.Camera _camera;

        public void Initialize(NecromancerBehavior owner, float duration, Vector3 worldPosition)
        {
            _owner = owner;
            _reviveTime = duration;
            _worldPosition = worldPosition;
            _camera = UnityEngine.Camera.main;

            if (_timerText == null)
                _timerText = GetComponentInChildren<TextMeshProUGUI>();

            if (_reviveRoutine != null)
                StopCoroutine(_reviveRoutine);
            _reviveRoutine = StartCoroutine(ReviveCountdown());
        }

        private IEnumerator ReviveCountdown()
        {
            while (_reviveTime > 0)
            {
                // Update screen position setiap frame (follow camera)
                if (_camera != null && _rectTransform != null)
                {
                    Vector3 screenPos = _camera.WorldToScreenPoint(_worldPosition);
                    _rectTransform.position = screenPos;
                }

                if (_timerText != null)
                    _timerText.text = $"{Mathf.CeilToInt(_reviveTime)}s";

                yield return null;
                _reviveTime -= Time.deltaTime;
            }

            // Revive complete - trigger owner, destroy self
            if (_owner != null)
                _owner.OnReviveComplete(_worldPosition);

            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (_reviveRoutine != null)
            {
                StopCoroutine(_reviveRoutine);
                _reviveRoutine = null;
            }
        }
    }
}
