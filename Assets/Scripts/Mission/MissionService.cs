using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;
using Unity.VisualScripting;

namespace IdleDefenseSurvival.Mission
{
    /// <summary>
    /// Central service for managing missions - generation, progress, claim, cancel, cooldown
    /// </summary>
    public class MissionService : MonoBehaviour
    {
        #region Singleton

        private static MissionService _instance;
        public static MissionService Instance => _instance;

        [SerializeField] private bool _debug = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region Fields

        private MissionTemplateData _templateData;
        private List<MissionInstance> _missions = new();
        private int _maxMission = 1;

        // Events for UI
        public event Action OnMissionsChanged;
        public event Action<MissionInstance> OnMissionStatusChanged;
        public event Action<MissionInstance> OnMissionProgressChanged;

        #endregion

        #region Initialization

        public void Initialize()
        {
            LoadTemplates();
            LoadMissionsFromSave();
            ValidateAndMigrate();
            GenerateMissingMissions();
            SaveMissions();
        }

        private void LoadTemplates()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/Player/dataMission");
            if (json == null)
            {
                Debug.LogError("[MissionService] Failed to load dataMission.json from Resources");
                _templateData = new MissionTemplateData();
                return;
            }

            try
            {
                _templateData = JsonUtility.FromJson<MissionTemplateData>(json.text);
                if (_templateData?.missions == null)
                {
                    _templateData = new MissionTemplateData();
                }
                if (_debug) Debug.Log($"[MissionService] Loaded {_templateData.missions.Count} mission templates");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MissionService] Failed to parse dataMission.json: {e.Message}");
                _templateData = new MissionTemplateData();
            }
        }

        private void LoadMissionsFromSave()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            var saveData = GetSaveData();
            if (saveData?.missions != null)
            {
                _missions = saveData.missions;
            }
            else
            {
                _missions = new List<MissionInstance>();
            }

            _maxMission = saveData?.account?.maxMission ?? 1;
            if (_debug) Debug.Log($"[MissionService] Loaded {_missions.Count} missions, MaxMission: {_maxMission}");
        }

        private SaveData GetSaveData()
        {
            // Access via reflection or direct field access since SaveManager has private fields
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return null;

            // We need to access the internal save data - use a method on SaveManager
            // For now, we'll rely on SaveManager's public GetAccountData and missions are loaded via SaveManager
            // Actually, SaveManager loads data into its private fields, so we need a way to get missions
            return null; // Will be populated via SaveManager's load flow
        }

        /// <summary>
        /// Called by SaveManager after loading to populate missions
        /// </summary>
        public void OnSaveLoaded(SaveData saveData)
        {
            if (saveData == null) return;

            _missions = saveData.missions ?? new List<MissionInstance>();
            _maxMission = saveData.account?.maxMission ?? 1;

            ValidateAndMigrate();
            GenerateMissingMissions();
            SaveMissions();

            if (_debug) Debug.Log($"[MissionService] Save loaded: {_missions.Count} missions, MaxMission: {_maxMission}");
        }

        private void ValidateAndMigrate()
        {
            bool dirty = false;
            var now = DateTime.UtcNow;

            // Remove missions with invalid data
            _missions.RemoveAll(m => m == null || string.IsNullOrEmpty(m.instanceId) || string.IsNullOrEmpty(m.missionId));

            // Ensure InstanceId uniqueness
            var seenIds = new HashSet<string>();
            foreach (var mission in _missions)
            {
                if (seenIds.Contains(mission.instanceId))
                {
                    mission.instanceId = GenerateInstanceId();
                    dirty = true;
                }
                seenIds.Add(mission.instanceId);

                // Clamp currentCount
                if (mission.currentCount < 0) mission.currentCount = 0;
                if (mission.currentCount > mission.targetCount) mission.currentCount = mission.targetCount;

                // Auto-complete if target reached but status not updated
                if (mission.status == MissionStatus.Active && mission.currentCount >= mission.targetCount)
                {
                    mission.status = MissionStatus.Completed;
                    mission.completedAt = now.ToString("o");
                    dirty = true;
                }

                // Validate slotIndex
                if (mission.slotIndex < 0 || mission.slotIndex >= _maxMission)
                {
                    mission.slotIndex = -1;
                    dirty = true;
                }
            }

            // Reassign slot indices for active missions
            ReassignSlotIndices();

            if (dirty) SaveMissions();
        }

        private void ReassignSlotIndices()
        {
            var activeMissions = _missions.Where(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed).ToList();
            for (int i = 0; i < activeMissions.Count && i < _maxMission; i++)
            {
                if (activeMissions[i].slotIndex != i)
                {
                    activeMissions[i].slotIndex = i;
                }
            }
        }

        #endregion

        #region Mission Generation

        private void GenerateMissingMissions()
        {
            int activeCount = _missions.Count(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed);
            int slotsAvailable = _maxMission - activeCount;

            if (slotsAvailable <= 0) return;

            var now = DateTime.UtcNow;
            var usedTemplates = new HashSet<string>(_missions.Where(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed).Select(m => m.missionId));

            for (int i = 0; i < slotsAvailable; i++)
            {
                // Find available slot index
                int slotIndex = FindAvailableSlot();
                if (slotIndex < 0) break;

                var template = SelectRandomTemplate(usedTemplates);
                if (template == null) break;

                var mission = CreateMissionInstance(template, slotIndex, now);
                _missions.Add(mission);
                usedTemplates.Add(template.id);

                if (_debug) Debug.Log($"[MissionService] Generated mission: {mission.instanceId} ({template.id}) for slot {slotIndex}");
            }
        }

        private int FindAvailableSlot()
        {
            var usedSlots = new HashSet<int>(_missions.Where(m => m.slotIndex >= 0 && m.slotIndex < _maxMission).Select(m => m.slotIndex));
            for (int i = 0; i < _maxMission; i++)
            {
                if (!usedSlots.Contains(i)) return i;
            }
            return -1;
        }

        private MissionTemplate SelectRandomTemplate(HashSet<string> usedTemplates)
        {
            var available = _templateData.missions.Where(t => !usedTemplates.Contains(t.id)).ToList();

            // Fallback: if all templates used, allow duplicates
            if (available.Count == 0)
            {
                available = _templateData.missions.ToList();
            }

            if (available.Count == 0) return null;

            int index = UnityEngine.Random.Range(0, available.Count);
            return available[index];
        }

        private MissionInstance CreateMissionInstance(MissionTemplate template, int slotIndex, DateTime now)
        {
            int targetCount = UnityEngine.Random.Range(template.minCount, template.maxCount + 1); // inclusive

            return new MissionInstance
            {
                instanceId = GenerateInstanceId(),
                missionId = template.id,
                targetCount = targetCount,
                currentCount = 0,
                status = MissionStatus.Active,
                createdAt = now.ToString("o"),
                completedAt = null,
                cooldownUntil = null,
                rewardClaimed = false,
                slotIndex = slotIndex,
                reward = new MissionReward
                {
                    gold = template.reward.gold,
                    gem = template.reward.gem,
                    meat = template.reward.meat
                }
            };
        }

        private string GenerateInstanceId()
        {
            return $"mission_{Guid.NewGuid().ToString("N")[..12]}";
        }

        #endregion

        #region Cooldown Handling

        private void Update()
        {
            CheckCooldowns();
        }

        private void CheckCooldowns()
        {
            var now = DateTime.UtcNow;
            bool dirty = false;

            foreach (var mission in _missions)
            {
                if (mission.status == MissionStatus.Claimed || mission.status == MissionStatus.Cancelled)
                {
                    if (!string.IsNullOrEmpty(mission.cooldownUntil) && DateTime.TryParse(mission.cooldownUntil, out DateTime cooldownEnd))
                    {
                        if (now >= cooldownEnd)
                        {
                            // Cooldown expired - generate new mission for this slot
                            GenerateMissionForSlot(mission.slotIndex);
                            dirty = true;
                        }
                    }
                }
            }

            if (dirty) SaveMissions();
        }

        private void GenerateMissionForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _maxMission) return;

            var now = DateTime.UtcNow;
            var usedTemplates = new HashSet<string>(_missions.Where(m => m.slotIndex != slotIndex && (m.status == MissionStatus.Active || m.status == MissionStatus.Completed)).Select(m => m.missionId));
            var template = SelectRandomTemplate(usedTemplates);
            if (template == null) return;

            var mission = CreateMissionInstance(template, slotIndex, now);
            _missions.Add(mission);

            if (_debug) Debug.Log($"[MissionService] Generated new mission for slot {slotIndex}: {mission.instanceId} ({template.id})");
        }

        #endregion

        #region Public API

        public IReadOnlyList<MissionInstance> GetAllMissions() => _missions.AsReadOnly();

        public IReadOnlyList<MissionInstance> GetActiveMissions() => _missions.Where(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed).ToList().AsReadOnly();

        public MissionInstance GetMission(string instanceId) => _missions.FirstOrDefault(m => m.instanceId == instanceId);

        public int GetMaxMission() => _maxMission;

        public void SetMaxMission(int value)
        {
            if (value < 1) value = 1;
            _maxMission = value;

            var saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                var account = saveManager.GetAccountData();
                account.maxMission = _maxMission;
                saveManager.SaveAll();
            }

            GenerateMissingMissions();
            SaveMissions();
            OnMissionsChanged?.Invoke();
        }

        /// <summary>
        /// Update mission progress from gameplay events
        /// </summary>
        public void UpdateProgress(MissionEventType eventType, string targetId, long amount)
        {
            if (amount <= 0) return;

            var now = DateTime.UtcNow;
            bool dirty = false;

            foreach (var mission in _missions)
            {
                if (mission == null) continue;
                if (mission.status != MissionStatus.Active) continue;
                if (mission.targetCount <= 0) continue;

                var template = _templateData.missions
                    .FirstOrDefault(t => t.id == mission.missionId);
                if (template == null) continue;

                if (!DoesEventMatchMission(eventType, targetId, template)) continue;

                long oldCount = mission.currentCount;
                mission.currentCount = Math.Min(
                    mission.currentCount + amount, mission.targetCount);
                if (mission.currentCount == oldCount) continue;

                dirty = true;
                OnMissionProgressChanged?.Invoke(mission);

                if (mission.currentCount >= mission.targetCount)
                {
                    mission.currentCount = mission.targetCount;
                    mission.status = MissionStatus.Completed;
                    mission.completedAt = now.ToString("o");

                    OnMissionStatusChanged?.Invoke(mission);

                    if (_debug) Debug.Log(
                        $"[MissionService] Mission completed: " +
                        $"{mission.instanceId} " +
                        $"({mission.currentCount}/{mission.targetCount})"
                    );
                }
            }

            if (dirty) SaveMissions();
        }

        private bool DoesEventMatchMission(MissionEventType eventType, string targetId, MissionTemplate mission)
        {
            if (mission == null) return false;

            return mission.type switch
            {
                // Generic kill mission.
                // Hanya event EnemyKilled yang boleh masuk.
                MissionEventType.EnemyKilled => eventType == MissionEventType.EnemyKilled, 
                
                // Specific kill HARUS punya targetId 
                // dan harus sama persis dengan enemy yang mati.
                MissionEventType.SpecificEnemyKilled => eventType == MissionEventType.SpecificEnemyKilled
                    && !string.IsNullOrEmpty(mission.targetId)
                    && !string.IsNullOrEmpty(targetId)
                    && string.Equals(mission.targetId, targetId, StringComparison.Ordinal),

                // Any enemy with Role.BOSS.
                // targetId is intentionally ignored.
                MissionEventType.BossKilled => eventType == MissionEventType.BossKilled,

                // Gold, Gem, Meat
                MissionEventType.CurrencyEarned => eventType == MissionEventType.CurrencyEarned
                    && !string.IsNullOrEmpty(mission.targetId)
                    && string.Equals(mission.targetId, targetId, StringComparison.Ordinal),

                MissionEventType.WaveCompleted => eventType == MissionEventType.WaveCompleted,

                _ => false,
            };
        }

        /// <summary>
        /// Claim reward for a completed mission
        /// </summary>
        public bool ClaimMission(string instanceId)
        {
            var mission = GetMission(instanceId);
            if (mission == null)
            {
                if (_debug) Debug.LogWarning($"[MissionService] Mission not found: {instanceId}");
                return false;
            }

            if (mission.status != MissionStatus.Completed)
            {
                if (_debug) Debug.LogWarning($"[MissionService] Mission not completed: {instanceId} (status: {mission.status})");
                return false;
            }

            if (mission.rewardClaimed)
            {
                if (_debug) Debug.LogWarning($"[MissionService] Reward already claimed: {instanceId}");
                return false;
            }

            // Give reward
            GiveReward(mission.reward);

            // Update mission state
            mission.status = MissionStatus.Claimed;
            mission.rewardClaimed = true;
            mission.cooldownUntil = DateTime.UtcNow.AddMinutes(GetClaimCooldown(mission)).ToString("o");

            SaveMissions();
            OnMissionStatusChanged?.Invoke(mission);
            OnMissionsChanged?.Invoke();

            if (_debug) Debug.Log($"[MissionService] Mission claimed: {instanceId}, cooldown until: {mission.cooldownUntil}");
            return true;
        }

        /// <summary>
        /// Cancel an active mission
        /// </summary>
        public bool CancelMission(string instanceId)
        {
            var mission = GetMission(instanceId);
            if (mission == null)
            {
                if (_debug) Debug.LogWarning($"[MissionService] Mission not found: {instanceId}");
                return false;
            }

            if (mission.status != MissionStatus.Active)
            {
                if (_debug) Debug.LogWarning($"[MissionService] Mission not active: {instanceId} (status: {mission.status})");
                return false;
            }

            // Update mission state
            mission.status = MissionStatus.Cancelled;
            mission.cooldownUntil = DateTime.UtcNow.AddMinutes(GetCancelCooldown(mission)).ToString("o");

            SaveMissions();
            OnMissionStatusChanged?.Invoke(mission);
            OnMissionsChanged?.Invoke();

            if (_debug) Debug.Log($"[MissionService] Mission cancelled: {instanceId}, cooldown until: {mission.cooldownUntil}");
            return true;
        }

        private int GetClaimCooldown(MissionInstance mission)
        {
            var template = _templateData.missions.FirstOrDefault(t => t.id == mission.missionId);
            return template?.claimCooldownMinutes ?? 30;
        }

        private int GetCancelCooldown(MissionInstance mission)
        {
            var template = _templateData.missions.FirstOrDefault(t => t.id == mission.missionId);
            return template?.cancelCooldownMinutes ?? 15;
        }

        private void GiveReward(MissionReward reward)
        {
            var economy = ServiceLocator.EconomyService;
            if (economy == null) return;

            if (reward.gold > 0) economy.AddCurrency(CurrencyType.Gold, reward.gold, "MissionReward");
            if (reward.gem > 0) economy.AddCurrency(CurrencyType.Gem, reward.gem, "MissionReward");
            if (reward.meat > 0) economy.AddCurrency(CurrencyType.Meat, reward.meat, "MissionReward");
        }

        public void RefreshMissions()
        {
            ValidateAndMigrate();
            GenerateMissingMissions();
            SaveMissions();
            OnMissionsChanged?.Invoke();
        }

        #endregion

        #region Persistence

        private void SaveMissions()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            // Update the missions list in SaveManager's internal data
            // We need to trigger a save - SaveManager will pick up the missions via GatherAllData
            // But we need a way to push missions to SaveManager
            saveManager.SaveAll();
        }

        #endregion

        #region Debug

        [ContextMenu("Debug Print Missions")]
        private void DebugPrintMissions()
        {
            foreach (var m in _missions)
            {
                Debug.Log($"Mission: {m.instanceId} | Template: {m.missionId} | Slot: {m.slotIndex} | Status: {m.status} | Progress: {m.currentCount}/{m.targetCount} | Cooldown: {m.cooldownUntil}");
            }
        }

        [ContextMenu("Force Generate All Missions")]
        private void DebugForceGenerate()
        {
            _missions.Clear();
            GenerateMissingMissions();
            SaveMissions();
        }

        #endregion
    }
}