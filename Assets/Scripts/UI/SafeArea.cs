using UnityEngine;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Menjaga RectTransform agar selalu berada di dalam safe area layar
    /// (menghindari notch / rounded corner pada Android & iOS).
    /// Pasang pada root panel ber-anchor stretch (Min 0,0 Max 1,1) di bawah Canvas.
    /// Pada editor / device tanpa notch, safe area = full screen, jadi tidak ada perubahan visual.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private Rect _lastSafeArea = Rect.zero;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            Refresh();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
                Refresh();
        }

        private void Refresh()
        {
            _lastSafeArea = Screen.safeArea;

            // Normalisasi ke fraksi layar, lalu pakai sebagai anchor —
            // kompatibel dengan CanvasScaler apa pun (unit pixel tidak perlu diubah).
            Vector2 min = _lastSafeArea.position;
            Vector2 max = _lastSafeArea.position + _lastSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rectTransform.anchorMin = min;
            _rectTransform.anchorMax = max;
        }
    }
}
