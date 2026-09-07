using System;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Root container for all game settings.
    /// Used for JSON export/import and default values.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        // ============================================================
        // UI SETTINGS
        // ============================================================
        public bool ShowDamagePopup = true;
        public bool ShowCriticalText = true;
        public bool ShowHealPopup = true;
        public bool ShowEnemyHealthBar = true;
        public bool AutoCastUltimate = true;
        public bool AutoPotion = true;
        public float HealthPotionThreshold = 0.5f;
        public float ManaPotionThreshold = 0.5f;
        public bool ShowFPS = false;
        public float PopupDuration = 1.5f;

        // ============================================================
        // AUDIO SETTINGS
        // ============================================================
        public float MasterVolume = 1f;
        public float MusicVolume = 0.8f;
        public float SfxVolume = 0.8f;
        public bool MasterMuted;
        public bool MusicMuted;
        public bool SfxMuted;

        // ============================================================
        // GAMEPLAY SETTINGS
        // ============================================================
        public bool ScreenShake = true;
        public bool CameraShake = true;
        public bool AutoAimIndicator = true;
        public bool VibrationEnabled = true;
        public float GameSpeed = 1f;
        public bool ShowDamageNumbers = true;

        /// <summary>
        /// Reset all settings to default values.
        /// </summary>
        public void ResetToDefault()
        {
            // UI
            ShowDamagePopup = true;
            ShowEnemyHealthBar = true;
            AutoCastUltimate = true;
            AutoPotion = true;
            HealthPotionThreshold = 0.5f;
            ManaPotionThreshold = 0.5f;
            ShowCriticalText = true;
            ShowHealPopup = true;
            ShowFPS = false;
            PopupDuration = 1.5f;

            // Audio
            MasterVolume = 1f;
            MusicVolume = 0.8f;
            SfxVolume = 0.8f;
            MasterMuted = false;
            MusicMuted = false;
            SfxMuted = false;

            // Gameplay
            ScreenShake = true;
            CameraShake = true;
            AutoAimIndicator = true;
            VibrationEnabled = true;
            GameSpeed = 1f;
            ShowDamageNumbers = true;
        }
    }
}
