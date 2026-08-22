// Updated SaveManager: removed current tier runtime management; tier selection handled by MainMenu and WaveManager.
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Economy;
using System;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Save;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Mission;
using System.Linq;
// ponytail: CraftTransactionJournal removed; re-add when journal feature returns.

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Centralized save/load system using JSON files.
    /// All game data (Currency, Upgrades, GameState) saved to single file.
    /// Uses Application.persistentDataPath for cross-platform compatibility.
    /// </summary>
    public class SaveManager : MonoBehaviour, ISaveService
    {
        private static WaitForSeconds _waitForSeconds0_1 = new(0.1f);

        /// <summary>
        /// Event fired when save data has finished loading.
        /// </summary>
        public static event Action OnSaveLoaded;

        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------

        private static SaveManager _instance;
        public static SaveManager Instance => _instance;

        public bool IsSaveLoaded { get; private set; } = false;

        // Craft journal removed – not needed for current work.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
            OnSaveLoaded = null;
            // Note: IsSaveLoaded/_isLoading are instance, not static
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            IsSaveLoaded = false;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Auto-load save data when game starts
            // Use a coroutine to wait until essential singletons are initialized.
            StartCoroutine(DelayedLoadAll());
        }

        private IEnumerator DelayedLoadAll()
        {
            // Small initial delay to allow Awake methods to finish.
            yield return _waitForSeconds0_1;
            // Wait until EconomyManager is available.
            while (EconomyManager.Instance == null)
                yield return null;
            LoadAll();
        }

        private void Update()
        {
            // Accumulate real-time playtime (unscaled) - not affected by game speed
            _sessionPlayTime += Time.unscaledDeltaTime;

            if (!_autoSaveEnabled) return;

            // Don't auto-save until load completes and all systems are initialized
            if (!IsSaveLoaded) return;

            // Check if any system is dirty and auto-save immediately
            // (flags accumulate while load was blocked, so this flush also covers them)
            if (_cardInventoryDirty || _inventoryDirty || _equipmentDirty)
            {
                SaveAll();
                _cardInventoryDirty = false;
                _inventoryDirty = false;
                _equipmentDirty = false;
                _autoSaveTimer = 0f; // Reset timer since we just saved
            }
            else
            {
                _autoSaveTimer += Time.deltaTime;
                if (_autoSaveTimer >= _autoSaveInterval)
                {
                    SaveAll();
                    _autoSaveTimer = 0f;
                }
            }
        }

        // -------------------------------------------------------------------
        // File Configuration
        // -------------------------------------------------------------------
        // Lazy initialization - compute at runtime, not at class load
        private static string _saveDirCache;
        private static string _saveFileCache;
        private static string SaveDir
        {
            get
            {
                if (string.IsNullOrEmpty(_saveDirCache))
                {
                    _saveDirCache = Path.Combine(Application.persistentDataPath);
                }
                return _saveDirCache;
            }
        }
        private static string SaveFile
        {
            get
            {
                if (string.IsNullOrEmpty(_saveFileCache))
                {
                    _saveFileCache = Path.Combine(SaveDir, "SaveData.json");
                }
                return _saveFileCache;
            }
        }

        // -------------------------------------------------------------------
        // Auto-save Configuration
        // -------------------------------------------------------------------
        [Header("Auto-save Settings")]
        [Tooltip("Enable automatic saving at intervals")]
        [SerializeField] private bool _autoSaveEnabled = false;
        [Tooltip("Auto-save interval in seconds")]
        [SerializeField] private float _autoSaveInterval = 60f;

        // Auto-save timer
        private float _autoSaveTimer = 0f;
        private float _sessionPlayTime = 0f; // Accumulate real-time playtime this session (unscaled)
        private bool _isLoading = false;

        // -------------------------------------------------------------------
        // Default Data to Save
        // -------------------------------------------------------------------
        private AccountData _currentAccount = new();
        private VipData _currentVip = new();
        private GameStateData _currentGameState = new();
        private WaveProgressData _currentWaveProgress = new();
        private IdleRewardData _currentIdleReward = new();
        private DailyRewardSaveData _currentDailyReward = new();

        // Card inventory dirty flag for auto-save optimization
        private bool _cardInventoryDirty = false;
        private CardInventoryData _cardInventoryData = new();

        // New Inventory & Equipment systems dirty flags
        private bool _inventoryDirty = false;
        private bool _equipmentDirty = false;

        /// <summary>
        /// True while LoadAll is running. All dirty flags and SaveAll are blocked.
        /// </summary>
        private bool CanSave => !_isLoading && IsSaveLoaded;

        public void MarkInventoryDirty() { if (CanSave) _inventoryDirty = true; }
        public void MarkEquipmentDirty() { if (CanSave) _equipmentDirty = true; }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Save all game data to JSON file.
        /// </summary>
        public void SaveAll()
        {
            if (!CanSave) return; // Block saves during load and before load completes
            try
            {
                var saveData = GatherAllData();
                PersistDurably(saveData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Gather fresh runtime state (including journal) and durably persist (I-17).
        /// Used by CraftTransactionService at each checkpoint so no stale SaveData is written.
        /// Throws IOException on filesystem failure so the transaction caller can react.
        ///</summary>
        public void PersistCurrentStateDurably()
        {
            var data = GatherAllData();
            PersistDurably(data);
        }

        public void MarkCardInventoryDirty()
        {
            if (CanSave) _cardInventoryDirty = true;
        }

        // Helper for CardManager pity counters (persistence layer)
        public int GetInt(string key)
        {
            return key switch
            {
                "PityEpic" => _cardInventoryData.rollsSinceEpic,
                "PityLegendary" => _cardInventoryData.rollsSinceLegendary,
                "PityMythic" => _cardInventoryData.rollsSinceMythic,
                _ => 0,
            };
        }

        public void SetInt(string key, int value)
        {
            switch (key)
            {
                case "PityEpic": _cardInventoryData.rollsSinceEpic = value; break;
                case "PityLegendary": _cardInventoryData.rollsSinceLegendary = value; break;
                case "PityMythic": _cardInventoryData.rollsSinceMythic = value; break;
            }
            _cardInventoryDirty = true;
        }

        /// <summary>
        /// Load all game data from JSON file.
        /// </summary>
        public void LoadAll()
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                if (!File.Exists(SaveFile))
                {
                    Debug.Log("[SaveManager] No save file found. Creating initial save with current game state.");
                    var initialData = new SaveData
                    {
                        currency = GatherCurrency()
                    };
                    PersistDurably(initialData);
                    Debug.Log("[SaveManager] Initial save created.");
                    NotifySaveLoaded();
                    AccountManager.Instance?.NotifyDataLoaded();
                    return;
                }

                var saveData = LoadFromFile();
                if (saveData == null)
                {
                    // Broken or empty file: keep it for manual recovery, then start fresh.
                    if (new FileInfo(SaveFile).Length == 0)
                        File.Delete(SaveFile);
                    else
                        File.Move(SaveFile, SaveFile + ".corrupt");
                    Debug.LogWarning("[SaveManager] Save file corrupted or empty - starting fresh save.");
                    var initialData = new SaveData
                    {
                        currency = GatherCurrency()
                    };
                    PersistDurably(initialData);
                    Debug.Log("[SaveManager] Initial save created.");
                    NotifySaveLoaded();
                    AccountManager.Instance?.NotifyDataLoaded();
                    return;
                }

                // Validate + migrate + repair old save files before applying
                UpgradeSave(saveData);

                ApplyAllData(saveData);
                NotifySaveLoaded();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to load: {e.Message}\n{e.StackTrace}");
                NotifySaveLoaded();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void NotifySaveLoaded()
        {
            IsSaveLoaded = true;
            OnSaveLoaded?.Invoke();
        }

        /// <summary>
        /// Migrate old save files to the current version and repair missing fields.
        /// New fields are filled with defaults; existing data is never touched.
        /// </summary>
        private void UpgradeSave(SaveData data)
        {
            if (data == null) return;

            // Repair missing fields for any version
            data.account ??= new AccountData();
            data.currency ??= new CurrencyData();
            data.spending ??= new SpendingData();
            data.vip ??= new VipData();
            data.gameState ??= new GameStateData();
            data.waveProgress ??= new WaveProgressData();
            data.idleReward ??= new IdleRewardData();
            data.dailyReward ??= new DailyRewardSaveData();
            data.cardInventory ??= new CardInventoryData();
            data.inventory ??= new Dictionary<string, long>();
            data.missions ??= new List<MissionInstance>();
        }

        public void DeleteAll()
        {
            try
            {
                if (File.Exists(SaveFile))
                {
                    File.Delete(SaveFile);
                    Debug.Log("[SaveManager] Save file deleted.");
                }

                // Reset back to Initial
                _currentAccount     = new AccountData();
                var economyManager  = EconomyManager.Instance;
                if (economyManager != null)
                    economyManager.SetCurrencyData(new CurrencyData());
                _spending = new SpendingData();
                _currentVip         = new VipData();
                _currentGameState   = new GameStateData();
                _currentWaveProgress = new WaveProgressData();
                _currentIdleReward  = new IdleRewardData();
                _currentDailyReward = new DailyRewardSaveData();
                _cardInventoryData  = new CardInventoryData();

                Debug.Log("[SaveManager] All data reset.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to delete save: {e.Message}");
            }
        }

        /// <summary>
        /// Get directory where saves are stored.
        /// </summary>
        public static string GetSaveDirectory() => SaveDir;
        /// <summary>
        /// Get full path to save file.
        /// </summary>
        public static string GetSaveFilePath() => SaveFile;

        private void EnsureTierExists(int tier)
        {
            while (_currentWaveProgress.Tiers.Count < tier)
            {
                _currentWaveProgress.Tiers.Add(new TierProgress
                {
                    Tier = _currentWaveProgress.Tiers.Count + 1
                });
            }
        }

        private TierProgress GetTier(int tier)
        {
            if (tier <= 0) throw new ArgumentOutOfRangeException(nameof(tier));
            EnsureTierExists(tier);
            return _currentWaveProgress.Tiers[tier - 1];
        }

        public int GetHighestWave(int tier) => GetTier(tier).HighestWave;

        public int GetHighestUnlockedTier()
        {
            int tier = 1;
            // Increment while the next tier is unlocked
            while (IsTierUnlocked(tier + 1)) tier++;
            return tier;
        }

        public bool IsTierUnlocked(int tier)
        {
            if (tier <= 1) return true;
            int index = tier - 2;
            if (index >= _currentWaveProgress.Tiers.Count) return false;
            return _currentWaveProgress.Tiers[index].Cleared;
        }

        public void UpdateHighestWave(int tier, int wave)
        {
            TierProgress data = GetTier(tier);
            if (wave > data.HighestWave) data.HighestWave = wave;
            SaveAll();
        }

        public void CompleteTier(int tier)
        {
            GetTier(tier).Cleared = true;
            SaveAll();
        }

        public void RecordRun(int tier)
        {
            GetTier(tier).TotalRuns++;
            SaveAll();
        }

        public void AddKills(int tier, int amount) => GetTier(tier).TotalKills += amount;

        public void RecordHighestGoldMeatExp(int tier, long gold, long meat, long exp)
        {
            TierProgress data = GetTier(tier);
            if (gold > data.HighestGoldEarned) data.HighestGoldEarned = gold;
            if (meat > data.HighestMeatEarned) data.HighestMeatEarned = meat;
            if (exp > data.HighestExpEarned) data.HighestExpEarned = exp;
        }

        public long GetHighestGoldEarned()
        {
            long best = 0L;
            foreach (var tier in _currentWaveProgress.Tiers)
            {
                if (tier.HighestGoldEarned > best) best = tier.HighestGoldEarned;
            }
            return best;
        }

        public long GetHighestMeatEarned()
        {
            long best = 0L;
            foreach (var tier in _currentWaveProgress.Tiers)
            {
                if (tier.HighestMeatEarned > best) best = tier.HighestMeatEarned;
            }
            return best;
        }

        public long GetHighestExpEarned()
        {
            long best = 0L;
            foreach (var tier in _currentWaveProgress.Tiers)
            {
                if (tier.HighestExpEarned > best) best = tier.HighestExpEarned;
            }
            return best;
        }

        public void RecordEnemyKill(string enemyId, string damageSource, string role)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            _currentGameState.totalEnemiesKilled ??= new Dictionary<string, Dictionary<string, Dictionary<string, long>>>();

            if (!_currentGameState.totalEnemiesKilled.TryGetValue(role, out var roleGroup))
            {
                roleGroup = new Dictionary<string, Dictionary<string, long>>();
                _currentGameState.totalEnemiesKilled[role] = roleGroup;
            }

            if (!roleGroup.TryGetValue(enemyId, out var sources))
            {
                sources = new Dictionary<string, long>();
                roleGroup[enemyId] = sources;
            }

            if (sources.TryGetValue(damageSource, out var count))
                sources[damageSource] = count + 1;
            else
                sources[damageSource] = 1;

            SaveAll();
        }


        // -------------------------------------------------------------------
        // Daily gem limit handling (unchanged)
        // -------------------------------------------------------------------
        public bool HasReachedDailyGemLimit()
        {
            CheckAndResetDailyCounter();
            return _currentGameState.dailyGemsEarned >= 20;
        }

        public int GetRemainingDailyGems()
        {
            CheckAndResetDailyCounter();
            return Math.Max(0, 20 - _currentGameState.dailyGemsEarned);
        }

        public int RecordGemDrop(int gemCount)
        {
            CheckAndResetDailyCounter();

            if (_currentGameState.dailyGemsEarned >= 20)
                return 0; // Limit already reached

            int available = 20 - _currentGameState.dailyGemsEarned;
            int awarded = Math.Min(gemCount, available);

            _currentGameState.dailyGemsEarned += awarded;

            // Auto-save when significant change
            SaveAll();

            return awarded;
        }

        public int GetTodaysGemEarnings() => _currentGameState.dailyGemsEarned;

        [ContextMenu("Reset Daily Gem Counter")]
        public void ResetDailyGemCounter()
        {
            _currentGameState.dailyGemsEarned = 0;
            _currentGameState.dailyResetDate = DateTime.Now.ToString(GameConstants.DATE_FORMAT);
            SaveAll();
            Debug.Log("[SaveManager] Daily gem counter reset.");
        }

        private void CheckAndResetDailyCounter()
        {
            string today = DateTime.UtcNow.ToString(GameConstants.DATE_FORMAT);
            if (_currentGameState.dailyResetDate != today)
            {
                _currentGameState.dailyGemsEarned = 0;
                _currentGameState.dailyResetDate = today;
                DailyRewardManager.Instance?.HandleDailyReset(DateTime.UtcNow);
            }
        }

        // -------------------------------------------------------------------
        // VIP Data API (unchanged)
        // -------------------------------------------------------------------
        public void SetDaily(bool enabled)
        {
            _currentVip.daily = enabled;
            SaveAll();
        }
        public void SetMaxSpeed(bool enabled)
        {
            _currentVip.maxSpeed = enabled;
            SaveAll();
        }
        public void SetAutoCollect(bool enabled)
        {
            _currentVip.autoCollect = enabled;
            SaveAll();
        }
        public bool IsDailyEnabled() => _currentVip.daily;
        public bool IsMaxSpeedEnabled() => _currentVip.maxSpeed;
        public bool IsAutoCollectEnabled() => _currentVip.autoCollect;

        // -------------------------------------------------------------------
        // Accessors for idle reward data
        // -------------------------------------------------------------------
        public AccountData GetAccountData() => _currentAccount;
        public VipData GetVipData() => _currentVip;
        public IdleRewardData GetIdleRewardData() => _currentIdleReward;
        public DailyRewardSaveData GetDailyRewardData() => _currentDailyReward;
        public void SetIdleRewardData(IdleRewardData data)
        {
            _currentIdleReward = data;
        }

        public void SaveDailyRewardData(DailyRewardSaveData data)
        {
            _currentDailyReward = data ?? new DailyRewardSaveData();
            SaveAll();
        }
        
        // -------------------------------------------------------------------
        // File I/O (unchanged)
        // -------------------------------------------------------------------
        /// <summary>
        /// Synchronous durable write (I-17). Atomic via temp file + move.
        /// Throws IOException on filesystem failure.
        ///</summary>
        public void PersistDurably(SaveData data)
        {
            if (!Directory.Exists(SaveDir)) Directory.CreateDirectory(SaveDir);
            string json = JsonConvert.SerializeObject(data, Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters = { new CustomDataConverter() }
                });
            string tempPath = SaveFile + ".tmp";
            File.WriteAllText(tempPath, json);
            // Replace existing file atomically; fall back to Move if destination doesn't exist yet
            if (File.Exists(SaveFile))
                File.Replace(tempPath, SaveFile, null);
            else
                File.Move(tempPath, SaveFile);
        }

        private SaveData LoadFromFile()
        {
            if (!File.Exists(SaveFile)) return null;
            string json = File.ReadAllText(SaveFile);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<SaveData>(json,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Converters = { new CustomDataConverter() }
                    });
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] JSON parse error: {e.Message}");
                return null;
            }
        }

        // -------------------------------------------------------------------
        // Data Collection & Application
        // -------------------------------------------------------------------
        private SaveData GatherAllData()
        {
            var account = _currentAccount;
            var currency = GatherCurrency();
            var spending = GatherSpendingData();
            var vip = GatherVipData();
            var gameState = GatherGameState();
            var waveProgress = _currentWaveProgress;
            var idleReward = _currentIdleReward;
            var dailyReward = _currentDailyReward;
            var cardInventory = GatherCardInventoryData();
            // Single source of truth for items: InventoryService slots (includes equipment items).
            var inventoryData = InventoryService.Instance != null ? InventoryService.Instance.GetSaveData() : null;
            var equipmentData = EquipmentService.Instance != null ? EquipmentService.Instance.GetSaveData() : null;
            var craftQueue = CraftingManager.Instance != null ? CraftingManager.Instance.GetQueueSaveData() : null;
            var missions = MissionService.Instance != null ? MissionService.Instance.GetAllMissions().ToList() : new List<MissionInstance>();

            return new SaveData
            {
                version = GameConstants.CURRENT_SAVE_VERSION,
                saveTimestamp = DateTime.Now.Ticks,
                account = account,
                currency = currency,
                spending = spending,
                vip = vip,
                gameState = gameState,
                waveProgress = waveProgress,
                idleReward = idleReward,
                dailyReward = dailyReward,
                inventory = null,
                cardInventory = cardInventory,
                inventoryData = inventoryData,
                equipmentData = equipmentData,
                craftQueue = craftQueue,
                missions = missions
            };
        }

        private CurrencyData GatherCurrency()
        {
            var economy = EconomyManager.Instance;
            if (economy == null)
            {
                Debug.LogError("[SaveManager] ❌ EconomyManager.Instance is NULL! Currency will NOT be saved!");
                return new CurrencyData();
            }
            return economy.GetCurrencyData();
        }

        private SpendingData _spending = new();
        private SpendingData GatherSpendingData() => _spending;
        private VipData GatherVipData() => _currentVip;
        private GameStateData GatherGameState()
        {
            return new GameStateData
            {
                lastSaveTime = DateTime.Now,
                dailyGemsEarned = _currentGameState.dailyGemsEarned,
                dailyResetDate = _currentGameState.dailyResetDate,
                totalEnemiesKilled = _currentGameState.totalEnemiesKilled ?? new Dictionary<string, Dictionary<string, Dictionary<string, long>>>(),
                totalPlayTime = _currentGameState.totalPlayTime + _sessionPlayTime
                // Wave progress is stored separately in _currentWaveProgress
            };
        }

        private CardInventoryData GatherCardInventoryData()
        {
            var cardInventory = new CardInventoryData();
            if (CardInventory.Instance != null)
            {
                var savedCards = CardInventory.Instance.GetSaveData();
                cardInventory.ownedCards = new Dictionary<string, OwnedCardData>(savedCards.Count);
                foreach (var (id, card) in savedCards)
                {
                    cardInventory.ownedCards[id] = new OwnedCardData
                    {
                        CardId = card.CardId,
                        Level = card.Level,
                        DuplicateCount = card.DuplicateCount
                    };
                }
                cardInventory.equippedCards = CardEquipmentService.Instance?.GetSaveData() ?? new List<string>();
            }

            if (_cardInventoryData != null)
            {
                cardInventory.rollsSinceEpic = _cardInventoryData.rollsSinceEpic;
                cardInventory.rollsSinceLegendary = _cardInventoryData.rollsSinceLegendary;
                cardInventory.rollsSinceMythic = _cardInventoryData.rollsSinceMythic;
            }

            return cardInventory;
        }

        private void ApplyAllData(SaveData data)
        {
            ApplyAccount(data.account);
            ApplyCurrency(data.currency);
            ApplySpendingData(data.spending);
            ApplyVipData(data.vip);
            ApplyDailyRewardData(data.dailyReward);
            ApplyGameState(data.gameState);
            ApplyWaveProgress(data.waveProgress);
            ApplyIdleRewardData(data.idleReward);
            ApplyCardInventory(data.cardInventory);
            ApplyInventoryData(data.inventoryData);
            ApplyEquipmentData(data.equipmentData);

            // Restore craft queue (after InventoryService loaded, for offline progress)
            if (CraftingManager.Instance != null && data.craftQueue != null)
                CraftingManager.Instance.LoadQueueSaveData(data.craftQueue);

            AccountManager.Instance?.NotifyDataLoaded();
        }

        private void ApplyCardInventory(CardInventoryData data)
        {
            if (data == null) return;

            if (CardInventory.Instance != null)
            {
                // Convert OwnedCardData to OwnedCard
                var ownedCards = new Dictionary<string, OwnedCardData>();
                foreach (var kvp in data.ownedCards)
                {
                    ownedCards[kvp.Key] = new OwnedCardData
                    {
                        CardId = kvp.Value.CardId,
                        Level = kvp.Value.Level,
                        DuplicateCount = kvp.Value.DuplicateCount
                    };
                }
                CardInventory.Instance.LoadInventory(ownedCards);
            }

            if (CardEquipmentService.Instance != null)
                CardEquipmentService.Instance.LoadEquipment(data.equippedCards);

            // Restore pity counters
            _cardInventoryData = new CardInventoryData
            {
                rollsSinceEpic = data.rollsSinceEpic,
                rollsSinceLegendary = data.rollsSinceLegendary,
                rollsSinceMythic = data.rollsSinceMythic
            };

            // Apply card modifiers from equipped cards after loading inventory and equipment
            CardModifierService.Refresh();

            // Fire global inventory changed event after load
            CardManager.NotifyInventoryChanged();
        }

        private void ApplyAccount(AccountData data) => 
            _currentAccount = data ?? _currentAccount;

        private void ApplyCurrency(CurrencyData currency)
        {
            var economy = EconomyManager.Instance;
            if (economy != null) economy.SetCurrencyData(currency);
        }

        private void ApplySpendingData(SpendingData data) => 
            _spending = data ?? _spending;

        private void ApplyVipData(VipData data) => 
            _currentVip = data ?? _currentVip;

        private void ApplyGameState(GameStateData data)
        {
            _currentGameState = data ?? _currentGameState;
            _sessionPlayTime = 0f;
            CheckAndResetDailyCounter();
        }

        private void ApplyWaveProgress(WaveProgressData data) => 
            _currentWaveProgress = data ?? _currentWaveProgress;

        private void ApplyIdleRewardData(IdleRewardData data) =>
            _currentIdleReward = data ?? _currentIdleReward;

        private void ApplyDailyRewardData(DailyRewardSaveData data)
        {
            _currentDailyReward = data ?? _currentDailyReward;
        }

        private void ApplyInventoryData(InventorySaveData data)
        {
            InventoryService.Instance?.LoadFromSaveData(data);

            // Rehydrate socketed gem instances AFTER items exist (SocketData.GemInstanceId is a
            // reference; the GemInstanceData itself lives in SaveData.SocketedGems, owned by
            // GemService — level/experience survive restarts).
            if (GemService.Instance != null && InventoryService.Instance != null)
            {
                GemService.Instance.LoadSocketedGems(data?.SocketedGems);
                GemService.Instance.RestoreSocketedGems(InventoryService.Instance.AllItems);
            }
        }

        private void ApplyEquipmentData(EquipmentSaveData data) =>
            EquipmentService.Instance?.LoadFromSaveData(data);

        // -------------------------------------------------------------------
        // Statistics & Info (unchanged)
        // -------------------------------------------------------------------
        public void AddSpending(CurrencyType type, long amount)
        {
            switch (type)
            {
                case CurrencyType.Gold: _spending.totalGoldSpent += amount; break;
                case CurrencyType.Gem: _spending.totalGemSpent += amount; break;
                case CurrencyType.Meat: _spending.totalMeatSpent += amount; break;
            }
        }

        public void AddEarn(CurrencyType type, long amount)
        {
            switch (type)
            {
                case CurrencyType.Gold: _spending.totalGoldEarn += amount; break;
                case CurrencyType.Gem: _spending.totalGemEarn += amount; break;
                case CurrencyType.Meat: _spending.totalMeatEarn += amount; break;
            }
        }

        // -------------------------------------------------------------------
        // Unity Lifecycle
        // -------------------------------------------------------------------
        private void OnApplicationQuit() => SaveAll();
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveAll();
        }

        // -------------------------------------------------------------------
        // Debug/Testing
        // -------------------------------------------------------------------
        [ContextMenu("Open Save File in Explorer")]
        private void DebugOpenSaveDirectory()
        {
#if UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start("explorer.exe", "/select," + SaveFile);
#elif UNITY_EDITOR_OSX
            System.Diagnostics.Process.Start("open", "-R " + SaveFile);
#endif
        }
    }
}
