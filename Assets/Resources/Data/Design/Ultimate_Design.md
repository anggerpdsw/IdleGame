# Ultimate System Design — IdleDefenseSurvival

**Purpose:** Powerful abilities with cooldowns — screen-clearing attacks, buffs, special effects.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Player_Design.md](./Player_Design.md) — ultimate triggering
- [Combat_Design.md](./Combat_Design.md) — damage application
- [Enemy_Design.md](./Enemy_Design.md) — enemy targeting

---

## 1. Ultimate Identity

**Owner files:**
- `Scripts/Ultimate/UltimateManager.cs` — registration host, spawning
- `Scripts/Ultimate/UltimateFactory.cs` — static registry of handlers
- `Scripts/Ultimate/IUltimateHandler.cs` — interface

**Handler implementations:** `Scripts/Ultimate/` (one file per ultimate)

**Data source:** `Assets/Resources/Data/Player/dataUltimate.json`

---

## 2. Ultimate Registry

**8 ultimate families (verified from `UltimateManager.Awake`):**

| ID | Handler | Behavior |
|----|---------|----------|
| `Void` | `VoidHandler.cs` | Black hole pulls + damages enemies |
| `Tank` | `TankHandler.cs` | Spawns defensive turret |
| `Root` | `RootHandler.cs` | Roots enemies in place |
| `Bomb` | `BombHandler.cs` | AoE explosion damage |
| `Fountain` | `FountainHandler.cs` | Healing fountain |
| `Cloud` | `CloudHandler.cs` | Damaging cloud DoT |
| `Lightning` | `LightningHandler.cs` | Chain lightning |
| `Shockwave` | `ShockwaveHandler.cs` | Radial knockback wave |

**Registration (in `UltimateManager.Awake`):**
```csharp
UltimateFactory.RegisterHandler("Void", new VoidHandler());
UltimateFactory.RegisterHandler("Tank", new TankHandler());
// ... etc
```

---

## 3. Ultimate Trigger

**Activation sources:**
1. **Manual:** Player presses ultimate button (mana cost)
2. **Automatic:** Random chance per enemy kill
3. **Cooldown-based:** Triggers every N seconds

**Current implementation:** Check `UltimateManager.Update()` for exact trigger conditions.

**Typical flow:**

```csharp
void Update()
{
    // Check cooldown
    _cooldownTimer += Time.deltaTime;
    
    if (_cooldownTimer >= _cooldownDuration)
    {
        _cooldownTimer = 0f;
        
        // Roll activation chance
        if (Random.Range(0f, 100f) < _activationChance)
        {
            TriggerRandomUltimate();
        }
    }
}
```

---

## 4. Ultimate Handler Interface

**Interface (`IUltimateHandler.cs`):**

```csharp
public interface IUltimateHandler
{
    void Activate(Vector2 position, UltimateData data);
    void Deactivate();
    bool IsActive { get; }
}
```

**Handler responsibilities:**
- Spawn visual effects
- Apply gameplay effects (damage, CC, healing)
- Handle duration/lifetime
- Clean up on deactivate

---

## 5. Ultimate Data Schema

**File:** `Assets/Resources/Data/Player/dataUltimate.json`

**Schema example:**
```json
{
  "id": "ultimate_void",
  "name": "Void Singularity",
  "description": "Creates a black hole that pulls and damages enemies",
  "cooldown": 60.0,
  "activationChance": 10.0,
  "activeDuration": 5.0,
  "damage": 100.0,
  "radius": 8.0,
  "element": "None",
  "manaCost": 50
}
```

**Fields:**
- `cooldown` — seconds between triggers
- `activationChance` — % chance to trigger when cooldown ready
- `activeDuration` — how long ultimate lasts (seconds)
- `damage` — base damage (if damage-dealing)
- `radius` — area of effect
- `element` — elemental type (affects damage calculation)
- `manaCost` — mana consumed on activation

---

## 6. Ultimate Examples

### 6.1 Void (Black Hole)

**Behavior:**
1. Spawn black hole at position
2. Pull nearby enemies toward center (force-based)
3. Deal damage per second to enemies inside
4. Last N seconds
5. Destroy

**Implementation notes:**
- Uses `AddForce` toward center
- Continuous damage tick (0.5s intervals)
- Visual: rotating vortex sprite

---

### 6.2 Bomb (AoE Explosion)

**Behavior:**
1. Spawn bomb projectile
2. Travel toward target position
3. Explode on arrival
4. Deal damage to all enemies in radius
5. Destroy

**Damage falloff:** None (flat damage to all in radius).

---

### 6.3 Lightning (Chain Lightning)

**Behavior:**
1. Select initial target (nearest enemy)
2. Deal damage
3. Jump to nearest unchained enemy within range
4. Repeat N times
5. Visual: lightning arc between targets

**Chain rules:**
- Max jumps: 5-10 (configurable)
- Damage reduction per jump: 0-20% (configurable)
- Cannot jump to same enemy twice

---

### 6.4 Fountain (Healing)

**Behavior:**
1. Spawn fountain at player position
2. Heal player HP per second
3. Last N seconds
4. Destroy

**Heal amount:** Config value or % of MaxHP.

---

### 6.5 Root (Crowd Control)

**Behavior:**
1. Apply Root status to all enemies in radius
2. Root prevents movement (enemies can still attack)
3. Last N seconds
4. Visual: roots growing from ground

**Note:** Uses enemy status system (see `StatusEffect_Design.md`).

---

### 6.6 Tank (Defensive Turret)

**Behavior:**
1. Spawn turret at position
2. Turret auto-attacks enemies in range
3. Turret has own HP pool
4. Last until destroyed or duration expires

**Turret stats:**
- AttackDamage
- AttackSpeed
- AttackRange
- HealthPoint

---

### 6.7 Cloud (Damage Over Time)

**Behavior:**
1. Spawn cloud at position
2. Enemies inside take DoT (damage per second)
3. Cloud slowly expands or moves
4. Last N seconds
5. Destroy

**Visual:** Semi-transparent poison/fire cloud.

---

### 6.8 Shockwave (Knockback)

**Behavior:**
1. Radial shockwave from player position
2. Push all enemies away (knockback)
3. Deal damage
4. Instant effect (no duration)

**Knockback force:** High enough to create space.

---

## 7. Ultimate Factory

**Static registry:**

```csharp
public static class UltimateFactory
{
    private static Dictionary<string, IUltimateHandler> _handlers = new();
    private static Dictionary<string, int> _activeCounts = new();
    
    public static void RegisterHandler(string id, IUltimateHandler handler)
    {
        _handlers[id] = handler;
        _activeCounts[id] = 0;
    }
    
    public static void Activate(string id, Vector2 position, UltimateData data)
    {
        if (!_handlers.TryGetValue(id, out var handler))
        {
            Debug.LogError($"Unknown ultimate: {id}");
            return;
        }
        
        handler.Activate(position, data);
        _activeCounts[id]++;
    }
    
    public static void Deactivate(string id)
    {
        if (_handlers.TryGetValue(id, out var handler))
        {
            handler.Deactivate();
            _activeCounts[id]--;
        }
    }
    
    public static int GetActiveCount(string id)
    {
        return _activeCounts.TryGetValue(id, out var count) ? count : 0;
    }
}
```

---

## 8. Ultimate Scaling

**Damage scaling:**

```
FinalDamage = BaseDamage × UltimateAttackModifier × PlayerAttackMultiplier
```

**UltimateAttackModifier:** From attributes (STR contributes).

**Example:**
- Base ultimate damage: 100
- UltimateAttack stat: +50%
- Result: 100 × 1.5 = **150**

**Duration scaling:** Usually fixed (not scaled by stats).

**Cooldown reduction:** May be affected by CDR stat (if implemented).

---

## 9. Ultimate Activation Flow

**In `UltimateManager`:**

```csharp
void TriggerRandomUltimate()
{
    // 1. Select random ultimate from unlocked pool
    string ultimateId = SelectRandomUltimate();
    
    // 2. Check mana cost
    if (Player.Instance.CurrentMana < GetManaCost(ultimateId))
        return;
    
    // 3. Consume mana
    Player.Instance.ConsumeMana(GetManaCost(ultimateId));
    
    // 4. Load data
    UltimateData data = UltimateDatabase.Get(ultimateId);
    
    // 5. Calculate position
    Vector2 position = CalculateUltimatePosition(ultimateId);
    
    // 6. Activate via factory
    UltimateFactory.Activate(ultimateId, position, data);
    
    // 7. Fire event
    OnUltimateActivated?.Invoke(ultimateId);
}
```

---

## 10. Ultimate Positioning

**Position logic per ultimate:**

| Ultimate | Position |
|----------|----------|
| Void | Random within arena |
| Tank | Near player |
| Root | Player position (radius) |
| Bomb | Farthest enemy cluster |
| Fountain | Player position |
| Cloud | Random within arena |
| Lightning | Nearest enemy |
| Shockwave | Player position |

**Smart positioning:**
```csharp
Vector2 CalculateUltimatePosition(string ultimateId)
{
    switch (ultimateId)
    {
        case "Bomb":
            return GetEnemyClusterCenter();
        case "Lightning":
            return GetNearestEnemyPosition();
        default:
            return Player.Instance.transform.position;
    }
}
```

---

## 11. Ultimate UI

**Display:**
- Cooldown timer (circular fill or number)
- Ultimate icon
- Activation indicator (flash/glow)
- Active count (if multiple can be active)

**Manual trigger button:** Greyed out if on cooldown or insufficient mana.

---

## 12. Ultimate Unlock

**Unlock methods:**
1. **Level-based:** Unlock at specific player levels
2. **Item-based:** Consume `UltimateStone` items (from daily rewards)
3. **Quest-based:** Complete missions

**Current implementation:** Check `UltimateManager` or unlock system.

**UltimateStone variants (verified `DailyRewardService.cs`):**
- `UltimateStone_None` (no-op)
- `UltimateStone_Metal`
- `UltimateStone_Wood`
- `UltimateStone_Fire`
- `UltimateStone_Water`
- `UltimateStone_Earth`
- `UltimateStone_Lightning`
- `UltimateStone_Wind`

**Each stone unlocks corresponding ultimate.**

---

## 13. Performance

**Ultimate effects are expensive:**
- Particle systems
- Force applications (multiple enemies)
- Continuous damage ticks

**Optimizations:**
- Pool ultimate prefabs
- Limit simultaneous active ultimates
- Use efficient collision queries (layer masks)

**Do NOT:**
- Check all enemies every frame for AoE (use trigger colliders)
- Allocate new arrays per tick (cache results)

---

## 14. Testing Checklist

```
[ ] All 8 ultimates registered in factory
[ ] Cooldown timer works correctly
[ ] Activation chance rolls correctly
[ ] Mana consumed on activation
[ ] Ultimate spawns at correct position
[ ] Damage applies to enemies
[ ] Duration expires correctly
[ ] Effects clean up on deactivate
[ ] Multiple ultimates can be active
[ ] UltimateAttack stat scales damage
[ ] Visual effects show correctly
[ ] Ultimate unlocks via UltimateStone
```

---

## 15. Common Issues

### Issue: Ultimate never triggers
**Cause:** Cooldown too long, or activation chance too low.
**Fix:** Verify cooldown duration, check chance roll logic.

### Issue: Ultimate deals no damage
**Cause:** Damage calculation wrong, or enemies not detected.
**Fix:** Verify damage formula, check collision/trigger detection.

### Issue: Ultimate doesn't end
**Cause:** Duration timer not running, or deactivate not called.
**Fix:** Verify timer logic, check deactivate call.

### Issue: Multiple ultimates overlap
**Cause:** No limit on active count.
**Fix:** Enforce max active count per ultimate type.

---

## 16. Future Extensions

### Ultimate Upgrades
- Increase damage, duration, radius with items

### Ultimate Combos
- Cast multiple ultimates together for bonus effects

### Ultimate Customization
- Choose which ultimates are in rotation

### Ultimate Mastery
- Gain XP for ultimates, unlock variants

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
