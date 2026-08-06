using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Controls game speed via Time.timeScale.
    /// - Normal speed: 1x (realtime)
    /// - Max speed without VIP: 5.5x
    /// - Max speed with VIP: 7.5x
    ///
    /// Attach to a Canvas GameObject that has the speed button as a child.
    /// Does NOT modify any existing game scripts.
    /// </summary>
    public class GameSpeedController : MonoBehaviour
    {
        [Header("Speed Settings")]
        [SerializeField] private float[] _speedOptions = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f, 6.5f, 7f, 7.5f };
        [SerializeField] private int _normalMaxIndex = 10; // 5.5x
        [SerializeField] private int _vipMaxIndex = 14;    // 7.5x

        [Header("UI References")]
        [Tooltip("Text that shows current speed (e.g. '1.0x').")]
        [SerializeField] private TextMeshProUGUI _speedLabel;

        [Header("Optional (off by default)")]
        [Tooltip("Set true when user has VIP status.")]
        [SerializeField] private bool _isVip = false;

        private int _speedIndex = 1;
        private int MaxSpeedIndex => _isVip ? _vipMaxIndex : _normalMaxIndex;

        private void Start()
        {
            CheckVIP();
        }

        public void IncreaseSpeed()
        {
            if (_speedIndex < MaxSpeedIndex)
            {
                _speedIndex++;
                ApplySpeed();
            }
        }

        public void DecreaseSpeed()
        {
            if (_speedIndex > 0)
            {
                _speedIndex--;
                ApplySpeed();
            }
        }

        /// <summary>
        /// Set speed manually (useful for save/load).
        /// </summary>
        public void SetSpeed(float targetSpeed)
        {
            float maxAllowed = _speedOptions[MaxSpeedIndex];
            float clamped = Mathf.Clamp(targetSpeed, _speedOptions[0], maxAllowed);

            // Find closest index
            int bestIndex = 0;
            float bestDiff = Mathf.Abs(_speedOptions[0] - clamped);
            for (int i = 1; i <= MaxSpeedIndex; i++)
            {
                float diff = Mathf.Abs(_speedOptions[i] - clamped);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }

            _speedIndex = bestIndex;
            ApplySpeed();
        }

        /// <summary>
        /// Toggle VIP status and clamp speed if needed.
        /// </summary>
        public void CheckVIP()
        {
            _isVip = SaveManager.Instance.IsMaxSpeedEnabled();
            // If current speed index now exceeds the allowed max, clamp down
            if (_speedIndex > MaxSpeedIndex)
                _speedIndex = MaxSpeedIndex;

            ApplySpeed();
        }

        private void ApplySpeed()
        {
            float speed = _speedOptions[_speedIndex];
            Time.timeScale = speed;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (_speedLabel != null)
            {
                float speed = _speedOptions[_speedIndex];
                _speedLabel.text = $"{speed:F1}x";
            }
        }

        private void OnValidate()
        {
            // Auto-clamp speed when VIP status changes in Inspector
            if (_speedOptions != null && _speedOptions.Length > 0)
            {
                // Clamp max indices to valid array bounds
                if (_normalMaxIndex >= _speedOptions.Length) _normalMaxIndex = _speedOptions.Length - 1;
                if (_vipMaxIndex >= _speedOptions.Length) _vipMaxIndex = _speedOptions.Length - 1;
                if (_normalMaxIndex < 0) _normalMaxIndex = 0;
                if (_vipMaxIndex < 0) _vipMaxIndex = 0;

                // When VIP status changes, clamp speed to new max
                int currentMax = MaxSpeedIndex;
                if (_speedIndex > currentMax)
                {
                    _speedIndex = currentMax;

                    // Apply speed change if running
                    if (Application.isPlaying)
                    {
                        ApplySpeed();
                    }
                }
            }
        }

    }
}
