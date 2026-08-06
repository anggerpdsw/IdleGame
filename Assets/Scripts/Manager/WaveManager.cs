using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Central brain for wave-based gameplay.
    /// Reads config from dataWave.json, exposes difficulty multipliers
    /// for other systems (Enemy, Spawner, etc.) to consume modularly.
    ///
    /// Flow: InterWave (10s) → ActiveWave (30s) → InterWave → ...
    /// Each wave increases difficulty up to maxWave (350).
    /// After wave 350, tier increases and wave resets to 1 (infinite progression).
    ///
    /// BALANCE GOAL: Enemy stats should scale so player can handle them with reasonable upgrades.
    /// Target: At Tier 10, Wave 350, enemy HP ~2-5x base, Damage ~1.5-2x base.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static WaveManager _instance;
        public static WaveManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        // -------------------------------------------------------------------
        // Configurable fields (fallback if JSON fails)
        // -------------------------------------------------------------------
        [SerializeField] private bool debug;
        [Header("References")]
        [Tooltip("Reference to the EnemySpawner component.")]
        [SerializeField] private EnemySpawner _enemySpawner;

        [Header("Default Config (used if dataWave.json missing)")]
        [SerializeField] private float _waveDuration = 30f;
        [SerializeField] private float _interWaveDuration = 10f;
        [SerializeField] private int _maxWave = GameConstants.MAX_WAVE_PER_TIER;
        [SerializeField] private float _tierMultiplier = 0.15f;
        [SerializeField] private float _healthMultiplier = 1.008f;
        [SerializeField] private float _speedMultiplier = 1.0015f;
        [SerializeField] private float _damageMultiplier = 1.005f;
        [SerializeField] private float _baseSpawnInterval = 1.5f;
        [SerializeField] private float _minSpawnInterval = 0.3f;
        [SerializeField] private int _minWaveSpawnDecay = 200;
        [SerializeField] private float _maxSpawnBuffer = 4.7f;
        [SerializeField] private long _waveGoldEarned;
        [SerializeField] private long _waveMeatEarned;
        [SerializeField] private long _waveExpEarned;

        // -------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------
        public int CurrentTier { get; private set; } = 1;
        public int CurrentWave { get; private set; } = 1;
        public float TimeRemaining { get; private set; }
        public WaveState State { get; private set; }
        public bool IsRunActive { get; private set; }
        public float ProgressionSpeed => 1f - CardModifierService.GetEffectResult(CardEffectType.TimeFast);
            
        // Damage stats tracking per wave
        private Dictionary<string, Dictionary<string, long>> _currentWaveDamage = new();

        // -------------------------------------------------------------------
        // Modular multipliers — accessible by any system
        // Formula: BaseStat * (Multiplier ^ clampedWave) * TierMultiplier
        // Wave clamped to [1, maxWave] so difficulty caps at wave 350.
        // Tier adds additional scaling for infinite progression.
        // -------------------------------------------------------------------
        public float HealthMult  => GetWaveHealthMultiplier();
        public float DamageMult  => GetWaveDamageMultiplier();
        public float SpeedMult   => GetWaveSpeedMultiplier();
        private float DecayCount => Utilityku.WaveDecayCalculate(CurrentBaseSpawnInterval, CurrentMinSpawnInterval, _minWaveSpawnDecay);
        public float SpawnMult   => Utilityku.WaveMultiplier(DecayCount, CurrentWave, _maxWave);
        public float SpawnBuffer => UnityEngine.Random.Range(2.1f, _maxSpawnBuffer);

        public float CurrentSpawnInterval =>
            Mathf.Max(CurrentBaseSpawnInterval * SpawnMult, CurrentMinSpawnInterval);

        /// <summary>
        /// Get max wave per tier (for external systems like EnemySpawner).
        /// </summary>
        public int GetMaxWave() => _maxWave;

        /// <summary>
        /// Get current wave damage statistics.
        /// </summary>
        public Dictionary<string, Dictionary<string, long>> CurrentWaveDamage => _currentWaveDamage;

        public static event Action<long, long> OnWaveBonusReward;

        // -------------------------------------------------------------------
        // Unity callbacks
        // -------------------------------------------------------------------
        private void Awake()
        {
            // Initialize singleton
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!IsRunActive) return;

            TimeRemaining -= Time.deltaTime;

            if (TimeRemaining > 0f) return;

            SkipToNextWave();
        }

        public void RegisterSpawner(EnemySpawner spawner) => _enemySpawner = spawner;

        /// <summary>
        /// Record damage dealt to an enemy in current wave.
        /// </summary>
        public void RecordDamage(string enemyId, string damageSource, float amount)
        {
            if (!_currentWaveDamage.ContainsKey(enemyId))
                _currentWaveDamage[enemyId] = new Dictionary<string, long>();

            if (!_currentWaveDamage[enemyId].ContainsKey(damageSource))
                _currentWaveDamage[enemyId][damageSource] = 0;

            _currentWaveDamage[enemyId][damageSource] += Mathf.RoundToInt(amount);
        }

        /// <summary>
        /// Reset damage statistics for a new wave.
        /// Called when wave 1 starts.
        /// </summary>
        public void ResetDamageStats() => _currentWaveDamage.Clear();

        /// <summary>
        /// Called by MainMenu to begin a gameplay session with the selected tier.
        /// Sets the current tier and resets wave state.
        /// </summary>
        public void InitializeRun(int selectedTier)
        {
            IsRunActive = true;
            CurrentTier = Mathf.Max(1, selectedTier);
            SaveManager.Instance.RecordRun(CurrentTier);
            ResetWave();
        }

        private void ResetWave()
        {
            CurrentWave = 1;

            _waveGoldEarned = 0;
            _waveMeatEarned = 0;
            _waveExpEarned  = 0;

            ResetDamageStats();

            State = WaveState.ActiveWave;
            TimeRemaining = CurrentWaveDuration;

            ApplySpawnData();
        }

        /// <summary>
        /// Push current spawn interval to EnemySpawner.
        /// This is the ONLY place where we write to EnemySpawner.SpawnInterval.
        /// </summary>
        public void ApplySpawnData()
        {
            if (_enemySpawner == null) return;
            _enemySpawner.enabled = true;
            _enemySpawner.SpawnInterval = CurrentSpawnInterval;

            _healthMultiplier = HealthMult;
            _speedMultiplier = SpeedMult;
            _damageMultiplier = DamageMult;
        }

        private void StartNextWave()
        {
            CurrentWave++;
            // Check if we reached the end of current tier (wave 350)
            if (CurrentWave > _maxWave)
            {
                // Munculkan Popup Clear Tier dan stop game saat ini supaya kembali ke main menu untuk pindah Tier selanjutnya sebelum memulai wave lagi dari 1
                // Alurnya Wave350 → Run Complete → Victory UI → Return Main Menu
                Victory();
                return;
            }
            
            State = WaveState.ActiveWave;
            TimeRemaining = CurrentWaveDuration;
            ApplySpawnData();
        }

        public static event Action<VictoryData> OnRunCompleted;
        private void Victory()
        {
            State = WaveState.Victory;
            var result = BuildData(State);
            OnRunCompleted?.Invoke(result);
            SaveManager.Instance.CompleteTier(CurrentTier);
            EndRun();
        }
        public void Defeat()
        {
            State = WaveState.Defeat;
            var result = BuildData(State);
            OnRunCompleted?.Invoke(result);
            EndRun();
        }
        private VictoryData BuildData(WaveState waveState)
        {
            long bonusGold = Utilityku.WaveBonusVictory(_waveGoldEarned, CurrentTier, CurrentWave);
            long bonusMeat = Utilityku.WaveBonusVictory(_waveMeatEarned, CurrentTier, CurrentWave);
            return new VictoryData {
                State = waveState,
                Tier = CurrentTier,
                HighestWave = CurrentWave,
                GoldEarned  = _waveGoldEarned,
                MeatEarned  = _waveMeatEarned,
                ExpEarned   = _waveExpEarned,
                BonusGold   = bonusGold,
                BonusMeat   = bonusMeat
            };
        }
        public void EndRun()
        {
            _enemySpawner.enabled = false;
            IsRunActive = false;
            SaveManager.Instance.RecordHighestGoldMeatExp(CurrentTier, _waveGoldEarned, _waveMeatEarned, _waveExpEarned);
            SceneLoader.Instance.ResetGlobalState();
        }

        private void CompleteWave()
        {
            // Record progress before transitioning to inter‑wave
            SaveManager.Instance.UpdateHighestWave(CurrentTier, CurrentWave);

            WaveBonusInterest();

            State = WaveState.InterWave;
            _enemySpawner.enabled = false;
            TimeRemaining = CurrentInterWaveDuration;
        }

        // -------------------------------------------------------------------
        // JSON config
        // -------------------------------------------------------------------
        public void LoadWaveData()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/dataWave");
            if (json == null)
            {
                if(debug) Debug.LogWarning("[WaveManager] dataWave.json not found, using Inspector defaults.");
                return;
            }

            var config = JsonConvert.DeserializeObject<WaveData>(json.text);
            if (config == null) return;

            _waveDuration = config.waves.waveDuration;
            _interWaveDuration = config.waves.interWaveDuration;
            _maxWave = config.waves.maxWave;

            _tierMultiplier = config.difficulty.tierMultiplier;
            _healthMultiplier = config.difficulty.healthMultiplier;
            _speedMultiplier = config.difficulty.speedMultiplier;
            _damageMultiplier = config.difficulty.damageMultiplier;

            _baseSpawnInterval = config.spawning.baseSpawnInterval;
            _minSpawnInterval = config.spawning.minSpawnInterval;
            _minWaveSpawnDecay = config.spawning.minWaveSpawnDecay;
            _maxSpawnBuffer = config.spawning.maxSpawnBuffer;
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------
        public WaveInfo GetWaveInfo() => new()
        {
            TierNumber = CurrentTier,
            WaveNumber = CurrentWave,
            State = State,
            TimeRemaining = TimeRemaining,
            SpawnInterval = CurrentSpawnInterval,
            WaveDuration = CurrentWaveDuration,
            InterWaveDuration = CurrentInterWaveDuration,
            HealthMult = HealthMult,
            SpeedMult = SpeedMult,
            DamageMult = DamageMult
        };

        /// <summary>
        /// Skip to next wave (debug/cheat).
        /// </summary>
        public void SkipToNextWave()
        {
            if (State == WaveState.InterWave)
                StartNextWave();
            else if (State == WaveState.ActiveWave)
                CompleteWave();
        }

        public void RecordGold(long amount) => _waveGoldEarned += amount;
        public void RecordMeat(long amount) => _waveMeatEarned += amount;
        public void RecordExp(long amount) => _waveExpEarned += amount;
        private void WaveBonusInterest()
        {
            float bonusPercent = PlayerStatsManager.Instance.GetStat(SkillType.InterestWave);

            long bonusGold = Utilityku.WaveBonusInterest(CurrencyType.Gold, _waveGoldEarned, bonusPercent, CurrentTier);
            long bonusMeat = Utilityku.WaveBonusInterest(CurrencyType.Meat, _waveMeatEarned, bonusPercent, CurrentTier);

            OnWaveBonusReward?.Invoke(bonusGold, bonusMeat);

            EconomyManager.Instance.AddCurrency(CurrencyType.Gold, bonusGold);
            EconomyManager.Instance.AddCurrency(CurrencyType.Meat, bonusMeat);

            // _waveGoldEarned = 0;
            // _waveMeatEarned = 0; karena sudah di limit jadi gak perlu reset
        }

        /// <summary>
        /// Wave progress from 0 (wave 1) to 1 (wave 350).
        /// </summary>
        private float GetWaveProgressMultiplier() => Mathf.Clamp01((CurrentWave - 1f) / (_maxWave - 1f));
        private float GetTierMultiplier() => 1f + (CurrentTier - 1) * _tierMultiplier;
        private float GetWaveSpeedMultiplier()
        {
            return Mathf.Lerp(1f, 1.413f, GetWaveProgressMultiplier());
        }
        private float GetWaveHealthMultiplier()
            => Mathf.Lerp(1f, 2.37f, Mathf.Pow(GetWaveProgressMultiplier(), 1.2f)) * GetTierMultiplier();
        private float GetWaveDamageMultiplier() => 
            Mathf.Lerp(1f, 1.15f, Mathf.Pow(GetWaveProgressMultiplier(), 1.2f)) * GetTierMultiplier();

        public float CurrentWaveDuration => _waveDuration * ProgressionSpeed;
        public float CurrentInterWaveDuration => _interWaveDuration * ProgressionSpeed;
        public float CurrentBaseSpawnInterval => _baseSpawnInterval * ProgressionSpeed;
        public float CurrentMinSpawnInterval => _minSpawnInterval * ProgressionSpeed;

        private void OnEnable() => SaveManager.OnSaveLoaded += LoadWaveData;
        private void OnDisable() => SaveManager.OnSaveLoaded -= LoadWaveData;

        // -------------------------------------------------------------------
        // Editor Gizmos
        // -------------------------------------------------------------------
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                Vector3 labelPos = transform.position + Vector3.up * 2f;
                UnityEditor.Handles.Label(labelPos, $"Wave {CurrentWave} ({State})\nHP×{HealthMult:F1} SPD×{SpeedMult:F1} DMG×{DamageMult:F1} Duration: {CurrentWaveDuration} Interwave: {CurrentInterWaveDuration}");
            }
        }
#endif
    }
}