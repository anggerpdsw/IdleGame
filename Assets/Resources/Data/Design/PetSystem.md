# Pet System Design Documentation

**Version:** 1.0  
**Last Updated:** 2026-09-16  
**Status:** Implementation Complete — Core + Voidling

---

## 1. SYSTEM OVERVIEW

Pet System adds autonomous combat companions that fight alongside the player. Pets have independent AI, skills, passives, and emergency behaviors triggered by player state.

### Core Concepts

- **Data-Driven:** All pet stats, skills, and behaviors configured via JSON (`dataPet.json`)
- **Event-Based:** Emergency mode triggered by `Player.OnHealthChanged` event (no polling)
- **Pooled Resources:** Reuses existing `ProjectilePool`, `EnemyStatusEffectController`, damage pipeline
- **Modular Skills:** Skills extend `PetSkill` base class with custom cast conditions
- **State Machine:** Clean transitions (Idle → Follow → SearchTarget → Attack, interrupt to Emergency)

---

## 2. ARCHITECTURE

### 2.1 Core Components

| Component | Responsibility | File |
|---|---|---|
| `PetDefinition` | Static pet data (stats, skills, behavior config) | `Scripts/Pet/PetDefinition.cs` |
| `PetRuntime` | Instance state (level, XP, cooldowns, target, position) | `Scripts/Pet/PetRuntime.cs` |
| `PetManager` | Singleton manager (equip/unequip, state machine, target scanning) | `Scripts/Pet/PetManager.cs` |
| `PetState` | Enum (Idle, Follow, SearchTarget, Attack, Emergency, Dead) | `Scripts/Pet/PetState.cs` |
| `PetTargeting` | Target scoring system (distance, elite, HP priorities) | `Scripts/Pet/PetTargeting.cs` |
| `IPetService` | Service interface for `ServiceLocator` | `Scripts/Pet/IPetService.cs` |

### 2.2 State Machine

```
Idle → Follow → SearchTarget → Attack (loop)
                    ↑                ↓
                    └───── Emergency (interrupt) when player HP ≤ 30%
```

**Transition rules:**
- **Idle → Follow:** Always transition on first update
- **Follow → SearchTarget:** Periodic (every 0.2s) when no target
- **SearchTarget → Attack:** When valid target found
- **Attack → Follow:** When target dies or becomes invalid
- **Any → Emergency:** Player HP drops below `emergencyThreshold` (30% for Voidling)
- **Emergency → Follow:** Player HP recovers above threshold

### 2.3 Integration Points

| System | Usage | Notes |
|---|---|---|
| `ProjectilePool` | Pet basic attacks | Reuse existing pool, no new projectile system |
| `EnemyStatusEffectController` | Apply Slow/DoT/CC | `AddEffect(new SlowStatus(...))` |
| `Player.OnHealthChanged` | Emergency mode trigger | Event subscription in `PetManager.Awake()` |
| `SaveManager` | Pet persistence | New `pets` array in `SaveData` v4 |
| `ServiceLocator` | Service registration | `ServiceLocator.PetService` |

---

## 3. PET STATS

### 3.1 Base Stats (from `PetDefinition`)

- `attack` — base attack damage
- `attackSpeed` — attacks per second
- `skillDamage` — skill damage multiplier (1.5 = +50% skill damage)
- `moveSpeed` — movement speed (orbiting, following)
- `targetRange` — max range to search for targets
- `health` — pet health (if applicable; most pets invulnerable)

### 3.2 Stat Calculation Formula

```
FinalStat = (BaseStat + (Level - 1) × GrowthPerLevel) × RarityMultiplier
```

**Rarity Multipliers:**
- Common: 1.0×
- Rare: 1.15×
- Epic: 1.3×
- Legendary: 1.5×
- Mythic: 1.8×

**Example (Voidling Level 5, Epic):**
```
Attack = (10 + (5-1) × 2) × 1.3 = 18 × 1.3 = 23.4
```

---

## 4. VOIDLING SPECIFICATION

### 4.1 Identity

- **ID:** `pet_voidling`
- **Name:** Voidling
- **Role:** Support / Crowd Control
- **Rarity:** Epic (1.3× stat multiplier)
- **Theme:** Void creature that protects player in emergencies

### 4.2 Base Stats (Level 1)

| Stat | Value | Growth/Level |
|---|---|---|
| Attack | 10 | +2 |
| Attack Speed | 1.5 | — |
| Skill Damage | 1.5 | +0.1 |
| Move Speed | 5 | — |
| Target Range | 8 | — |
| Health | 100 | +10 |

### 4.3 Behavior Configuration

- **Behavior Type:** Guardian
- **Orbit Radius:** 2.5 units around player
- **Emergency Threshold:** 30% player HP
- **Target Priority:**
  1. Closest to Player (highest weight)
  2. Elite enemies (medium weight)
  3. Highest HP (lowest weight)

### 4.4 Skills

#### VoidBolt (Basic Attack)

- **Type:** Basic projectile attack
- **Damage:** 100% pet Attack
- **Cooldown:** Governed by `attackSpeed` stat
- **Range:** `targetRange` stat
- **Implementation:** Reuses `ProjectilePool`, standard projectile behavior

#### VoidPulse (Active Skill)

- **Type:** AOE around player
- **Damage:** 150% pet Attack
- **Radius:** 3.5 units
- **Slow:** 35% movement speed reduction
- **Slow Duration:** 2.5 seconds
- **Cooldown:** 12 seconds
- **Cast Conditions:**
  - Enemy count in radius ≥ 3, OR
  - Elite/Boss present in radius, OR
  - Emergency mode active
- **Status Effect:** `SlowStatus` (Temporary type)

**Formula:**
```
Damage = PetAttack × 1.5 × SkillDamageMultiplier
Example (Lv5 Voidling): 23.4 × 1.5 × 1.9 = 66.7 damage
```

#### BlackHole (Evolution Skill)

- **Type:** Area control + DoT + Explosion
- **Unlock:** Voidling evolution stage 1 (level 10+)
- **Duration:** 3 seconds
- **Radius:** 4.5 units
- **Pull Force:** 8 units/sec (30% for bosses, 50% for elites)
- **DoT:** 60% pet Attack per 0.5s tick (6 ticks total)
- **Explosion:** 300% pet Attack (final hit)
- **Cooldown:** 25 seconds
- **Cast Condition:** ≥5 enemies present

**Formulas:**
```
DoT per tick = PetAttack × 0.6 × SkillDamageMultiplier
Explosion = PetAttack × 3.0 × SkillDamageMultiplier

Total damage (6 DoT ticks + explosion):
= (0.6 × 6 + 3.0) × PetAttack × SkillDamageMultiplier
= 6.6 × PetAttack × SkillDamageMultiplier
```

#### LastHorizon (Passive)

- **Type:** Emergency shield passive
- **Trigger:** Player HP drops ≤ 30%
- **Shield:** 8% player max HP
- **Damage Reduction:** 20%
- **Duration:** 3 seconds
- **Cooldown:** 25 seconds
- **Visual:** Barrier effect enabled on player

**Formula:**
```
Shield Amount = PlayerMaxHP × 0.08
Example (Player 1000 HP): 1000 × 0.08 = 80 HP shield
```

---

## 5. EMERGENCY MODE

### 5.1 Trigger Condition

```csharp
float hpPercent = Player.CurrentHealth / Player.MaxHealth;
if (hpPercent <= pet.Definition.emergencyThreshold) // 0.3 for Voidling
    EnterEmergencyMode();
```

### 5.2 Behavior Changes

**Normal Mode:**
- Orbit player at `orbitRadius`
- Scan for targets every 0.2s
- Attack closest target matching priority
- Cast VoidPulse when conditions met

**Emergency Mode:**
- Same orbiting behavior
- Target selection prioritizes enemies **closest to player** (highest threat)
- Force VoidPulse cast immediately if skill ready (ignore normal conditions)
- Trigger LastHorizon passive if cooldown ready
- Return to normal when player HP > 30%

### 5.3 Event Flow

```
Player.OnHealthChanged event fired
    ↓
PetManager.CheckEmergencyMode()
    ↓
HP ≤ 30%? → Enter Emergency
    ↓
Set pet.CurrentState = PetState.Emergency
    ↓
Trigger LastHorizon passive (if ready)
    ↓
Force VoidPulse on next update (if ready)
    ↓
HP > 30%? → Exit Emergency → Return to Follow
```

---

## 6. TARGET SELECTION ALGORITHM

### 6.1 Scoring System

Each enemy receives a score based on priority rules. Higher score = higher priority target.

**Base scoring formula:**
```
TotalScore = Σ (PriorityRuleScore × PriorityWeight)

PriorityWeight = number of remaining priorities
First priority = highest weight, last priority = weight 1
```

### 6.2 Priority Rules

| Rule | Score Formula | Max Score |
|---|---|---|
| `ClosestToPlayer` | `100 - distanceToPlayer` | 100 |
| `ClosestToPet` | `100 - distanceToPet` | 100 |
| `Elite` | `50` if Elite, else `0` | 50 |
| `Boss` | `100` if Boss, else `0` | 100 |
| `HighestHp` | `(currentHP / maxHP) × 50` | 50 |
| `LowestHp` | `(1 - currentHP / maxHP) × 50` | 50 |

**Example (Voidling):**
```
Priorities: [ClosestToPlayer, Elite, HighestHp]

Enemy A: Normal, 5 units from player, 80% HP
  Score = (100 - 5) × 3 + 0 × 2 + (0.8 × 50) × 1
        = 285 + 0 + 40
        = 325

Enemy B: Elite, 8 units from player, 100% HP
  Score = (100 - 8) × 3 + 50 × 2 + (1.0 × 50) × 1
        = 276 + 100 + 50
        = 426

→ Enemy B (Elite) selected despite being farther
```

### 6.3 Target Scan Frequency

- **Interval:** 0.2 seconds (5 times per second)
- **Method:** `Physics2D.OverlapCircleAll(petPosition, targetRange, EnemyLayerMask)`
- **Performance:** Single physics query per scan, result cached until next scan

---

## 7. SAVE SYSTEM INTEGRATION

### 7.1 Save Data Structure

```csharp
[Serializable]
public class PetSaveEntry
{
    public string instanceId;      // GUID
    public string petId;           // Definition ID ("pet_voidling")
    public int level;              // Current level
    public long experience;        // Current XP
    public int evolutionStage;     // 0 = base, 1 = evolved
    public bool isEquipped;        // Currently equipped?
}
```

### 7.2 SaveData Schema (Version 4)

```json
{
  "version": 4,
  "pets": [
    {
      "instanceId": "a1b2c3d4-...",
      "petId": "pet_voidling",
      "level": 5,
      "experience": 12500,
      "evolutionStage": 0,
      "isEquipped": true
    }
  ]
}
```

### 7.3 Save Migration (v3 → v4)

```csharp
// In SaveManager.UpgradeSave()
if (data.version < 4)
{
    data.pets ??= new List<PetSaveEntry>();
}
```

---

## 8. PERFORMANCE CONSIDERATIONS

### 8.1 Optimizations Implemented

| System | Optimization | Impact |
|---|---|---|
| Target Scanning | 0.2s interval (not per-frame) | 80% reduction in queries |
| Projectiles | Reuse `ProjectilePool` | Zero new allocation |
| Status Effects | Batch via `AddEffect()` | Leverages existing system |
| State Transitions | Event-driven | No per-frame state polling |
| Emergency Detection | `OnHealthChanged` subscription | Zero HP polling |

### 8.2 Performance Budgets

**Per equipped pet:**
- Target scan: 1 `OverlapCircleAll` per 0.2s = 5 queries/sec
- State machine: ~10 conditional checks per frame
- Cooldown ticks: 1-4 dictionary updates per frame
- Basic attack: 1 projectile spawn per `1/attackSpeed` seconds

**Expected cost (1 pet, 60 FPS):**
- ~5 physics queries/sec (not per-frame)
- ~600 state checks/sec (negligible)
- ~60-240 dictionary operations/sec (negligible)

**Scalability:**
- 1 pet: <1% frame time
- 3 pets: <3% frame time
- 5+ pets: Test performance, may need spatial partitioning for target queries

---

## 9. EXTENSION GUIDE

### 9.1 Adding New Pets

**Step 1:** Add definition to `dataPet.json`

```json
{
  "id": "pet_ember_fox",
  "name": "Ember Fox",
  "role": "DPS",
  "rarity": "Legendary",
  "behaviorType": "Aggressive",
  "targetPriority": ["LowestHp", "ClosestToPet"],
  "skills": {
    "basic": "flame_bolt",
    "active": "fire_storm",
    "passive": "burning_aura",
    "evolution": "inferno"
  }
}
```

**Step 2:** Create skills

```csharp
// Scripts/Pet/Skills/FlameBolt.cs
public class FlameBolt : PetSkill
{
    public FlameBolt()
    {
        SkillId = "flame_bolt";
        BaseDamageMultiplier = 1.2f; // 120% pet attack
    }
    
    protected override void OnExecute()
    {
        // Implementation
    }
}
```

**Step 3:** Test
- Compile
- Load pet via `PetManager.GrantPet("pet_ember_fox")`
- Equip via `PetManager.EquipPet(instanceId)`
- Verify behavior, skills, save/load

### 9.2 Adding New Skills

**Extend `PetSkill` base class:**

```csharp
public class CustomSkill : PetSkill
{
    public CustomSkill()
    {
        SkillId = "custom_skill";
        BaseCooldown = 15f;
        BaseDamageMultiplier = 2.0f;
    }
    
    protected override bool CheckCustomConditions()
    {
        // Custom cast conditions
        return true;
    }
    
    protected override void OnExecute()
    {
        // Skill logic
        float damage = CalculateDamage();
        // Apply damage, effects, etc.
    }
}
```

**Skill can access:**
- `Pet` — full runtime state
- `Pet.Target` — current target
- `Pet.Position` — pet world position
- `CalculateDamage()` — base damage × skill multiplier
- `Pet.StartCooldown(skillId, duration)` — manual cooldown management

### 9.3 Adding New Behaviors

**Option A:** Extend `PetManager` state machine

**Option B:** Create pet-specific behavior class

```csharp
public class EmberFoxBehavior
{
    private PetRuntime _pet;
    
    public void Update(float deltaTime)
    {
        // Custom behavior logic
    }
}
```

Register in `PetManager.UpdatePetStateMachine()` with behavior type check.

---

## 10. KNOWN LIMITATIONS

### 10.1 Current Implementation

- **Single pet support:** UI/save system supports multiple, but only 1-3 recommended due to target query cost
- **No pet HP system:** Pets are invulnerable (health stat defined but unused)
- **Projectile owner workaround:** `Projectile.Initialize()` doesn't natively support Pet owner, uses damage multiplier trick
- **No pet-specific VFX:** Skill VFX/SFX marked with TODO comments
- **LastHorizon shield:** Uses existing player barrier visual, not custom pet effect

### 10.2 Future Enhancements

- **Pet HP/death system** — if pets become targetable by enemies
- **Pet-specific projectile types** — extend `Projectile` to support Pet as first-class owner
- **Advanced AI behaviors** — pathing, formations, coordinated attacks
- **Pet synergy bonuses** — multiple pets granting combined effects
- **Pet equipment system** — pets equipping their own gear

---

## 11. DEBUGGING

### 11.1 Debug Flags

Add to `PetManager`:

```csharp
[SerializeField] private bool _debugMode = false;

if (_debugMode)
    Debug.Log($"[Pet] State: {pet.CurrentState}, Target: {pet.Target?.name}");
```

### 11.2 Common Issues

| Symptom | Likely Cause | Solution |
|---|---|---|
| Pet not spawning | Missing prefab reference | Assign `_petPrefab` in `PetManager` Inspector |
| No target found | Layer mask incorrect | Verify enemies on "Enemy" layer |
| Skills not casting | Cooldown not expiring | Check `Pet.TickCooldowns()` called in Update |
| Emergency mode stuck | Event not unsubscribing | Check `OnDestroy` cleanup |
| Save not persisting | `SaveManager.MarkPetDirty()` not called | Add dirty flag after pet changes |

---

## 12. TESTING CHECKLIST

### 12.1 Functional Tests

- [ ] Pet equip/unequip
- [ ] Pet follows player orbit
- [ ] Target selection matches priority rules
- [ ] Basic attack fires at correct rate
- [ ] VoidPulse casts under correct conditions
- [ ] Emergency mode triggers at 30% HP
- [ ] LastHorizon shield applies
- [ ] BlackHole unlocks at level 10
- [ ] Save/load preserves pet state
- [ ] Multiple pets orbit without collision

### 12.2 Performance Tests

- [ ] Target scan frequency = 0.2s (not per-frame)
- [ ] Projectiles pool correctly (no Instantiate spam)
- [ ] State machine transitions don't allocate GC
- [ ] 5000+ enemies + 1 pet = stable 60 FPS

### 12.3 Edge Cases

- [ ] Target dies mid-attack
- [ ] Player dies during emergency mode
- [ ] Pet unequipped during skill cast
- [ ] Scene transition while pet active
- [ ] Save/load during cooldown
- [ ] Multiple emergency mode triggers rapid

---

## 13. STAMINA SYSTEM

### 13.1 Overview

Pet stamina system prevents infinite skill spam and creates resource management. Each pet has independent stamina pool with regeneration.

**Key Principles:**
- **Per-pet stamina** — each pet tracks own stamina independently
- **Skill costs** — every skill consumes stamina on execution
- **Automatic regeneration** — stamina recovers over time
- **Percentage-based potion targeting** — potion restores pet with lowest stamina %
- **Data-driven** — all values configurable via JSON

### 13.2 Stamina Stats

**PetDefinition fields:**
```csharp
public float maxStamina = 100f;
public float staminaRegen = 2f; // Per second
```

**Current values (dataPet.json):**

| Pet | Max Stamina | Regen/sec | Design Rationale |
|-----|-------------|-----------|------------------|
| Voidling | 100 | 2.0 | Support baseline — balanced pool |
| Ember Fox | 120 | 2.5 | DPS — aggressive, high sustain |
| Thunder Pup | 110 | 2.2 | DPS — balanced AOE spam |
| Blood Bat | 80 | 3.0 | Support — low pool, fast recovery |
| Iron Tortoise | 150 | 1.5 | Tank — deep pool, slow regen |

**Stat Calculation:**
```
MaxStamina = definition.maxStamina (no level scaling)
CurrentStamina = clamped [0, MaxStamina]
StaminaPercent = CurrentStamina / MaxStamina
```

### 13.3 Skill Stamina Cost

**PetSkill base class:**
```csharp
public float StaminaCost { get; protected set; }
```

**Behavior action schema:**
```json
{
  "action": {
    "type": "BasicAttack",
    "damageMultiplier": 1.0,
    "staminaCost": 15
  }
}
```

**Current skill costs:**

| Pet | Skill | Cost | Attacks @ Full |
|-----|-------|------|----------------|
| Voidling | Basic Attack | 15 | 6.7× |
| Voidling | AOE Pulse | 35 | 2.9× |
| Ember Fox | Executioner | 20 | 6.0× |
| Thunder Pup | Cluster Bomb | 18 | 6.1× |
| Blood Bat | Vampiric Assault | 12 | 6.7× |
| Iron Tortoise | Guardian Shield | 40 | 3.8× |
| Iron Tortoise | Basic Taunt | 10 | 15.0× |

**Validation Pipeline:**
```
PetSkill.CanExecute()
    ↓
Check cooldown
    ↓
Check Pet.CanConsumeStamina(StaminaCost)
    ↓
Check custom conditions
    ↓
Execute → ConsumeStamina(cost) → OnExecute()
```

### 13.4 Stamina Regeneration

**Implementation:**
```csharp
// Called every frame in PetManager.Update()
pet.TickStaminaRegen(deltaTime);

// PetRuntime.TickStaminaRegen()
public void TickStaminaRegen(float deltaTime)
{
    float regen = Definition.staminaRegen;
    if (regen > 0f)
        RestoreStamina(regen * deltaTime);
}
```

**Regeneration formula:**
```
StaminaGain = staminaRegen × deltaTime
CurrentStamina = Clamp(CurrentStamina + StaminaGain, 0, MaxStamina)
```

**Examples (60 FPS):**
- Voidling: 2.0/sec = ~0.033 per frame
- Blood Bat: 3.0/sec = ~0.05 per frame
- Iron Tortoise: 1.5/sec = ~0.025 per frame

### 13.5 Stamina Potion

**Data source:** `dataConsumables.json`
```json
{
  "Id": "potion_sp",
  "Name": "Stamina Potion",
  "PotionType": 3,
  "PercentValue": 5,
  "FlatValue": 100,
  "EffectDuration": 10,
  "Cooldown": 5
}
```

**Restore calculation:**
```csharp
float amount = (MaxStamina × 0.05) + 100;
pet.RestoreStamina(amount);
```

**Examples:**
- Voidling (max 100): restores 5 + 100 = 105 → capped to 100
- Ember Fox (max 120): restores 6 + 100 = 106 → capped to 120
- Iron Tortoise (max 150): restores 7.5 + 100 = 107.5 → capped to 150

### 13.6 Potion Targeting Algorithm

**Target selection:** Pet with **lowest stamina percentage** (not absolute value).

**Implementation:**
```csharp
public PetRuntime GetPetWithLowestStaminaPercentage()
{
    PetRuntime lowestPet = null;
    float lowestPercent = float.MaxValue;
    
    foreach (var pet in _equippedPets)
    {
        float percent = pet.StaminaPercent;
        
        if (percent < lowestPercent)
        {
            lowestPercent = percent;
            lowestPet = pet;
        }
        // Tie-breaker: lowest OrbitIndex
        else if (Mathf.Approximately(percent, lowestPercent) && 
                 pet.OrbitIndex < lowestPet.OrbitIndex)
        {
            lowestPet = pet;
        }
    }
    
    return lowestPet;
}
```

**Example scenario:**
```
Pet A: 80/100 = 80%
Pet B: 30/100 = 30%
Pet C: 60/200 = 30%

Potion targets: Pet B (tie with C, but lower OrbitIndex)
```

**Edge cases:**
- No equipped pets → potion not consumed
- All pets at 100% → potion not consumed
- MaxStamina = 0 → percent = 0, eligible but useless

### 13.7 Persistence Schema

**PetSaveEntry:**
```csharp
[Serializable]
public class PetSaveEntry
{
    public string instanceId;
    public string petId;
    public int level;
    public long experience;
    public int evolutionStage;
    public bool isEquipped;
    public float currentStamina = -1f; // -1 = unset (v4 compat)
}
```

**Save flow:**
```csharp
saveData.Add(new PetSaveEntry {
    currentStamina = pet.CurrentStamina
});
```

**Load flow:**
```csharp
var pet = new PetRuntime(...);

// Restore stamina (backward compat)
if (entry.currentStamina >= 0f)
    pet.RestoreStamina(entry.currentStamina - pet.CurrentStamina);
```

**Backward compatibility:**
- Old saves (no `currentStamina` field) → default `-1` → load at full stamina
- New saves preserve exact stamina value

### 13.8 UI Integration

**Data source:**
```csharp
// PetRuntime properties
public float CurrentStamina { get; private set; }
public float MaxStamina { get; private set; }
public float StaminaPercent => MaxStamina > 0f ? CurrentStamina / MaxStamina : 0f;
```

**UI implementation (future):**
```csharp
public class PetUI : MonoBehaviour
{
    [SerializeField] private Image _staminaBar;
    [SerializeField] private TextMeshProUGUI _staminaText;
    
    public void RefreshStamina(PetRuntime pet)
    {
        _staminaBar.fillAmount = pet.StaminaPercent;
        _staminaText.text = $"{pet.CurrentStamina:F0}/{pet.MaxStamina:F0}";
        
        // Visual feedback
        _staminaBar.color = pet.StaminaPercent < 0.3f 
            ? Color.red 
            : Color.cyan;
    }
}
```

### 13.9 Extension Guide

**Adding stamina to new pet:**
```json
{
  "id": "pet_new",
  "maxStamina": 100,
  "staminaRegen": 2.0,
  "behaviorDefinitions": [...]
}
```

**Adding stamina cost to skill:**
```json
{
  "action": {
    "type": "BasicAttack",
    "damageMultiplier": 1.0,
    "staminaCost": 20
  }
}
```

**Modifying potion values:**
Edit `dataConsumables.json`:
```json
{
  "Id": "potion_sp_large",
  "PotionType": 3,
  "PercentValue": 10,
  "FlatValue": 200
}
```

### 13.10 Design Rationale

**Why percentage-based targeting?**
- Fair across different pet max stamina values
- Prevents always targeting low-max pets
- Example: 30/100 (30%) vs 60/200 (30%) → both equally depleted

**Why no stamina overflow to other pets?**
- Simpler mental model
- Prevents complex cascade logic
- One transaction = one pet

**Why regeneration instead of skill-based restore?**
- Passive recovery reduces micromanagement
- Encourages strategic timing
- Different regen rates create pet identity

**Balance considerations:**
- Tank: High pool, slow regen → sustained presence
- DPS: Medium pool, fast regen → burst windows
- Support: Varies by role (Blood Bat: fast regen = spam heals)

---

## 14. REFERENCES

- **CLAUDE.md §56** — Pet System architectural overview
- **dataPet.json** — Pet definitions database (includes stamina config)
- **dataConsumables.json** — Stamina potion definition
- **dataPlayer.json** — Player stats for emergency threshold
- **dataEnemy.json** — Enemy roles/stats for targeting
- **ProjectilePool.cs** — Projectile pooling system
- **EnemyStatusEffectController.cs** — Status effect application
- **PetRuntime.cs** — Stamina runtime state implementation
- **PetManager.cs** — Stamina regeneration tick, potion targeting
- **PotionPanelController.cs** — Stamina potion consumption handler
