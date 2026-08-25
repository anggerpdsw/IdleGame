using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using Newtonsoft.Json;
using IdleDefenseSurvival.Controller;
using System.Linq;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Centralized manager for ultimate abilities.
    /// Handles:
    /// 1. Loading ultimate data from dataUltimate.json
    /// 2. Registering ultimate handlers
    /// 3. Providing data lookup
    /// 4. Delegating spawning to handlers via UltimateFactory
    /// 5. Stack management for chance/kill-count based ultimates (Bomb, Tank, Cloud, Lightning)
    ///
    /// Architecture:
    /// - TryGenerateStack(): chance roll → add stack → mark READY → HandleAutoCast
    /// - TryCastReady(): cooldown + mana check → consume stack → FactoryTrySpawn
    /// - TrySpawn(): auto-cast path → TryCastReady
    /// - TrySpawnManual(): manual click path → TryCastReady
    /// </summary>
    public class UltimateManager : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static UltimateManager _instance;
        public static UltimateManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
            UltimateFactory.Reset();
        }

        // -------------------------------------------------------------------
        // Handler Components (attach to UltimateManager GameObject)
        // -------------------------------------------------------------------
        [Header("Ultimate Handlers")]
        [Tooltip("Handlers for each ultimate type")]
        [SerializeField] private VoidHandler _voidHandler;
        [SerializeField] private TankHandler _tankHandler;
        [SerializeField] private RootHandler _rootHandler;
        [SerializeField] private BombHandler _bombHandler;
        [SerializeField] private FountainHandler _fountainHandler;
        [SerializeField] private CloudHandler _cloudHandler;
        [SerializeField] private LightningHandler _lightningHandler;
        [SerializeField] private ShockwaveHandler _shockwaveHandler;

        // -------------------------------------------------------------------
        // Data & Tracking
        // -------------------------------------------------------------------
        private Dictionary<string, UltimateData> _ultimateDatabase = new();
        private Dictionary<string, float> _lastSpawnTimeMap = new();

        // Stack system for chance/kill-count based ultimates
        // Bomb, Tank, Cloud: chance-based stacks
        // Lightning: kill-count tracked by LightningHandler.RegisterKill
        private Dictionary<string, int> _currentStacks = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            LoadUltimateDatabase();
            RegisterHandlers();
        }

        private void Start()
        {
            // Player is in Game scene, UltimateManager is in Bootstrap (DontDestroyOnLoad).
            // Persistent coroutine tracks Player.Instance across scene transitions.
            StartCoroutine(TrackPlayerManaChanges());
        }

        private System.Collections.IEnumerator TrackPlayerManaChanges()
        {
            Player.Player currentPlayer = null;

            while (true)
            {
                // Wait for Player to exist (first load or after MainMenu)
                yield return new WaitUntil(() => Player.Player.Instance != null);

                // If Player instance changed (scene reload), unsubscribe old
                if (currentPlayer != null && currentPlayer != Player.Player.Instance)
                    currentPlayer.OnManaChanged -= OnManaChanged;

                currentPlayer = Player.Player.Instance;
                currentPlayer.OnManaChanged += OnManaChanged;

                // Wait until this Player instance is destroyed (scene unload)
                yield return new WaitUntil(() => Player.Player.Instance == null || Player.Player.Instance != currentPlayer);

                // Unsubscribe from destroyed instance
                currentPlayer.OnManaChanged -= OnManaChanged;
            }
        }

        private void OnDestroy()
        {
            // Cleanup on Bootstrap reload or app quit
            if (Player.Player.Instance != null)
                Player.Player.Instance.OnManaChanged -= OnManaChanged;
        }

        private void LoadUltimateDatabase()
        {
            TextAsset ultimateJson = Resources.Load<TextAsset>("Data/Player/dataUltimate");
            if (ultimateJson == null)
            {
                Debug.LogError("[UltimateManager] Ultimate data file not found at Resources/Data/dataUltimate.json!");
                return;
            }

            var wrapper = JsonConvert.DeserializeObject<UltimateWrapper>(ultimateJson.text);
            if (wrapper?.ultimate != null)
            {
                _ultimateDatabase = new Dictionary<string, UltimateData>();
                foreach (var data in wrapper.ultimate)
                {
                    _ultimateDatabase[data.id] = data;
                    _lastSpawnTimeMap[data.id] = Time.time;
                    _currentStacks[data.id] = 0;
                }
            }
        }

        /// <summary>
        /// Register all handlers with the factory.
        /// Must be called after handlers are assigned in inspector.
        /// </summary>
        private void RegisterHandlers()
        {
            if (_voidHandler != null)
                UltimateFactory.RegisterHandler(_voidHandler.UltimateId, _voidHandler);
            if (_tankHandler != null)
                UltimateFactory.RegisterHandler(_tankHandler.UltimateId, _tankHandler);
            if (_rootHandler != null)
                UltimateFactory.RegisterHandler(_rootHandler.UltimateId, _rootHandler);
            if (_bombHandler != null)
                UltimateFactory.RegisterHandler(_bombHandler.UltimateId, _bombHandler);
            if (_fountainHandler != null)
                UltimateFactory.RegisterHandler(_fountainHandler.UltimateId, _fountainHandler);
            if (_cloudHandler != null)
                UltimateFactory.RegisterHandler(_cloudHandler.UltimateId, _cloudHandler);
            if (_lightningHandler != null)
                UltimateFactory.RegisterHandler(_lightningHandler.UltimateId, _lightningHandler);
            if (_shockwaveHandler != null)
                UltimateFactory.RegisterHandler(_shockwaveHandler.UltimateId, _shockwaveHandler);
        }

        // -------------------------------------------------------------------
        // Public API: Data Lookup
        // -------------------------------------------------------------------
        public UltimateData GetUltimate(string id)
        {
            if (_ultimateDatabase.TryGetValue(id, out var data)) return data;
            Debug.LogError($"[UltimateManager] Ultimate data not found for id: {id}");
            return null;
        }
        public bool TryGetUltimate(string id, out UltimateData data)
            => _ultimateDatabase.TryGetValue(id, out data);

        // -------------------------------------------------------------------
        // Stack System API
        // -------------------------------------------------------------------
        public int GetStack(string ultimateId)
            => _currentStacks.TryGetValue(ultimateId, out int stack) ? stack : 0;
        public int GetMaxStack(string ultimateId)
        {
            if (TryGetUltimate(ultimateId, out var data)) return data.GetCount();
            return 1;
        }

        /// <summary>
        /// Try to add a stack directly (used by Lightning kill-count).
        /// Does NOT roll chance. Returns true if stack was added.
        /// </summary>
        public bool TryAddStack(string ultimateId)
        {
            if (!TryGetUltimate(ultimateId, out var data)) return false;
            if (!data.UsesStackSystem) return false;
            int maxStack = data.GetCount();
            int currentStack = GetStack(ultimateId);
            if (currentStack >= maxStack) return false;
            _currentStacks[ultimateId] = currentStack + 1;
            return true;
        }

        /// <summary>
        /// Consume a READY stack for casting.
        /// Returns true if stack was consumed.
        /// </summary>
        public bool ConsumeStack(string ultimateId)
        {
            if (!_currentStacks.TryGetValue(ultimateId, out int currentStack)) return false;
            if (currentStack <= 0) return false;
            _currentStacks[ultimateId] = currentStack - 1;
            return true;
        }

        /// <summary>
        /// Generate one Ultimate stack via chance roll.
        /// For chance-based ultimates (Bomb, Tank, Cloud).
        /// After adding stack, attempts auto-cast if enabled.
        /// </summary>
        public bool TryGenerateStack(string ultimateId, Player.Player player)
        {
            if (player == null) return false;
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return false;
            if (!ultimateData.GetActive()) return false;
            if (!ultimateData.UsesStackSystem) return false;
            // For chance-based ultimates only
            float chance = ultimateData.GetChance();
            if (chance <= 0f) return false;
            // Check stack
            int maxStack = GetMaxStack(ultimateId);
            if (GetStack(ultimateId) >= maxStack) return false;
            // Roll chance stack
            if (!Utilityku.Chance(chance)) return false;
            if (!TryAddStack(ultimateId)) return false;
            // Auto cast check here
            HandleAutoCast(ultimateId, player);
            return true;
        }

        /// <summary>
        /// Generate a stack directly without chance roll (for Lightning kill-count).
        /// After adding stack, attempts auto-cast if enabled.
        /// </summary>
        public bool TryGenerateTriggerStack(string ultimateId, Player.Player player)
        {
            if (player == null) return false;
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return false;
            if (!ultimateData.GetActive()) return false;
            if (!ultimateData.UsesStackSystem) return false;
            // Lightning uses triggerKillCount, not chance
            if (ultimateData.GetTriggerKillCount() <= 0) return false;
            // Check stack
            int maxStack = GetMaxStack(ultimateId);
            if (GetStack(ultimateId) >= maxStack) return false;
            if (!TryAddStack(ultimateId)) return false;
            // Auto cast check here
            HandleAutoCast(ultimateId, player);
            return true;
        }

        /// <summary>
        /// Automatically casts a newly generated Ultimate stack
        /// when Auto Cast is enabled.
        /// </summary>
        private void HandleAutoCast(string ultimateId, Player.Player player)
        {
            if (!IsAutoCastEnabled()) return;
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return;
            // Keep the stack READY until the player has enough mana.
            if (!player.CanAfford(ultimateData.manaCost)) return;
            Vector3 position = GetSpawnPosition(ultimateId, player);
            TryCastReady(ultimateId, position, player);
        }

        /// <summary>
        /// Gets the spawn position for an ultimate.
        /// Override for special positioning (e.g., Tank).
        /// </summary>
        private Vector3 GetSpawnPosition(string ultimateId, Player.Player player)
        {
            if (ultimateId == UltimateDMG.Tank.ToString())
            {
                if (player.TryGetTankSpawnPosition(out Vector3 spawnPos))
                    return spawnPos;
            }
            return player.transform.position;
        }

        /// <summary>
        /// Casts an Ultimate (stack-based or cooldown-based).
        /// For stack-based ultimates: requires a READY stack, consumes it on success.
        /// For cooldown-based ultimates: no stack required, just cooldown + mana.
        /// Does not perform a chance roll. Stack must already exist (for stack ultimates).
        /// Cooldown and mana requirements are always respected.
        /// </summary>
        public bool TryCastReady(string ultimateId, Vector3 position, Player.Player player)
        {
            if (player == null) return false;
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return false;
            if (!ultimateData.GetActive()) return false;

            float cooldown = ultimateData.GetCooldown();
            if (cooldown > 0f && !IsOffCooldown(ultimateId, cooldown)) return false;

            if (!player.CanAfford(ultimateData.manaCost)) return false;

            // Stack-based ultimates (Bomb, Tank, Cloud, Lightning): require READY stack
            if (ultimateData.UsesStackSystem)
            {
                if (GetStack(ultimateId) <= 0) return false;
                // Spawn first, then consume stack on success
                if (!UltimateFactory.TrySpawn(ultimateData.id, player, position, ultimateData))
                    return false;
                if (!ConsumeStack(ultimateId)) return false;
            }
            // Cooldown-based ultimates (Void, Root, Fountain, Shockwave): no stack needed
            else
            {
                if (!UltimateFactory.TrySpawn(ultimateData.id, player, position, ultimateData))
                    return false;
            }

            _lastSpawnTimeMap[ultimateId] = Time.time;
            player.SpendMana(ultimateData.manaCost);
            return true;
        }

        /// <summary>
        /// Auto-cast path: attempts to cast a READY stack.
        /// Only used when AutoCastUltimate setting is ON.
        /// </summary>
        public bool TrySpawn(string ultimateId, Vector3 position, Player.Player player)
        {
            if (!IsAutoCastEnabled()) return false;
            return TryCastReady(ultimateId, position, player);
        }

        /// <summary>
        /// Manual cast path: attempts to cast a READY stack.
        /// Called by UI when user clicks an ultimate button.
        /// </summary>
        public bool TrySpawnManual(string ultimateId, Vector3 position, Player.Player player)
            => TryCastReady(ultimateId, position, player);

        /// <summary>
        /// Attempts to cast all currently READY stacks while
        /// cooldown and mana requirements allow.
        /// Used when mana regenerates to sufficient amount.
        /// </summary>
        public void TryCastReadyStacks(string ultimateId, Player.Player player)
        {
            if (player == null || !IsAutoCastEnabled()) return;
            while (GetStack(ultimateId) > 0)
            {
                if (!TryGetUltimate(ultimateId, out var ultimateData)) break;
                if (!player.CanAfford(ultimateData.manaCost)) break;
                if (ultimateData.GetCooldown() > 0f && !IsOffCooldown(ultimateId, ultimateData.GetCooldown())) break;
                Vector3 position = GetSpawnPosition(ultimateId, player);
                if (!TryCastReady(ultimateId, position, player)) break;
            }
        }

        private bool IsOffCooldown(string ultimateId, float cooldown)
        {
            if (!_lastSpawnTimeMap.TryGetValue(ultimateId, out float lastTime)) return true;
            return Time.time - lastTime >= cooldown;
        }

        private bool IsAutoCastEnabled()
            => SettingsController.Instance != null && SettingsController.Instance.AutoCastUltimate;

        public float GetCooldownRemaining(string ultimateId)
        {
            if (!TryGetUltimate(ultimateId, out var data)) return 0f;
            if (!_lastSpawnTimeMap.TryGetValue(ultimateId, out float lastTime)) return 0f;
            float elapsed = Time.time - lastTime;
            float cooldown = data.GetCooldown();
            return Mathf.Max(0f, cooldown - elapsed);
        }

        // -------------------------------------------------------------------
        // Public API: Utility
        // -------------------------------------------------------------------
        public IReadOnlyCollection<string> GetAllUltimateIds()
            => UltimateFactory.GetAllUltimateIds();
        public int GetActiveCount(string ultimateId)
            => UltimateFactory.GetActiveCount(ultimateId);

        /// <summary>
        /// Called when player's mana changes (e.g., mana regen tick).
        /// Checks if any ready ultimates can now be auto-cast.
        /// </summary>
        public void OnManaChanged()
        {
            var player = Player.Player.Instance;
            if (!IsAutoCastEnabled() || player == null) return;
            foreach (var ultimateId in _currentStacks.Keys.ToList())
            {
                if (GetStack(ultimateId) > 0)
                    TryCastReadyStacks(ultimateId, player);
            }
        }
    }
}