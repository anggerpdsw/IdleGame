using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Vertical progress bar for Lightning ultimate.
    /// Listens to LightningHandler events instead of polling every frame.
    /// </summary>
    public class UltimateBar : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;

        private UltimateData _lightningData;

        private void Awake()
        {
            _lightningData = UltimateManager.Instance?.GetUltimate(UltimateDMG.Lightning.ToString());
            if (_lightningData != null)
                // Initialize fill amount
                _fillImage.fillAmount = LightningHandler.Progress;
        }

        private void OnEnable()
        {
            LightningHandler.OnLightningProgressChanged += OnProgressChanged;
            LightningHandler.OnLightningReady += OnLightningReady;
        }

        private void OnDisable()
        {
            LightningHandler.OnLightningProgressChanged -= OnProgressChanged;
            LightningHandler.OnLightningReady -= OnLightningReady;
        }

        private void OnProgressChanged(float progress)
        {
            if (_fillImage != null) _fillImage.fillAmount = progress;
        }

        private void OnLightningReady()
        {
            // Optional: Add visual feedback when lightning is ready (flash, pulse, etc.)
            // For now just reset fill (already done via OnProgressChanged with 0)
        }
    }
}