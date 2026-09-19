using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerClass = IdleDefenseSurvival.Player.Player;
using IdleDefenseSurvival.Pet.Behavior;

namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Central manager for Pet system.
    /// Handles pet equipping, state machine updates, target selection, behavior execution.
    /// Singleton pattern, DontDestroyOnLoad for persistence across scenes.
    /// </summary>
    public class PetManager : MonoBehaviour, IPetService
    {
        private static PetManager _instance;
        public static PetManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        [Header("Pet Configuration")]
        [SerializeField] private GameObject _petPrefab; // Generic pet visual prefab
        [SerializeField] private Transform _petContainer; // Parent transform for pet instances

        [Header("Target Scan Settings")]
        [SerializeField] private float _targetScanInterval = 0.2f; // Scan for targets every 0.2s

        [Header("Debug")]
        [SerializeField] private bool _debugBehaviors = false;

        // Pet database (loaded from JSON)
        private Dictionary<string, PetDefinition> _petDefinitions = new();

        // Owned pets (instance id -> runtime)
        private Dictionary<string, PetRuntime> _ownedPets = new();

        // Equipped pets (currently active in combat)
        private List<PetRuntime> _equippedPets = new();

        // Player reference
        private PlayerClass _player;

        // Shared behavior context (reused across all pets per frame)
        private ActionModifierData _sharedModifierData = new();
        private float _battleTime = 0f;

        // Events
        public event Action<PetRuntime> OnPetStateChanged;
        public event Action<PetRuntime, string> OnPetSkillReady;
        public event Action<PetRuntime> OnPetEquipped;
        public event Action<PetRuntime> OnPetUnequipped;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadPetDefinitions();
        }

        private void Start()
        {
            // Find player and subscribe to health changes for emergency mode
            _player = PlayerClass.Instance;
            if (_player != null) _player.OnHealthChanged += CheckEmergencyMode;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _battleTime += deltaTime;

            foreach (var pet in _equippedPets)
            {
                if (pet == null || pet.GameObject == null) continue;

                // Tick cooldowns (skills + behaviors)
                pet.TickCooldowns(deltaTime);

                // Tick stamina regeneration
                pet.TickStaminaRegen(deltaTime);

                // Update state machine and execute behaviors
                UpdatePetStateMachine(pet, deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnHealthChanged -= CheckEmergencyMode;
        }

        /// <summary>
        /// Load pet definitions from dataPet.json.
        /// </summary>
        private void LoadPetDefinitions()
        {
            var textAsset = Resources.Load<TextAsset>("Data/Pet/dataPet");
            if (textAsset == null)
            {
                Debug.LogWarning("[PetManager] dataPet.json not found in Resources/Data/Pet/");
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<PetDataWrapper>(textAsset.text);
                if (wrapper?.pets != null)
                {
                    foreach (var def in wrapper.pets)
                    {
                        _petDefinitions[def.id] = def;
                    }
                    Debug.Log($"[PetManager] Loaded {_petDefinitions.Count} pet definitions");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PetManager] Failed to parse dataPet.json: {e.Message}");
            }
        }

        /// <summary>
        /// Update pet behavior state machine.
        /// Now evaluates data-driven behaviors before falling back to legacy state logic.
        /// </summary>
        private void UpdatePetStateMachine(PetRuntime pet, float deltaTime)
        {
            // Clear invalid target
            pet.ClearInvalidTarget();

            // Create behavior context (shared data for behavior evaluation)
            var context = BehaviorContext.Create(pet, _player, _battleTime);

            // Execute data-driven behaviors (NEW)
            bool behaviorExecuted = pet.ExecuteBehaviors(context, _sharedModifierData);

            if (_debugBehaviors && behaviorExecuted)
            {
                Debug.Log($"[PetManager] {pet.PetId} executed behavior, target: {pet.Target?.name ?? "none"}");
            }

            // Legacy state machine (preserve for orbit/follow behavior when no attacks fire)
            switch (pet.CurrentState)
            {
                case PetState.Idle:
                    TransitionToFollow(pet);
                    break;

                case PetState.Follow:
                    UpdateFollowBehavior(pet, deltaTime);
                    // Check if should search for target
                    pet.TargetScanTimer += deltaTime;
                    if (pet.TargetScanTimer >= _targetScanInterval)
                    {
                        pet.TargetScanTimer = 0f;
                        TransitionToSearchTarget(pet);
                    }
                    break;

                case PetState.SearchTarget:
                    SearchForTarget(pet);
                    if (pet.Target != null)
                        TransitionToAttack(pet);
                    else
                        TransitionToFollow(pet);
                    break;

                case PetState.Attack:
                    UpdateAttackBehavior(pet, deltaTime);
                    break;

                case PetState.Emergency:
                    UpdateEmergencyBehavior(pet, deltaTime);
                    break;
            }
        }

        private void TransitionToFollow(PetRuntime pet)
        {
            pet.CurrentState = PetState.Follow;
            OnPetStateChanged?.Invoke(pet);
        }

        private void TransitionToSearchTarget(PetRuntime pet)
        {
            pet.CurrentState = PetState.SearchTarget;
        }

        private void TransitionToAttack(PetRuntime pet)
        {
            pet.CurrentState = PetState.Attack;
            pet.AttackTimer = 0f;
            OnPetStateChanged?.Invoke(pet);
        }

        /// <summary>
        /// Update follow behavior - formation anchor relatif player.
        /// Pet tidak langsung menuju player, tapi ke formation slot.
        /// </summary>
        private void UpdateFollowBehavior(PetRuntime pet, float deltaTime)
        {
            if (_player == null || pet.Transform == null) return;

            // Calculate formation offset (cartesian, bukan polar)
            Vector3 formationOffset = CalculateFormationOffset(pet.OrbitIndex, _equippedPets.Count, pet.Definition.orbitRadius);
            Vector3 formationPos = _player.transform.position + formationOffset;

            float distance = Vector3.Distance(pet.Transform.position, formationPos);
            float moveSpeed = pet.Definition.baseStats.moveSpeed;

            // Distance-based catch-up behavior
            if (distance > 8f)
            {
                // Safety snap - pet tertinggal terlalu jauh
                pet.Transform.position = formationPos;
            }
            else if (distance > 5f)
            {
                // Aggressive return - speed boost 2x
                pet.Transform.position = Vector3.MoveTowards(
                    pet.Transform.position,
                    formationPos,
                    moveSpeed * 2f * deltaTime
                );
            }
            else if (distance > 2f)
            {
                // Catch up - speed boost 1.5x
                pet.Transform.position = Vector3.MoveTowards(
                    pet.Transform.position,
                    formationPos,
                    moveSpeed * 1.5f * deltaTime
                );
            }
            else
            {
                // Normal follow - base speed
                pet.Transform.position = Vector3.MoveTowards(
                    pet.Transform.position,
                    formationPos,
                    moveSpeed * deltaTime
                );
            }

            pet.Position = pet.Transform.position;
        }

        /// <summary>
        /// Calculate formation offset untuk pet index.
        /// Formation pattern: spread horizontal di belakang player.
        /// </summary>
        private Vector3 CalculateFormationOffset(int index, int totalCount, float baseRadius)
        {
            if (totalCount == 1)
            {
                // Single pet: langsung di belakang player
                return new Vector3(0f, -baseRadius, 0f);
            }

            // Multi-pet: spread horizontal
            float spacing = baseRadius * 0.8f;
            float totalWidth = (totalCount - 1) * spacing;
            float startX = -totalWidth / 2f;

            float x = startX + (index * spacing);
            float y = -baseRadius; // Behind player

            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// Search for best target based on pet's priority rules.
        /// </summary>
        private void SearchForTarget(PetRuntime pet)
        {
            if (_player == null) return;

            pet.Target = PetTargeting.FindBestTarget(
                pet.Position,
                _player.transform.position,
                pet.Definition.baseStats.targetRange,
                pet.Definition.targetPriority
            );
        }

        /// <summary>
        /// Update attack behavior - basic attacks and skill casts.
        /// Behaviors handle actual damage via data-driven actions.
        /// </summary>
        private void UpdateAttackBehavior(PetRuntime pet, float deltaTime)
        {
            if (pet.Target == null || !pet.IsTargetValid())
            {
                TransitionToFollow(pet);
                return;
            }

            // Behavior system handles attacks now
            // This legacy path is kept for backward compatibility
        }

        /// <summary>
        /// Update emergency mode behavior.
        /// </summary>
        private void UpdateEmergencyBehavior(PetRuntime pet, float deltaTime)
        {
            // Emergency mode overrides normal targeting - prioritize closest to player
            pet.TargetScanTimer += deltaTime;
            if (pet.TargetScanTimer >= _targetScanInterval)
            {
                pet.TargetScanTimer = 0f;
                SearchForTarget(pet); // Uses existing priority system
            }

            // Force active skill cast if available
            string activeSkillId = pet.Definition.skills.active;
            if (!string.IsNullOrEmpty(activeSkillId) && !pet.IsSkillOnCooldown(activeSkillId))
            {
                OnPetSkillReady?.Invoke(pet, activeSkillId);
            }

            // Attack behavior same as normal
            UpdateAttackBehavior(pet, deltaTime);
        }

        /// <summary>
        /// Check if player HP triggers emergency mode.
        /// </summary>
        private void CheckEmergencyMode()
        {
            if (_player == null) return;

            float hpPercent = _player.CurrentHealth / _player.MaxHealth;

            foreach (var pet in _equippedPets)
            {
                bool shouldBeEmergency = hpPercent <= pet.Definition.emergencyThreshold;

                if (shouldBeEmergency && !pet.IsEmergencyMode)
                {
                    // Enter emergency mode
                    pet.IsEmergencyMode = true;
                    pet.CurrentState = PetState.Emergency;
                    OnPetStateChanged?.Invoke(pet);

                    // Trigger passive emergency skill (LastHorizon for Voidling)
                    string passiveSkillId = pet.Definition.skills.passive;
                    if (!string.IsNullOrEmpty(passiveSkillId) && !pet.IsSkillOnCooldown(passiveSkillId))
                    {
                        OnPetSkillReady?.Invoke(pet, passiveSkillId);
                    }
                }
                else if (!shouldBeEmergency && pet.IsEmergencyMode)
                {
                    // Exit emergency mode
                    pet.IsEmergencyMode = false;
                    TransitionToFollow(pet);
                }
            }
        }

        #region IPetService Implementation

        public List<PetRuntime> GetActivePets()
        {
            return new List<PetRuntime>(_equippedPets);
        }

        public bool EquipPet(string instanceId)
        {
            if (!_ownedPets.TryGetValue(instanceId, out var pet))
                return false;

            if (_equippedPets.Contains(pet))
                return false; // Already equipped

            // Spawn visual GameObject
            if (_petPrefab != null && _petContainer != null)
            {
                var petObj = Instantiate(_petPrefab, _petContainer);
                pet.GameObject = petObj;
                pet.Transform = petObj.transform;
                pet.OrbitIndex = _equippedPets.Count; // Assign orbit position
            }

            _equippedPets.Add(pet);
            pet.CurrentState = PetState.Follow;
            OnPetEquipped?.Invoke(pet);

            return true;
        }

        public bool UnequipPet(string instanceId)
        {
            var pet = _equippedPets.FirstOrDefault(p => p.InstanceId == instanceId);
            if (pet == null) return false;

            _equippedPets.Remove(pet);

            // Destroy visual GameObject
            if (pet.GameObject != null)
            {
                Destroy(pet.GameObject);
                pet.GameObject = null;
                pet.Transform = null;
            }

            OnPetUnequipped?.Invoke(pet);
            return true;
        }

        public PetDefinition GetPetDefinition(string petId)
        {
            return _petDefinitions.TryGetValue(petId, out var def) ? def : null;
        }

        public PetRuntime GetOwnedPet(string instanceId)
        {
            return _ownedPets.TryGetValue(instanceId, out var pet) ? pet : null;
        }

        public string GrantPet(string petId, int level = 1)
        {
            if (!_petDefinitions.TryGetValue(petId, out var definition))
            {
                Debug.LogError($"[PetManager] Pet definition not found: {petId}");
                return null;
            }

            string instanceId = Guid.NewGuid().ToString();
            var pet = new PetRuntime(instanceId, petId, definition, level);

            // NEW: Initialize behaviors from definition
            pet.InitializeBehaviors(definition);

            _ownedPets[instanceId] = pet;

            return instanceId;
        }

        public bool IsEmergencyModeActive()
        {
            return _equippedPets.Any(p => p.IsEmergencyMode);
        }

        /// <summary>
        /// Find equipped pet with lowest stamina percentage.
        /// Tie-breaker: lowest OrbitIndex (equip order).
        /// Returns null if no equipped pets.
        /// </summary>
        public PetRuntime GetPetWithLowestStaminaPercentage()
        {
            if (_equippedPets.Count == 0) return null;

            PetRuntime lowestPet = null;
            float lowestPercent = float.MaxValue;

            foreach (var pet in _equippedPets)
            {
                float percent = pet.StaminaPercent;

                // Lower percentage wins
                if (percent < lowestPercent)
                {
                    lowestPercent = percent;
                    lowestPet = pet;
                }
                // Tie-breaker: lowest OrbitIndex (deterministic)
                else if (Mathf.Approximately(percent, lowestPercent) &&
                         (lowestPet == null || pet.OrbitIndex < lowestPet.OrbitIndex))
                {
                    lowestPet = pet;
                }
            }

            return lowestPet;
        }

        #endregion

        #region Save/Load

        public List<PetSaveEntry> GetSaveData()
        {
            var saveData = new List<PetSaveEntry>();

            foreach (var pet in _ownedPets.Values)
            {
                saveData.Add(new PetSaveEntry
                {
                    instanceId = pet.InstanceId,
                    petId = pet.PetId,
                    level = pet.Level,
                    experience = pet.Experience,
                    evolutionStage = pet.EvolutionStage,
                    isEquipped = _equippedPets.Contains(pet),
                    currentStamina = pet.CurrentStamina
                });
            }

            return saveData;
        }

        public void LoadSaveData(List<PetSaveEntry> saveData)
        {
            if (saveData == null) return;

            _ownedPets.Clear();
            _equippedPets.Clear();

            foreach (var entry in saveData)
            {
                if (!_petDefinitions.TryGetValue(entry.petId, out var definition))
                {
                    Debug.LogWarning($"[PetManager] Pet definition not found for saved pet: {entry.petId}");
                    continue;
                }

                var pet = new PetRuntime(entry.instanceId, entry.petId, definition, entry.level)
                {
                    Experience = entry.experience,
                    EvolutionStage = entry.evolutionStage
                };

                // Restore stamina (backward compat: -1 = unset → use max)
                if (entry.currentStamina >= 0f)
                    pet.RestoreStamina(entry.currentStamina - pet.CurrentStamina);

                // NEW: Initialize behaviors from definition
                pet.InitializeBehaviors(definition);

                _ownedPets[entry.instanceId] = pet;

                if (entry.isEquipped) EquipPet(entry.instanceId);
            }
        }

        #endregion
    }

    /// <summary>
    /// Wrapper for JSON deserialization.
    /// </summary>
    [Serializable]
    public class PetDataWrapper
    {
        public List<PetDefinition> pets;
    }

    /// <summary>
    /// Save data entry for persistent pet state.
    /// </summary>
    [Serializable]
    public class PetSaveEntry
    {
        public string instanceId;
        public string petId;
        public int level;
        public long experience;
        public int evolutionStage;
        public bool isEquipped;
        public float currentStamina = -1f; // -1 = unset (v4 backward compat)
    }
}
