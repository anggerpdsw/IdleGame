using UnityEngine;
using TMPro;
using IdleDefenseSurvival.Economy;
using DG.Tweening;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays current currency amounts (Gold, Gem, Meat) in the HUD.
    /// Updates automatically when currency changes via EconomyManager events.
    /// </summary>
    public class CurrencyDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _gemText;
        [SerializeField] private TextMeshProUGUI _meatText;

        [Header("Format Settings")]
        [Tooltip("Format: K = thousands, M = millions")]
        [SerializeField] private bool _useShortFormat = true;

        private long _displayGold;
        private long _displayMeat;
        private long _displayGem;

        private Tween _goldTween;
        private Tween _meatTween;
        private Tween _gemTween;

        private void Start()
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnCurrencyChanged.AddListener(OnCurrencyChanged);

                _displayGold = EconomyManager.Instance.Gold;
                _displayMeat = EconomyManager.Instance.Meat;
                _displayGem  = EconomyManager.Instance.Gem;

                // Initial display
                UpdateDisplay(CurrencyType.Gold, EconomyManager.Instance.Gold);
                UpdateDisplay(CurrencyType.Gem, EconomyManager.Instance.Gem);
                UpdateDisplay(CurrencyType.Meat, EconomyManager.Instance.Meat);
            }
            else
            {
                Debug.LogWarning("[CurrencyDisplay] EconomyManager not found — UI will update when currency changes.");
            }
        }

        private void OnDestroy()
        {
            // Guard against destroyed / fake‑null Unity object.
            if (EconomyManager.Instance != null)
            {
                try
                {
                    EconomyManager.Instance.OnCurrencyChanged.RemoveListener(OnCurrencyChanged);
                }
                catch (System.NullReferenceException)
                {
                    // OnCurrencyChanged UnityEvent was already torn down during shutdown.
                }
            }
        }

        private void OnCurrencyChanged(CurrencyType type, long oldValue, long newValue)
        {
            AnimateCurrency(type, oldValue, newValue);
        }

        private void AnimateCurrency(CurrencyType type, long from, long to)
        {
            float duration = Mathf.Clamp(Mathf.Abs(to - from) / 50000f, 0.2f, 0.8f);

            switch (type)
            {
                case CurrencyType.Gold:
                    _goldTween?.Kill();
                    _goldTween = DOTween.To(
                        () => (double)_displayGold,
                        x =>
                        {
                            _displayGold = (long)x;
                            UpdateDisplay(CurrencyType.Gold, _displayGold);
                        },
                        to,
                        duration).SetEase(Ease.OutCubic);
                    break;

                case CurrencyType.Gem:
                    _gemTween?.Kill();
                    _gemTween = DOTween.To(
                        () => (double)_displayGem,
                        x =>
                        {
                            _displayGem = (long)x;
                            UpdateDisplay(CurrencyType.Gem, _displayGem);
                        },
                        to,
                        duration).SetEase(Ease.OutCubic);
                    break;

                case CurrencyType.Meat:
                    _meatTween?.Kill();
                    _meatTween = DOTween.To(
                        () => (float)_displayMeat,
                        x =>
                        {
                            _displayMeat = (long)x;
                            UpdateDisplay(CurrencyType.Meat, _displayMeat);
                        },
                        to,
                        duration).SetEase(Ease.OutCubic);
                    break;
            }
        }

        private void UpdateDisplay(CurrencyType type, long amount)
        {
            string formatted = _useShortFormat ? Utilityku.FormatNumber(amount) : amount.ToString("N0");

            switch (type)
            {
                case CurrencyType.Gold:
                    if (_goldText != null)
                        _goldText.text = formatted;
                    break;
                case CurrencyType.Gem:
                    if (_gemText != null)
                        _gemText.text = formatted;
                    break;
                case CurrencyType.Meat:
                    if (_meatText != null)
                        _meatText.text = formatted;
                    break;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test: Add 1000 Gold")]
        private void TestAddGold()
        {
            EconomyManager.Instance?.AddCurrency(CurrencyType.Gold, 1000, "Test");
        }

        [ContextMenu("Test: Add 100 Gems")]
        private void TestAddGem()
        {
            EconomyManager.Instance?.AddCurrency(CurrencyType.Gem, 100, "Test");
        }

        [ContextMenu("Test: Add 50 Meat")]
        private void TestAddMeat()
        {
            EconomyManager.Instance?.AddCurrency(CurrencyType.Meat, 150, "Test");
        }
#endif
    }
}
