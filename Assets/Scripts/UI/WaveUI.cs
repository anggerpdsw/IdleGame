using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Manager;
using TMPro;
using System.Collections;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays wave information using a single slider.
    /// - Green color during ActiveWave
    /// - Yellow color during InterWave
    /// </summary>
    public class WaveUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _tierNowLabel;
        [SerializeField] private TextMeshProUGUI _waveNowLabel;
        [SerializeField] private GameObject _rewardPanel;
        [SerializeField] private CanvasGroup _rewardGroup;
        [SerializeField] private TextMeshProUGUI _rewardGold;
        [SerializeField] private TextMeshProUGUI _rewardMeat;

        [Header("Slider Colors")]
        [SerializeField] private Slider _waveSlider;
        [Tooltip("Color when wave is active (green).")]
        [SerializeField] private Color _activeColor = new(0.2f, 0.8f, 0.2f, 1f);
        [Tooltip("Color when between waves (yellow).")]
        [SerializeField] private Color _interWaveColor = new(1f, 0.8f, 0.2f, 1f);

        private Image _fillImage;
        private Coroutine _hideRoutine;
        private static WaitForSeconds _waitForSeconds3 = new(3f);
        private bool isReady = false;

        private void Awake()
        {
            _fillImage = _waveSlider.fillRect.GetComponent<Image>();
        }

        private void Start()
        {
            _rewardGroup.alpha = 0f;
            _rewardPanel.SetActive(false);
            // Use a coroutine to wait until essential singletons are initialized.
            StartCoroutine(InitializeWave());
        }

        private IEnumerator InitializeWave()
        {
            yield return new WaitUntil(() => 
                WaveManager.Instance != null && 
                GameManager.Instance != null && 
                SaveManager.Instance != null);
            WaveManager.OnWaveBonusReward += ShowReward;
            WaveManager.Instance.IsRunActive = true;
        }

        private void Update()
        {
            if (!isReady)
            {
                isReady = true;
                return;
            }
            UpdateWaveDisplay();
        }

        private void UpdateWaveDisplay()
        {
            WaveInfo info = WaveManager.Instance.GetWaveInfo();
            _tierNowLabel.text = $"Tier {info.TierNumber}";
            _waveNowLabel.text = $"Wave {info.WaveNumber}";

            // Calculate progress (0 to 1)
            float duration = info.State == WaveState.ActiveWave
                ? info.WaveDuration
                : info.InterWaveDuration;

            float progress = info.TimeRemaining / duration;
            _waveSlider.value = progress;

            // Set slider fill color based on state
            if (_fillImage != null)
            {
                _fillImage.color = info.State == WaveState.ActiveWave
                    ? _activeColor
                    : _interWaveColor;
            }
        }

        private void ShowReward(long gold, long meat)
        {
            _rewardPanel.SetActive(true);
            _rewardGroup.alpha = 1;

            _rewardGold.text = gold > 0f ? $"Gold +{gold:N0}" : "";
            _rewardMeat.text = meat > 0f ? $"Meat +{meat:N0}" : "";

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterSeconds());
        }

        private IEnumerator HideAfterSeconds()
        {
            yield return _waitForSeconds3;
            float t = 0;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                _rewardGroup.alpha = Mathf.Lerp(1, 0, t / 0.5f);
                yield return null;
            }
            _rewardGroup.alpha = 0;
            _rewardPanel.SetActive(false);
        }

        private void OnDisable()
        {
            WaveManager.OnWaveBonusReward -= ShowReward;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Set default colors if not set
            if (_activeColor == Color.black) _activeColor = GameColors.green;
            if (_interWaveColor == Color.black) _interWaveColor = GameColors.waveInterYellow;
        }
#endif
    }
}
