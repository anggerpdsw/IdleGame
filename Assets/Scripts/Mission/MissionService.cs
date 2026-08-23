using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Mission
{
    public class MissionService : MonoBehaviour
    {
        #region Singleton
        [SerializeField] private bool _debug = false;
        private static MissionService _instance;
        public static MissionService Instance => _instance;
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
            OnMissionsChanged?.Invoke();
            if (_debug) Debug.Log($"[MissionService] Save loaded: {_missions.Count} missions, MaxMission: {_maxMission}");
        }
        #endregion

        #region Validation & Migration
        private void ValidateAndMigrate()
        {
            bool dirty = false;
            var now = DateTime.Now;
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
            int before = _missions.Count;
            _missions.RemoveAll(m => m.slotIndex == -1);
            if (_missions.Count != before) dirty = true;
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
            var now = DateTime.Now;
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
            else if (tmpl.type == MissionEventType.Blacksmithing)
            {
                var eqTypes = Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>()
                    .Where(e => e != EquipmentType.None).ToArray();
                if (eqTypes.Length > 0)
                    mission.targetId = eqTypes[UnityEngine.Random.Range(0, eqTypes.Length)].ToString();
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
            var now = DateTimeOffset.Now;
            bool dirty = false;
            var snapshot = _missions.ToArray();
            foreach (var m in snapshot)
            {
                if ((m.status == MissionStatus.Claimed || m.status == MissionStatus.Cancelled) &&
                    !string.IsNullOrEmpty(m.cooldownUntil) &&
                    DateTimeOffset.TryParse(m.cooldownUntil, out var end) && now >= end)
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
            _missions.RemoveAll(m => m.slotIndex == slot);
            var now = DateTime.Now;
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
        public MissionTemplate GetTemplate(string id)
            => string.IsNullOrEmpty(id) ? null : _templateData?.missions?.FirstOrDefault(t => t.id == id);

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
            var now = DateTime.Now;
            bool dirty = false;
            foreach (var m in _missions)
            {
                if (m == null || m.status != MissionStatus.Active) continue;
                var tmpl = GetTemplate(m.missionId);
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
                    OnMissionsChanged?.Invoke();
                }
            }
            if (dirty) SaveMissions();
        }

        private bool DoesEventMatchMission(MissionEventType ev, string tId, MissionTemplate t,
            MissionInstance m)
        {
            if (ev != t.type) return false;
            return t.type switch
            {
                MissionEventType.SpecificEnemyKilled or MissionEventType.Blacksmithing 
                    => IsTargetMatch(m.targetId, tId),
                MissionEventType.CurrencyEarned 
                    => IsTargetMatch(t.targetId, tId),
                MissionEventType.EnemyKilled or MissionEventType.BossKilled or MissionEventType.WaveCompleted 
                    => true,
                _ => false,
            };

        }
        private static bool IsTargetMatch(string mId, string tId)
        {
            return !string.IsNullOrEmpty(mId)
                && string.Equals(mId, tId, StringComparison.OrdinalIgnoreCase);
        }

        public bool ClaimMission(string id)
        {
            var m = GetMission(id);
            if (m == null || m.status != MissionStatus.Completed || m.rewardClaimed) return false;

            m.status = MissionStatus.Claimed; m.rewardClaimed = true;
            m.cooldownUntil = DateTimeOffset.Now.AddMinutes(GetClaimCooldown(m)).ToString("o");

            var rewardList = ToRewardData(m.reward);
            if (rewardList.Count > 0 && RewardManager.Instance != null)
                RewardManager.Instance.Show(rewardList, () => NotifyClaimed(m));
            else
            {
                GiveReward(m.reward);
                NotifyClaimed(m);
            }
            return true;
        }

        private void NotifyClaimed(MissionInstance m)
        {
            SaveMissions(); OnMissionStatusChanged?.Invoke(m); OnMissionsChanged?.Invoke();
        }

        private static List<RewardData> ToRewardData(MissionReward r)
        {
            var list = new List<RewardData>();
            if (r == null) return list;
            if (r.gold > 0) list.Add(new RewardData(RewardType.Gold, r.gold));
            if (r.gem > 0) list.Add(new RewardData(RewardType.Gem, r.gem));
            if (r.meat > 0) list.Add(new RewardData(RewardType.Meat, r.meat));
            return list;
        }

        public bool CancelMission(string id)
        {
            var m = GetMission(id);
            if (m == null || m.status != MissionStatus.Active) return false;
            m.status = MissionStatus.Cancelled;
            m.cooldownUntil = DateTimeOffset.Now.AddMinutes(GetCancelCooldown(m)).ToString("o");
            SaveMissions(); OnMissionStatusChanged?.Invoke(m); OnMissionsChanged?.Invoke(); return true;
        }

        private int GetClaimCooldown(MissionInstance m) => GetTemplate(m.missionId)?.claimCooldownMinutes ?? 30;
        private int GetCancelCooldown(MissionInstance m) => GetTemplate(m.missionId)?.cancelCooldownMinutes ?? 15;

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