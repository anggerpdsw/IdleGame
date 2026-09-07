using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Controller;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Controls the Settings Panel visibility and binds UI elements to SettingsController.
    /// Attach this script to the SettingsPanel GameObject.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("Panel Reference")]
        [SerializeField] private GameObject _settingsPanel;

        [Header("UI Settings")]
        [SerializeField] private Toggle _damagePopupToggle;
        [SerializeField] private Toggle _criticalTextToggle;
        [SerializeField] private Toggle _healPopupToggle;
        [SerializeField] private Toggle _enemyHealthBarToggle;
        [SerializeField] private Toggle _autoCastUltimate;
        [SerializeField] private Toggle _autoPotion;
        [SerializeField] private Slider _popupDurationSlider;

        [Header("Audio Settings")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private Toggle _masterMuteToggle;
        [SerializeField] private Toggle _musicMuteToggle;
        [SerializeField] private Toggle _sfxMuteToggle;

        [Header("Gameplay Settings")]
        [SerializeField] private Toggle _screenShakeToggle;
        [SerializeField] private Toggle _cameraShakeToggle;
        [SerializeField] private Toggle _vibrationToggle;
        [SerializeField] private Slider _gameSpeedSlider;

        [Header("Buttons")]
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _surendButton;

        [Header("Potions")]
        [SerializeField] private GameObject _objPotion;
        [SerializeField] private Slider _hpPotion;
        [SerializeField] private Slider _mpPotion;
        
        private SettingsController _settings;

        private void Awake()
        {
            // Start with panel hidden
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        private void Start()
        {
            _settings = SettingsController.Instance;

            // Initialize UI with current settings
            InitializeUI();

            // Subscribe to setting changes (bidirectional sync)
            SubscribeToChanges();

            // Setup buttons
            SetupButtons();
        }

        private void InitializeUI()
        {
            if (_settings == null) return;

            // UI Settings
            if (_damagePopupToggle != null) _damagePopupToggle.isOn = _settings.ShowDamagePopup;
            if (_enemyHealthBarToggle != null) _enemyHealthBarToggle.isOn = _settings.ShowEnemyHealthBar;
            if (_autoCastUltimate != null) _autoCastUltimate.isOn = _settings.AutoCastUltimate;
            if (_autoPotion != null) _autoPotion.isOn = _settings.AutoPotion;
            if (_hpPotion != null) _hpPotion.value = _settings.HealthPotionThreshold;
            if (_mpPotion != null) _mpPotion.value = _settings.ManaPotionThreshold;
            if (_criticalTextToggle != null) _criticalTextToggle.isOn = _settings.ShowCriticalText;
            if (_healPopupToggle != null) _healPopupToggle.isOn = _settings.ShowHealPopup;
            if (_popupDurationSlider != null) _popupDurationSlider.value = _settings.PopupDuration;

            // Audio Settings
            if (_masterVolumeSlider != null) _masterVolumeSlider.value = _settings.MasterVolume;
            if (_musicVolumeSlider != null) _musicVolumeSlider.value = _settings.MusicVolume;
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = _settings.SfxVolume;
            if (_masterMuteToggle != null) _masterMuteToggle.isOn = _settings.MasterMuted;
            if (_musicMuteToggle != null) _musicMuteToggle.isOn = _settings.MusicMuted;
            if (_sfxMuteToggle != null) _sfxMuteToggle.isOn = _settings.SfxMuted;

            // Gameplay Settings
            if (_screenShakeToggle != null) _screenShakeToggle.isOn = _settings.ScreenShake;
            if (_cameraShakeToggle != null) _cameraShakeToggle.isOn = _settings.CameraShake;
            if (_vibrationToggle != null) _vibrationToggle.isOn = _settings.VibrationEnabled;
            if (_gameSpeedSlider != null) _gameSpeedSlider.value = _settings.GameSpeed;

            // Update potion panel visibility
            UpdatePotionPanelVisibility();
        }

        private void UpdatePotionPanelVisibility()
        {
            if (_objPotion != null && _autoPotion != null)
                _objPotion.SetActive(_autoPotion.isOn);
        }

        /// <summary>
        /// Subscribe to SettingsController events for bidirectional sync.
        /// When a setting changes externally, UI updates automatically.
        /// </summary>
        private void SubscribeToChanges()
        {
            if (_settings == null) return;

            // UI
            if (_damagePopupToggle != null)
                _settings.DamagePopupChanged += v => _damagePopupToggle.isOn = v;
            if (_enemyHealthBarToggle != null)
                _settings.EnemyHealthBarChanged += v => _enemyHealthBarToggle.isOn = v;
            if (_autoCastUltimate != null)
                _settings.AutoCastUltimateChanged += v => _autoCastUltimate.isOn = v;
            if (_autoPotion != null)
                _settings.AutoPotionChanged += v =>
                {
                    _autoPotion.isOn = v;
                    UpdatePotionPanelVisibility();
                };
            if (_hpPotion != null)
                _settings.HealthPotionThresholdChanged += v => _hpPotion.value = v;
            if (_mpPotion != null)
                _settings.ManaPotionThresholdChanged += v => _mpPotion.value = v;
            if (_criticalTextToggle != null)
                _settings.CriticalTextChanged += v => _criticalTextToggle.isOn = v;
            if (_healPopupToggle != null)
                _settings.HealPopupChanged += v => _healPopupToggle.isOn = v;
            if (_popupDurationSlider != null)
                _settings.PopupDurationChanged += v => _popupDurationSlider.value = v;

            // Audio
            if (_masterVolumeSlider != null)
                _settings.MasterVolumeChanged += v => _masterVolumeSlider.value = v;
            if (_musicVolumeSlider != null)
                _settings.MusicVolumeChanged += v => _musicVolumeSlider.value = v;
            if (_sfxVolumeSlider != null)
                _settings.SfxVolumeChanged += v => _sfxVolumeSlider.value = v;
            if (_masterMuteToggle != null)
                _settings.MasterMutedChanged += v => _masterMuteToggle.isOn = v;
            if (_musicMuteToggle != null)
                _settings.MusicMutedChanged += v => _musicMuteToggle.isOn = v;
            if (_sfxMuteToggle != null)
                _settings.SfxMutedChanged += v => _sfxMuteToggle.isOn = v;

            // Gameplay
            if (_screenShakeToggle != null)
                _settings.ScreenShakeChanged += v => _screenShakeToggle.isOn = v;
            if (_cameraShakeToggle != null)
                _settings.CameraShakeChanged += v => _cameraShakeToggle.isOn = v;
            if (_vibrationToggle != null)
                _settings.VibrationEnabledChanged += v => _vibrationToggle.isOn = v;
            if (_gameSpeedSlider != null)
                _settings.GameSpeedChanged += v => _gameSpeedSlider.value = v;
        }

        /// <summary>
        /// Setup button click listeners.
        /// UI only calls SettingsController properties - never touches PlayerPrefs directly.
        /// </summary>
        private void SetupButtons()
        {
            // UI Toggle listeners
            if (_damagePopupToggle != null)
                _damagePopupToggle.onValueChanged.AddListener(v => _settings.ShowDamagePopup = v);
            if (_enemyHealthBarToggle != null)
                _enemyHealthBarToggle.onValueChanged.AddListener(v => _settings.ShowEnemyHealthBar = v);
            if (_autoCastUltimate != null)
                _autoCastUltimate.onValueChanged.AddListener(v => _settings.AutoCastUltimate = v);
            if (_autoPotion != null)
                _autoPotion.onValueChanged.AddListener(v => _settings.AutoPotion = v);
            if (_hpPotion != null)
                _hpPotion.onValueChanged.AddListener(v => _settings.HealthPotionThreshold = v);
            if (_mpPotion != null)
                _mpPotion.onValueChanged.AddListener(v => _settings.ManaPotionThreshold = v);
            if (_criticalTextToggle != null)
                _criticalTextToggle.onValueChanged.AddListener(v => _settings.ShowCriticalText = v);
            if (_healPopupToggle != null)
                _healPopupToggle.onValueChanged.AddListener(v => _settings.ShowHealPopup = v);
            if (_popupDurationSlider != null)
                _popupDurationSlider.onValueChanged.AddListener(v => _settings.PopupDuration = v);

            // Audio listeners
            if (_masterVolumeSlider != null)
                _masterVolumeSlider.onValueChanged.AddListener(v => _settings.MasterVolume = v);
            if (_musicVolumeSlider != null)
                _musicVolumeSlider.onValueChanged.AddListener(v => _settings.MusicVolume = v);
            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.onValueChanged.AddListener(v => _settings.SfxVolume = v);
            if (_masterMuteToggle != null)
                _masterMuteToggle.onValueChanged.AddListener(v => _settings.MasterMuted = v);
            if (_musicMuteToggle != null)
                _musicMuteToggle.onValueChanged.AddListener(v => _settings.MusicMuted = v);
            if (_sfxMuteToggle != null)
                _sfxMuteToggle.onValueChanged.AddListener(v => _settings.SfxMuted = v);

            // Gameplay listeners
            if (_screenShakeToggle != null)
                _screenShakeToggle.onValueChanged.AddListener(v => _settings.ScreenShake = v);
            if (_cameraShakeToggle != null)
                _cameraShakeToggle.onValueChanged.AddListener(v => _settings.CameraShake = v);
            if (_vibrationToggle != null)
                _vibrationToggle.onValueChanged.AddListener(v => _settings.VibrationEnabled = v);
            if (_gameSpeedSlider != null)
                _gameSpeedSlider.onValueChanged.AddListener(v => _settings.GameSpeed = v);

            // Button listeners
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_surendButton != null) _surendButton.onClick.AddListener(Surender);
        }

        // ================================================================
        // PANEL SHOW / HIDE
        // ================================================================

        /// <summary>
        /// Show the settings panel. Called by the Settings button.
        /// </summary>
        public void ShowPanel()
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(true);
                InitializeUI(); // Refresh values when opening
            }
        }

        /// <summary>
        /// Hide the settings panel. Called by Close button.
        /// </summary>
        public void HidePanel()
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        public void Surender() => WaveManager.Instance.Defeat();

        /// <summary>
        /// Toggle panel visibility. Can be used by the Settings button.
        /// </summary>
        public void TogglePanel()
        {
            if (_settingsPanel == null) return;

            if (_settingsPanel.activeSelf)
                HidePanel();
            else
                ShowPanel();
        }

        // ================================================================
        // BUTTON ACTIONS
        // ================================================================

        private void OnResetClicked()
        {
            _settings.ResetToDefault();
            InitializeUI(); // Refresh UI after reset
        }

        private void OnDestroy()
        {
            // Cleanup button listeners
            if (_resetButton != null) _resetButton.onClick.RemoveListener(OnResetClicked);
            if (_surendButton != null) _surendButton.onClick.RemoveListener(Surender);
        }
    }
}
