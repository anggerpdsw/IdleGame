using System;
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
    /// 
    /// Separates concerns:
    /// - Manager: data + trigger logic (cooldown, chance)
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
            TextAsset ultimateJson = Resources.Load<TextAsset>("Data/dataUltimate");
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
        // Public API: Spawning with Trigger Logic
        // -------------------------------------------------------------------

        /// <summary>
        /// Try to spawn an ultimate by ID.
        /// Handles: active check, cooldown, chance, then delegates to factory.
        /// </summary>
        public bool TrySpawn(string ultimateId, Vector3 position, Player.Player player)
        {
            if (!TryGetUltimate(ultimateId, out var ultimateData)) return false;
            if (!ultimateData.GetActive()) return false;

            // Check cooldown (Lightning uses triggerKillCount instead of cooldown)
            float cooldown = ultimateData.GetCooldown();
            if (cooldown > 0f && !IsOffCooldown(ultimateId, cooldown)) return false;

            // Check chance (unless cooldown is active for this ultimate)
            float chance = ultimateData.GetChance();
            if (chance > 0f && !Utilityku.Chance(chance)) return false;

            // Delegate to factory
            bool success = UltimateFactory.TrySpawn(ultimateId, player, position, ultimateData);
            // Update cooldown timer
            if (success) _lastSpawnTimeMap[ultimateId] = Time.time;

            return success;
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
