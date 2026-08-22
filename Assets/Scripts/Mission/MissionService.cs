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
    public class MissionService : MonoBehaviour
    {
        #region Singleton
        private static MissionService _instance;
        public static MissionService Instance => _instance;
        [SerializeField] private bool _debug = false;
        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this; DontDestroyOnLoad(gameObject);

            // Load templates EARLY so GenerateMissingMissions can use them
            // when SaveManager fires OnSaveLoaded (before BootstrapController calls Initialize)
            LoadTemplates();

            // Subscribe to SaveManager's load event so we generate missions after save loads
            SaveManager.OnSaveLoaded += OnSaveManagerLoaded;
        }

        private void OnDestroy()
        {
            SaveManager.OnSaveLoaded -= OnSaveManagerLoaded;
        }

        private void OnSaveManagerLoaded()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            var saveData = saveManager.LastLoadedSaveData;  // ← use loaded file data, not in-memory
            OnSaveLoaded(saveData);
        }
        #endregion

        #region Fields
        private MissionTemplateData _templateData;
        private List<MissionInstance> _missions = new();
        private int _maxMission = 1;
        public event Action OnMissionsChanged;
        public event Action<MissionInstance> OnMissionStatusChanged;
        public event Action<MissionInstance> OnMissionProgressChanged;
        #endregion

        #region Initialization
        public void Initialize()
        {
            // Templates already loaded in Awake; nothing else needed here
        }

        private void LoadTemplates()
        {
            var json = Resources.Load<TextAsset>("Data/Player/dataMission");
            if (json == null) { Debug.LogError("[MissionService] Failed to load dataMission.json"); _templateData = new MissionTemplateData(); return; }
            try { _templateData = JsonUtility.FromJson<MissionTemplateData>(json.text); if (_templateData?.missions == null) _templateData = new MissionTemplateData(); }
            catch (Exception e) { Debug.LogError($"[MissionService] Parse error: {e.Message}"); _templateData = new MissionTemplateData(); }
        }

        // Called by SaveManager after load completes
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
        #endregion

        #region Validation & Migration
        private void ValidateAndMigrate()
        {
            bool dirty = false;
            var now = DateTime.UtcNow;
            _missions.RemoveAll(m => m == null || string.IsNullOrEmpty(m.instanceId) || string.IsNullOrEmpty(m.missionId));

            var seen = new HashSet<string>();
            foreach (var m in _missions)
            {
                if (!seen.Add(m.instanceId)) { m.instanceId = GenerateInstanceId(); dirty = true; }
                if (m.currentCount < 0) m.currentCount = 0;
                if (m.currentCount > m.targetCount) m.currentCount = m.targetCount;
                if (m.status == MissionStatus.Active && m.currentCount >= m.targetCount) { m.status = MissionStatus.Completed; m.completedAt = now.ToString("o"); dirty = true; }
                if (m.slotIndex < 0 || m.slotIndex >= _maxMission) { m.slotIndex = -1; dirty = true; }
            }
            ReassignSlotIndices();
            if (dirty) SaveMissions();
        }

        private void ReassignSlotIndices()
        {
            var active = _missions.Where(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed).ToList();
            for (int i = 0; i < active.Count && i < _maxMission; i++) active[i].slotIndex = i;
        }
        #endregion

        #region Generation
        private void GenerateMissingMissions()
        {
            int active = _missions.Count(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed);
            int slots = _maxMission - active;
            if (slots <= 0) return;
            var now = DateTime.UtcNow;
            var used = new HashSet<string>(_missions.Where(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed).Select(m => m.missionId));

            for (int i = 0; i < slots; i++)
            {
                int slot = FindAvailableSlot();
                if (slot < 0) break;
                var tmpl = SelectRandomTemplate(used);
                if (tmpl == null) break;
                var mission = CreateMissionInstance(tmpl, slot, now);
                _missions.Add(mission);
                used.Add(tmpl.id);
                if (_debug) Debug.Log($"[MissionService] Generated mission {mission.instanceId} ({tmpl.id}) slot {slot}");
            }
        }

        private int FindAvailableSlot()
        {
            var taken = new HashSet<int>(_missions.Where(m => m.slotIndex >= 0 && m.slotIndex < _maxMission).Select(m => m.slotIndex));
            for (int i = 0; i < _maxMission; i++) if (!taken.Contains(i)) return i;
            return -1;
        }

        private MissionTemplate SelectRandomTemplate(HashSet<string> used)
        {
            var avail = _templateData.missions.Where(t => !used.Contains(t.id)).ToList();
            if (avail.Count == 0) avail = _templateData.missions.ToList();
            return avail.Count == 0 ? null : avail[UnityEngine.Random.Range(0, avail.Count)];
        }

        private MissionInstance CreateMissionInstance(MissionTemplate tmpl, int slot, DateTime now)
        {
            int targetCount = UnityEngine.Random.Range(tmpl.minCount, tmpl.maxCount + 1);
            var mission = new MissionInstance
            {
                instanceId = GenerateInstanceId(),
                missionId = tmpl.id,
                targetCount = targetCount,
                currentCount = 0,
                status = MissionStatus.Active,
                createdAt = now.ToString("o"),
                reward = new MissionReward { gold = tmpl.reward.gold, gem = tmpl.reward.gem, meat = tmpl.reward.meat },
                slotIndex = slot
            };

            // Specific enemy: pick random non‑BOSS enemy from JSON cache
            if (tmpl.type == MissionEventType.SpecificEnemyKilled)
            {
                var db = DatabaseJSONCache.DatabaseEnemy;
                if (db?.enemies != null && db.enemies.Length > 0)
                {
                    var candidates = db.enemies
                        .Where(e => e.role != Role.BOSS)
                        .Select(e => e.id).ToArray();
                    if (candidates.Length > 0)
                        mission.targetId = candidates[UnityEngine.Random.Range(0, candidates.Length)];
                }
            }
            else mission.targetId = tmpl.targetId;

            return mission;
        }

        private string GenerateInstanceId() => $"mission_{Guid.NewGuid().ToString("N")[..12]}";
        #endregion

        #region Cooldown
        private void Update() => CheckCooldowns();

        private void CheckCooldowns()
        {
            var now = DateTime.UtcNow;
            bool dirty = false;
            foreach (var m in _missions)
            {
                if ((m.status == MissionStatus.Claimed || m.status == MissionStatus.Cancelled) &&
                    !string.IsNullOrEmpty(m.cooldownUntil) &&
                    DateTime.TryParse(m.cooldownUntil, out var end) && now >= end)
                {
                    GenerateMissionForSlot(m.slotIndex);
                    dirty = true;
                }
            }
            if (dirty) SaveMissions();
        }

        private void GenerateMissionForSlot(int slot)
        {
            if (slot < 0 || slot >= _maxMission) return;
            var now = DateTime.UtcNow;
            var used = new HashSet<string>(_missions
                .Where(m => m.slotIndex != slot && (m.status == MissionStatus.Active || m.status == MissionStatus.Completed))
                .Select(m => m.missionId));
            var tmpl = SelectRandomTemplate(used);
            if (tmpl == null) return;
            var mission = CreateMissionInstance(tmpl, slot, now);
            _missions.Add(mission);
            if (_debug) Debug.Log($"[MissionService] New mission for slot {slot}: {mission.instanceId} ({tmpl.id})");
        }
        #endregion

        #region Public API
        public IReadOnlyList<MissionInstance> GetAllMissions() => _missions.AsReadOnly();
        public IReadOnlyList<MissionInstance> GetActiveMissions() => _missions.Where(m => m.status == MissionStatus.Active || m.status == MissionStatus.Completed).ToList().AsReadOnly();
        public MissionInstance GetMission(string id) => _missions.FirstOrDefault(m => m.instanceId == id);
        public int GetMaxMission() => _maxMission;

        public void SetMaxMission(int v)
        {
            if (v < 1) v = 1;
            _maxMission = v;
            var sm = SaveManager.Instance;
            if (sm != null) { var acc = sm.GetAccountData(); acc.maxMission = v; sm.SaveAll(); }
            GenerateMissingMissions(); SaveMissions(); OnMissionsChanged?.Invoke();
        }

        public void UpdateProgress(MissionEventType type, string targetId, long amount)
        {
            if (amount <= 0) return;
            var now = DateTime.UtcNow;
            bool dirty = false;
            foreach (var m in _missions)
            {
                if (m == null || m.status != MissionStatus.Active) continue;
                var tmpl = _templateData.missions.FirstOrDefault(t => t.id == m.missionId);
                if (tmpl == null) continue;
                if (!DoesEventMatchMission(type, targetId, tmpl, m)) continue;

                long old = m.currentCount;
                m.currentCount = Math.Min(m.currentCount + amount, m.targetCount);
                if (m.currentCount == old) continue;
                dirty = true; OnMissionProgressChanged?.Invoke(m);
                if (m.currentCount >= m.targetCount)
                {
                    m.status = MissionStatus.Completed;
                    m.completedAt = now.ToString("o");
                    OnMissionStatusChanged?.Invoke(m);
                }
            }
            if (dirty) SaveMissions();
        }

        private bool DoesEventMatchMission(MissionEventType ev, string targetId, MissionTemplate tmpl, MissionInstance mission) =>
            tmpl.type switch
            {
                MissionEventType.EnemyKilled => ev == MissionEventType.EnemyKilled,
                MissionEventType.SpecificEnemyKilled => ev == MissionEventType.SpecificEnemyKilled
                    && !string.IsNullOrEmpty(mission.targetId)
                    && string.Equals(mission.targetId, targetId, StringComparison.OrdinalIgnoreCase),
                MissionEventType.BossKilled => ev == MissionEventType.BossKilled,
                MissionEventType.CurrencyEarned => ev == MissionEventType.CurrencyEarned && tmpl.targetId == targetId,
                MissionEventType.WaveCompleted => ev == MissionEventType.WaveCompleted,
                _ => false,
            };

        public bool ClaimMission(string id)
        {
            var m = GetMission(id);
            if (m == null || m.status != MissionStatus.Completed || m.rewardClaimed) return false;
            GiveReward(m.reward);
            m.status = MissionStatus.Claimed; m.rewardClaimed = true;
            m.cooldownUntil = DateTime.UtcNow.AddMinutes(GetClaimCooldown(m)).ToString("o");
            SaveMissions(); OnMissionStatusChanged?.Invoke(m); OnMissionsChanged?.Invoke(); return true;
        }

        public bool CancelMission(string id)
        {
            var m = GetMission(id);
            if (m == null || m.status != MissionStatus.Active) return false;
            m.status = MissionStatus.Cancelled;
            m.cooldownUntil = DateTime.UtcNow.AddMinutes(GetCancelCooldown(m)).ToString("o");
            SaveMissions(); OnMissionStatusChanged?.Invoke(m); OnMissionsChanged?.Invoke(); return true;
        }

        private int GetClaimCooldown(MissionInstance m) => _templateData.missions.FirstOrDefault(t => t.id == m.missionId)?.claimCooldownMinutes ?? 30;
        private int GetCancelCooldown(MissionInstance m) => _templateData.missions.FirstOrDefault(t => t.id == m.missionId)?.cancelCooldownMinutes ?? 15;

        private void GiveReward(MissionReward r)
        {
            var econ = ServiceLocator.EconomyService;
            if (econ == null) return;
            if (r.gold > 0) econ.AddCurrency(CurrencyType.Gold, r.gold, "MissionReward");
            if (r.gem > 0) econ.AddCurrency(CurrencyType.Gem, r.gem, "MissionReward");
            if (r.meat > 0) econ.AddCurrency(CurrencyType.Meat, r.meat, "MissionReward");
        }

        public void RefreshMissions()
        {
            ValidateAndMigrate(); GenerateMissingMissions(); SaveMissions(); OnMissionsChanged?.Invoke();
        }
        #endregion

        #region Persistence
        private void SaveMissions()
        {
            SaveManager.Instance?.SaveAll();
        }
        #endregion

        #region Debug
        [ContextMenu("Debug Print Missions")]
        private void DebugPrintMissions()
        {
            foreach (var m in _missions)
                Debug.Log($"Mission {m.instanceId} tmpl {m.missionId} slot {m.slotIndex} status {m.status} prog {m.currentCount}/{m.targetCount} target {m.targetId} cd {m.cooldownUntil}");
        }

        [ContextMenu("Force Generate All Missions")]
        private void DebugForceGenerate()
        {
            _missions.Clear(); GenerateMissingMissions(); SaveMissions();
        }
        #endregion
    }
}