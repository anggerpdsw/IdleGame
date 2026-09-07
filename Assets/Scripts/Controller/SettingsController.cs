using System;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.UI;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Centralized settings controller following SOLID principles.
    /// Uses property + event pattern (NOT a single global event).
    /// Each setting has its own property and event for fine-grained control.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        private static SettingsController _instance;
        public static SettingsController Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        // Root settings container
        private GameSettings _settings = new();

        // Version for migration support
        private const string SETTINGS_VERSION_KEY = "SettingsVersion";
        private const int CURRENT_VERSION = 1;

        // ================================================================
        // UI SETTINGS EVENTS
        // ================================================================
        public event Action<bool> DamagePopupChanged;
        public event Action<bool> EnemyHealthBarChanged;
        public event Action<bool> AutoCastUltimateChanged;
        public event Action<bool> AutoPotionChanged;
        public event Action<float> HealthPotionThresholdChanged;
        public event Action<float> ManaPotionThresholdChanged;
        public event Action<bool> CriticalTextChanged;
        public event Action<bool> HealPopupChanged;
        public event Action<bool> ShowFPSChanged;
        public event Action<float> PopupDurationChanged;

        // ================================================================
        // AUDIO SETTINGS EVENTS
        // ================================================================
        public event Action<float> MasterVolumeChanged;
        public event Action<float> MusicVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<bool> MasterMutedChanged;
        public event Action<bool> MusicMutedChanged;
        public event Action<bool> SfxMutedChanged;

        // ================================================================
        // GAMEPLAY SETTINGS EVENTS
        // ================================================================
        public event Action<bool> ScreenShakeChanged;
        public event Action<bool> CameraShakeChanged;
        public event Action<bool> AutoAimIndicatorChanged;
        public event Action<bool> VibrationEnabledChanged;
        public event Action<float> GameSpeedChanged;
        public event Action<bool> ShowDamageNumbersChanged;

        // ================================================================
        // GLOBAL EVENTS
        // ================================================================
        public event Action SettingsReset; // Fired when ResetToDefault is called

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        // ================================================================
        // UI SETTINGS PROPERTIES
        // ================================================================

        public bool ShowDamagePopup
        {
            get => _settings.ShowDamagePopup;
            set => SetProperty(ref _settings.ShowDamagePopup, value, nameof(ShowDamagePopup), DamagePopupChanged);
        }

        public bool ShowCriticalText
        {
            get => _settings.ShowCriticalText;
            set => SetProperty(ref _settings.ShowCriticalText, value, nameof(ShowCriticalText), CriticalTextChanged);
        }

        public bool ShowHealPopup
        {
            get => _settings.ShowHealPopup;
            set => SetProperty(ref _settings.ShowHealPopup, value, nameof(ShowHealPopup), HealPopupChanged);
        }

        public bool ShowEnemyHealthBar
        {
            get => _settings.ShowEnemyHealthBar;
            set => SetProperty(ref _settings.ShowEnemyHealthBar, value, nameof(ShowEnemyHealthBar), EnemyHealthBarChanged);
        }

        public bool AutoCastUltimate
        {
            get => _settings.AutoCastUltimate;
            set => SetProperty(ref _settings.AutoCastUltimate, value, nameof(AutoCastUltimate), AutoCastUltimateChanged);
        }

        public bool ShowFPS
        {
            get => _settings.ShowFPS;
            set => SetProperty(ref _settings.ShowFPS, value, nameof(ShowFPS), ShowFPSChanged);
        }

        public bool AutoPotion
        {
            get => _settings.AutoPotion;
            set => SetProperty(ref _settings.AutoPotion, value, nameof(AutoPotion), AutoPotionChanged);
        }

        public float HealthPotionThreshold
        {
            get => _settings.HealthPotionThreshold;
            set => SetProperty(ref _settings.HealthPotionThreshold, Mathf.Clamp01(value), nameof(HealthPotionThreshold), HealthPotionThresholdChanged);
        }

        public float ManaPotionThreshold
        {
            get => _settings.ManaPotionThreshold;
            set => SetProperty(ref _settings.ManaPotionThreshold, Mathf.Clamp01(value), nameof(ManaPotionThreshold), ManaPotionThresholdChanged);
        }

        public float PopupDuration
        {
            get => _settings.PopupDuration;
            set => SetProperty(ref _settings.PopupDuration, value, nameof(PopupDuration), PopupDurationChanged);
        }

        // ================================================================
        // AUDIO SETTINGS PROPERTIES
        // ================================================================

        public float MasterVolume
        {
            get => _settings.MasterVolume;
            set => SetProperty(ref _settings.MasterVolume, Mathf.Clamp01(value), nameof(MasterVolume), MasterVolumeChanged);
        }

        public float MusicVolume
        {
            get => _settings.MusicVolume;
            set => SetProperty(ref _settings.MusicVolume, Mathf.Clamp01(value), nameof(MusicVolume), MusicVolumeChanged);
        }

        public float SfxVolume
        {
            get => _settings.SfxVolume;
            set => SetProperty(ref _settings.SfxVolume, Mathf.Clamp01(value), nameof(SfxVolume), SfxVolumeChanged);
        }

        public bool MasterMuted
        {
            get => _settings.MasterMuted;
            set => SetProperty(ref _settings.MasterMuted, value, nameof(MasterMuted), MasterMutedChanged);
        }

        public bool MusicMuted
        {
            get => _settings.MusicMuted;
            set => SetProperty(ref _settings.MusicMuted, value, nameof(MusicMuted), MusicMutedChanged);
        }

        public bool SfxMuted
        {
            get => _settings.SfxMuted;
            set => SetProperty(ref _settings.SfxMuted, value, nameof(SfxMuted), SfxMutedChanged);
        }

        // ================================================================
        // GAMEPLAY SETTINGS PROPERTIES
        // ================================================================

        public bool ScreenShake
        {
            get => _settings.ScreenShake;
            set => SetProperty(ref _settings.ScreenShake, value, nameof(ScreenShake), ScreenShakeChanged);
        }

        public bool CameraShake
        {
            get => _settings.CameraShake;
            set => SetProperty(ref _settings.CameraShake, value, nameof(CameraShake), CameraShakeChanged);
        }

        public bool AutoAimIndicator
        {
            get => _settings.AutoAimIndicator;
            set => SetProperty(ref _settings.AutoAimIndicator, value, nameof(AutoAimIndicator), AutoAimIndicatorChanged);
        }

        public bool VibrationEnabled
        {
            get => _settings.VibrationEnabled;
            set => SetProperty(ref _settings.VibrationEnabled, value, nameof(VibrationEnabled), VibrationEnabledChanged);
        }

        public float GameSpeed
        {
            get => _settings.GameSpeed;
            set => SetProperty(ref _settings.GameSpeed, Mathf.Clamp(value, 0.5f, 2f), nameof(GameSpeed), GameSpeedChanged);
        }

        public bool ShowDamageNumbers
        {
            get => _settings.ShowDamageNumbers;
            set => SetProperty(ref _settings.ShowDamageNumbers, value, nameof(ShowDamageNumbers), ShowDamageNumbersChanged);
        }

        // ================================================================
        // HELPER: SetProperty with change detection & auto-save
        // ================================================================

        /// <summary>
        /// Generic setter that:
        /// 1. Checks if value actually changed
        /// 2. Updates the backing field
        /// 3. Saves to PlayerPrefs
        /// 4. Invokes the specific event
        /// </summary>
        private void SetProperty<T>(ref T backingField, T newValue, string key, Action<T> onChanged)
        {
            // Early exit if value hasn't changed
            if (Equals(backingField, newValue))
                return;

            backingField = newValue;

            // Save to PlayerPrefs
            SaveSetting(key, newValue);

            // Notify subscribers
            onChanged?.Invoke(newValue);
        }

        // ================================================================
        // LOAD / SAVE
        // ================================================================

        private void LoadSettings()
        {
            int version = PlayerPrefs.GetInt(SETTINGS_VERSION_KEY, 0);

            // UI Settings
            _settings.ShowDamagePopup = PlayerPrefs.GetInt(nameof(ShowDamagePopup), 1) == 1;
            _settings.ShowEnemyHealthBar = PlayerPrefs.GetInt(nameof(ShowEnemyHealthBar), 1) == 1;
            _settings.AutoCastUltimate = PlayerPrefs.GetInt(nameof(AutoCastUltimate), 1) == 1;
            _settings.AutoPotion = PlayerPrefs.GetInt(nameof(AutoPotion), 1) == 1;
            _settings.HealthPotionThreshold = PlayerPrefs.GetFloat(nameof(HealthPotionThreshold), 0.5f);
            _settings.ManaPotionThreshold = PlayerPrefs.GetFloat(nameof(ManaPotionThreshold), 0.5f);
            _settings.ShowCriticalText = PlayerPrefs.GetInt(nameof(ShowCriticalText), 1) == 1;
            _settings.ShowHealPopup = PlayerPrefs.GetInt(nameof(ShowHealPopup), 1) == 1;
            _settings.ShowFPS = PlayerPrefs.GetInt(nameof(ShowFPS), 0) == 1;
            _settings.PopupDuration = PlayerPrefs.GetFloat(nameof(PopupDuration), 1.5f);

            // Audio Settings
            _settings.MasterVolume = PlayerPrefs.GetFloat(nameof(MasterVolume), 1f);
            _settings.MusicVolume = PlayerPrefs.GetFloat(nameof(MusicVolume), 0.8f);
            _settings.SfxVolume = PlayerPrefs.GetFloat(nameof(SfxVolume), 0.8f);
            _settings.MasterMuted = PlayerPrefs.GetInt(nameof(MasterMuted), 0) == 1;
            _settings.MusicMuted = PlayerPrefs.GetInt(nameof(MusicMuted), 0) == 1;
            _settings.SfxMuted = PlayerPrefs.GetInt(nameof(SfxMuted), 0) == 1;

            // Gameplay Settings
            _settings.ScreenShake = PlayerPrefs.GetInt(nameof(ScreenShake), 1) == 1;
            _settings.CameraShake = PlayerPrefs.GetInt(nameof(CameraShake), 1) == 1;
            _settings.AutoAimIndicator = PlayerPrefs.GetInt(nameof(AutoAimIndicator), 1) == 1;
            _settings.VibrationEnabled = PlayerPrefs.GetInt(nameof(VibrationEnabled), 1) == 1;
            _settings.GameSpeed = PlayerPrefs.GetFloat(nameof(GameSpeed), 1f);
            _settings.ShowDamageNumbers = PlayerPrefs.GetInt(nameof(ShowDamageNumbers), 1) == 1;

            // Version handling (for future migrations)
            if (version < CURRENT_VERSION)
            {
                PlayerPrefs.SetInt(SETTINGS_VERSION_KEY, CURRENT_VERSION);
                PlayerPrefs.Save();
            }
        }

        private void SaveSetting<T>(string key, T value)
        {
            switch (value)
            {
                case bool boolValue:
                    PlayerPrefs.SetInt(key, boolValue ? 1 : 0);
                    break;
                case int intValue:
                    PlayerPrefs.SetInt(key, intValue);
                    break;
                case float floatValue:
                    PlayerPrefs.SetFloat(key, floatValue);
                    break;
                case string stringValue:
                    PlayerPrefs.SetString(key, stringValue);
                    break;
                default:
                    Debug.LogWarning($"[SettingsController] Unsupported type for key: {key}");
                    break;
            }

            PlayerPrefs.Save();
        }

        // ================================================================
        // RESET TO DEFAULT
        // ================================================================

        public void ResetToDefault()
        {
            _settings.ResetToDefault();

            // Clear PlayerPrefs
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt(SETTINGS_VERSION_KEY, CURRENT_VERSION);
            PlayerPrefs.Save();

            // Reload to trigger all events
            LoadSettings();

            // Notify all systems
            NotifyAllChanges();

            // Fire global reset event
            SettingsReset?.Invoke();

            Debug.Log("[SettingsController] Settings reset to default.");
        }

        private void NotifyAllChanges()
        {
            // UI
            DamagePopupChanged?.Invoke(_settings.ShowDamagePopup);
            EnemyHealthBarChanged?.Invoke(_settings.ShowEnemyHealthBar);
            AutoCastUltimateChanged?.Invoke(_settings.AutoCastUltimate);
            AutoPotionChanged?.Invoke(_settings.AutoPotion);
            HealthPotionThresholdChanged?.Invoke(_settings.HealthPotionThreshold);
            ManaPotionThresholdChanged?.Invoke(_settings.ManaPotionThreshold);
            CriticalTextChanged?.Invoke(_settings.ShowCriticalText);
            HealPopupChanged?.Invoke(_settings.ShowHealPopup);
            ShowFPSChanged?.Invoke(_settings.ShowFPS);
            PopupDurationChanged?.Invoke(_settings.PopupDuration);

            // Audio
            MasterVolumeChanged?.Invoke(_settings.MasterVolume);
            MusicVolumeChanged?.Invoke(_settings.MusicVolume);
            SfxVolumeChanged?.Invoke(_settings.SfxVolume);
            MasterMutedChanged?.Invoke(_settings.MasterMuted);
            MusicMutedChanged?.Invoke(_settings.MusicMuted);
            SfxMutedChanged?.Invoke(_settings.SfxMuted);

            // Gameplay
            ScreenShakeChanged?.Invoke(_settings.ScreenShake);
            CameraShakeChanged?.Invoke(_settings.CameraShake);
            AutoAimIndicatorChanged?.Invoke(_settings.AutoAimIndicator);
            VibrationEnabledChanged?.Invoke(_settings.VibrationEnabled);
            GameSpeedChanged?.Invoke(_settings.GameSpeed);
            ShowDamageNumbersChanged?.Invoke(_settings.ShowDamageNumbers);
        }

        // ================================================================
        // EXPORT / IMPORT (Bonus)
        // ================================================================

        public string ExportSettings()
        {
            return JsonConvert.SerializeObject(_settings, Formatting.Indented);
        }

        public void ImportSettings(string json)
        {
            try
            {
                _settings = JsonConvert.DeserializeObject<GameSettings>(json);
                SaveAllSettings();
                NotifyAllChanges();
                Debug.Log("[SettingsController] Settings imported successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsController] Failed to import settings: {e.Message}");
            }
        }

        private void SaveAllSettings()
        {
            // UI
            SaveSetting(nameof(ShowDamagePopup), _settings.ShowDamagePopup);
            SaveSetting(nameof(ShowEnemyHealthBar), _settings.ShowEnemyHealthBar);
            SaveSetting(nameof(AutoCastUltimate), _settings.AutoCastUltimate);
            SaveSetting(nameof(ShowCriticalText), _settings.ShowCriticalText);
            SaveSetting(nameof(ShowHealPopup), _settings.ShowHealPopup);
            SaveSetting(nameof(ShowFPS), _settings.ShowFPS);
            SaveSetting(nameof(PopupDuration), _settings.PopupDuration);

            // Audio
            SaveSetting(nameof(MasterVolume), _settings.MasterVolume);
            SaveSetting(nameof(MusicVolume), _settings.MusicVolume);
            SaveSetting(nameof(SfxVolume), _settings.SfxVolume);
            SaveSetting(nameof(MasterMuted), _settings.MasterMuted);
            SaveSetting(nameof(MusicMuted), _settings.MusicMuted);
            SaveSetting(nameof(SfxMuted), _settings.SfxMuted);

            // Gameplay
            SaveSetting(nameof(ScreenShake), _settings.ScreenShake);
            SaveSetting(nameof(CameraShake), _settings.CameraShake);
            SaveSetting(nameof(AutoAimIndicator), _settings.AutoAimIndicator);
            SaveSetting(nameof(VibrationEnabled), _settings.VibrationEnabled);
            SaveSetting(nameof(GameSpeed), _settings.GameSpeed);
            SaveSetting(nameof(ShowDamageNumbers), _settings.ShowDamageNumbers);
        }
    }
}
