using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using Newtonsoft.Json;

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
    /// Separates concerns:
    /// - Manager: data + trigger logic (cooldown, chance, stacks)
    /// - Factory: handler registry + instance creation
    /// - Handlers: individual ultimate spawn logic
    /// - Instances: individual ultimate runtime behavior
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
        private Dictionary<string, float> _lastSpawnTimeMap = new(); // For cooldown tracking

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
                    _lastSpawnTimeMap[data.id] = Time.time; // Initialize cooldown timer
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

        /// <summary>
        /// Get ultimate data by ID.
        /// Returns null if not found (logs error).
        /// </summary>
        public UltimateData GetUltimate(string id)
        {
            if (_ultimateDatabase.TryGetValue(id, out var data)) return data;
            Debug.LogError($"[UltimateManager] Ultimate data not found for id: {id}");
            return null;
        }

        /// <summary>
        /// Try to get ultimate data by ID (safe).
        /// </summary>
        public bool TryGetUltimate(string id, out UltimateData data)
        {
            return _ultimateDatabase.TryGetValue(id, out data);
        }

        // -------------------------------------------------------------------
        // Stack System API
        // -------------------------------------------------------------------

        /// <summary>
        /// Get current stack count for an ultimate.
        /// </summary>
        public int GetStack(string ultimateId)
        {
            return _currentStacks.TryGetValue(ultimateId, out int stack) ? stack : 0;
        }

        /// <summary>
        /// Get max stack count for an ultimate (from data).
        /// </summary>
        public int GetMaxStack(string ultimateId)
        {
            if (TryGetUltimate(ultimateId, out var data))
                return data.GetCount();
            return 1;
        }

        /// <summary>
        /// Try to add a stack (for auto-cast chance/kill triggers).
        /// Returns true if stack was added.
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
        /// Consume a stack for manual cast.
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
        /// Try to spawn an ultimate by ID.
        /// For stack-based ultimates: flushes all existing stacks, then generates new stacks via chance while mana permits.
        /// For cooldown-based ultimates: normal spawn logic.
        /// Caller is responsible for checking AutoCast setting (for auto-cast path).
        /// </summary>
        public bool TrySpawn(string ultimateId, Vector3 position, Player.Player player)
        {
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return false;
            if (!ultimateData.GetActive()) return false;

            // Check cooldown (Lightning uses triggerKillCount instead of cooldown)
            float cooldown = ultimateData.GetCooldown();
            if (cooldown > 0f && !IsOffCooldown(ultimateId, cooldown)) return false;

            // Check mana cost
            if (!player.CanAfford(ultimateData.manaCost)) return false;

            bool anySpawned = false;
            // Handle stack-based ultimates (Bomb, Tank, Cloud, Lightning)
            if (ultimateData.UsesStackSystem)
            {
                // 1. Flush ALL existing stacks immediately (manual click or switching to auto)
                int currentStack = GetStack(ultimateId);
                while (currentStack > 0 && player.CanAfford(ultimateData.manaCost))
                {
                    ConsumeStack(ultimateId);
                    anySpawned = FactoryTrySpawn(player, position, ultimateData);
                    currentStack = GetStack(ultimateId);
                }

                // 2. For chance-based ultimates (Bomb, Tank, Cloud): keep rolling chance while mana permits
                // Lightning has chance = 0, so this loop won't execute for it (kill-count handled separately)
                float chance = ultimateData.GetChance();
                int maxStack = GetMaxStack(ultimateId);
                while (chance > 0f && player.CanAfford(ultimateData.manaCost) && GetStack(ultimateId) < maxStack)
                {
                    if (!Utilityku.Chance(chance)) break; // Chance failed
                    if (!TryAddStack(ultimateId)) break; // Max stack reached
                    ConsumeStack(ultimateId);
                    anySpawned = FactoryTrySpawn(player, position, ultimateData);
                }

                return anySpawned;
            }

            // Normal cooldown-based ultimates (Void, Root, Fountain, Shockwave)
            return FactoryTrySpawn(player, position, ultimateData);
        }

        /// <summary>
        /// Manual cast entry point for user click.
        /// For chance-based stack ultimates (Bomb, Tank, Cloud): rolls chance → adds stack → consumes & spawns.
        /// For Lightning (kill-count): must have stack from RegisterKill → consumes & spawns.
        /// Bypasses chance check for Lightning (chance is 0).
        /// Still respects active, cooldown, and mana cost.
        /// </summary>
        public bool TrySpawnManual(string ultimateId, Vector3 position, Player.Player player)
        {
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return false;
            if (!ultimateData.GetActive()) return false;

            // Check cooldown (Lightning has no cooldown)
            float cooldown = ultimateData.GetCooldown();
            if (cooldown > 0f && !IsOffCooldown(ultimateId, cooldown)) return false;

            // Check mana cost
            if (!player.CanAfford(ultimateData.manaCost)) return false;

            // Handle stack-based ultimates (Bomb, Tank, Cloud, Lightning)
            if (ultimateData.UsesStackSystem)
            {
                if (GetStack(ultimateId) > 0)
                {
                    // Has existing stack (from manual chance rolls or Lightning kill-count)
                    // Consume one and spawn
                    if (!ConsumeStack(ultimateId)) return false;
                }
                else
                {
                    // No stack: for chance-based (Bomb, Tank, Cloud), roll chance to add one
                    float chance = ultimateData.GetChance();
                    if (chance > 0f)
                    {
                        if (!Utilityku.Chance(chance)) return false; // chance failed → no stack, no spawn
                        if (!TryAddStack(ultimateId)) return false; // max stack reached
                        // Consume the newly added stack
                        ConsumeStack(ultimateId);
                    }
                    else
                    {
                        // Lightning has chance = 0, requires kill-count stack from RegisterKill
                        // No stack available → cannot cast
                        return false;
                    }
                }
            }

            // Delegate to factory
            return FactoryTrySpawn(player, position, ultimateData);
        }

        private bool FactoryTrySpawn(Player.Player pl, Vector3 po, UltimateData ud)
        {
            if (UltimateFactory.TrySpawn(ud.id, pl, po, ud))
            {
                _lastSpawnTimeMap[ud.id] = Time.time;
                pl.SpendMana(ud.manaCost);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if an ultimate is off cooldown.
        /// </summary>
        private bool IsOffCooldown(string ultimateId, float cooldown)
        {
            if (!_lastSpawnTimeMap.TryGetValue(ultimateId, out float lastTime)) return true;

            return Time.time - lastTime >= cooldown;
        }

        /// <summary>
        /// Get the time until an ultimate is off cooldown.
        /// Returns 0 if already off cooldown.
        /// </summary>
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

        /// <summary>
        /// Get all registered ultimate IDs from factory.
        /// </summary>
        public IReadOnlyCollection<string> GetAllUltimateIds()
        {
            return UltimateFactory.GetAllUltimateIds();
        }

        /// <summary>
        /// Get active count for an ultimate from factory.
        /// </summary>
        public int GetActiveCount(string ultimateId)
        {
            return UltimateFactory.GetActiveCount(ultimateId);
        }
    }
}
