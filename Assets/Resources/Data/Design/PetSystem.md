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

## 13. REFERENCES

- **CLAUDE.md §56** — Pet System architectural overview
- **dataPet.json** — Pet definitions database
- **dataPlayer.json** — Player stats for emergency threshold
- **dataEnemy.json** — Enemy roles/stats for targeting
- **ProjectilePool.cs** — Projectile pooling system
- **EnemyStatusEffectController.cs** — Status effect application
