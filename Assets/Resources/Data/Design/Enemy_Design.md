# Enemy System Design — IdleDefenseSurvival

**Purpose:** Enemy AI, spawning, movement, combat behavior, status effects, death handling.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Combat_Design.md](./Combat_Design.md) — damage calculation
- [Wave_Design.md](./Wave_Design.md) — enemy scaling
- [Spawn_Design.md](./Spawn_Design.md) — spawn mechanics
- [StatusEffect_Design.md](./StatusEffect_Design.md) — status system
- [Reward_Design.md](./Reward_Design.md) — drop rewards
- [EnemyDrop_Design.md](./EnemyDrop_Design.md) — drop tables

---

## 1. Enemy Identity

**Primary scripts:**
- `Scripts/Enemy/EnemyAi.cs` — orchestrator (827 lines, 67+ public API)
- `Scripts/Enemy/EnemySpawner.cs` — spawn management
- `Scripts/Enemy/EnemyData.cs` — data container
- `Scripts/Enemy/EnemyStatusEffectController.cs` — status handling

**Supporting services:**
- `EnemySpatialGrid.cs` — O(1) neighbor lookup
- `EnemyMovementCalculator.cs` — pure math (seek/flee/separation)
- `EnemyAuraVisualController.cs` — visual pulse animation
- `EnemyDeathHandler.cs` — 10-step death sequence
- `EnemyRewardDistributor.cs` — currency/material drops

**Data source:** `Assets/Resources/Data/dataEnemy.json`

---

## 2. Enemy Roles

**Verified from dataEnemy.json:**

| Role | Behavior | Stats Profile |
|------|----------|---------------|
| Normal | Standard melee | Balanced |
| Tanky | High HP, low damage | High HP, High Defense |
| Damage | High damage, low HP | Low HP, High Attack |
| Boss | Wave/tier boss | Very high HP, boss modifiers |

**Role affects:**
- Base stats
- Spawn weights
- Reward multipliers
- Visual indicators

---

## 3. Enemy Stats

**Base stats (from JSON):**
- HealthPoint
- Damage
- Defense
- AttackSpeed
- AttackRange
- MoveSpeed
- Evasion
- Element (if implemented)

**Scaling:** Wave/tier multipliers applied at spawn time (see `Wave_Design.md`).

**Formula:**
```
FinalStat = BaseStat × WaveMultiplier × TierMultiplier
```

---

## 4. Spawning

**Spawner:** `EnemySpawner.cs`

**Spawn flow:**

```
1. WaveManager triggers spawn
2. EnemySpawner.SpawnEnemy(enemyId, tier, wave)
3. Load EnemyData from JSON
4. Apply wave/tier scaling
5. Instantiate prefab
6. Initialize EnemyAi
7. Position outside player AttackRange
8. Register in EnemySpatialGrid
9. Start AI loop
```

**Spawn position:**
- Random point outside player's attack range
- Uses spawn radius (configurable)
- Ensures enemy not overlapping with others

**See:** `Spawn_Design.md` for spawn weights and timing.

---

## 5. Movement Behavior

**Steering system** (not pathfinding):

```
Seek: Move toward player
Flee: Move away from player (if HP low)
Separation: Avoid overlapping with other enemies
```

**Service:** `EnemyMovementCalculator.cs` (pure math, no state)

**Input:**
- Enemy position
- Player position
- Nearby enemy positions (from `EnemySpatialGrid`)
- Enemy speed
- Weights (seek/flee/separation)

**Output:** Movement direction vector

**Applied in EnemyAi:**
```csharp
void UpdateMovement()
{
    Vector2 direction = EnemyMovementCalculator.Calculate(
        transform.position,
        _playerPosition,
        _nearbyEnemies,
        _moveSpeed,
        _seekWeight,
        _separationWeight
    );
    
    transform.position += (Vector3)direction * Time.deltaTime;
}
```

---

## 6. Attack Behavior

**Attack flow:**

```
1. Enemy approaches player
2. Stop when within enemy's AttackRange
3. Every AttackSpeed seconds:
   a. Roll hit chance vs player Evasion
   b. If hit:
      - Calculate damage
      - Apply to player HP
   c. Else: Miss
4. Player knockback pushes enemy away
5. Re-approach and repeat
```

**Attack timer:**
```csharp
void Update()
{
    _attackTimer += Time.deltaTime;
    
    float interval = 1f / _attackSpeed;
    
    if (_attackTimer >= interval)
    {
        _attackTimer = 0f;
        AttemptAttack();
    }
}
```

**Hit chance:**
```csharp
bool RollHit()
{
    float hitChance = Mathf.Clamp(_hitRate - Player.Instance.GetEvasion(), 5f, 100f);
    return Random.Range(0f, 100f) < hitChance;
}
```

---

## 7. Taking Damage

**Damage flow:**

```csharp
public void TakeDamage(float amount)
{
    // 1. Apply damage
    _currentHP -= amount;
    
    // 2. Show damage popup
    DamagePopup.Spawn(transform.position, amount.ToString(), Color.red);
    
    // 3. Update health bar
    _healthBar.SetHealth(_currentHP / _maxHP);
    
    // 4. Check death
    if (_currentHP <= 0)
    {
        Die();
    }
    
    // 5. Fire event
    OnDamageTaken?.Invoke(amount);
}
```

**Defense:** Already factored into damage calculation in `Projectile.cs` (see `Combat_Design.md`).

---

## 8. Status Effects

**Controller:** `EnemyStatusEffectController.cs`

**Supported statuses:**
- Slow (reduces MoveSpeed + AttackSpeed)
- Defense Break (reduces Defense)
- Stun (freezes movement + attacks)
- HeartBreak / Max Health Reduction

**Apply status:**
```csharp
public void ApplyStatus(IStatusEffect effect)
{
    _statusController.AddEffect(effect);
}
```

**Status types:**
- **Permanent:** Never expires
- **Aura:** Expires when source leaves range
- **Temporary:** Expires after duration

**See:** `StatusEffect_Design.md` for full mechanics.

---

## 9. Death Handling

**Service:** `EnemyDeathHandler.cs` (10-step sequence)

**Death flow:**

```
1. Stop movement
2. Stop attacks
3. Disable collider
4. Play death animation
5. Distribute rewards (gold, meat, materials)
6. Fire OnEnemyKilled event
7. Update statistics
8. Remove from EnemySpatialGrid
9. Wait for animation
10. Return to pool or destroy
```

**Rewards:** `EnemyRewardDistributor.cs` handles currency + material drops (see `EnemyDrop_Design.md`).

---

## 10. Enemy Types

### 10.1 Normal Enemies

**Spawn:** Throughout wave
**HP:** Base × multipliers
**Damage:** Base × multipliers
**Reward:** Base gold/meat

### 10.2 Elite Enemies

**Spawn:** Less frequent, higher tier
**HP:** 2-3× normal
**Damage:** 1.5× normal
**Defense:** Higher defense
**Reward:** 2× gold/meat
**Damage reduction:** 15% (player deals 85% damage)

### 10.3 Boss Enemies

**Spawn:** End of tier or special waves
**HP:** 10-20× normal
**Damage:** 2-3× normal
**Defense:** Very high
**Reward:** 5-10× gold/meat + guaranteed materials
**Damage reduction:** 30% (player deals 70% damage)
**Visual:** Larger sprite, boss health bar, aura

---

## 11. Spatial Grid

**Purpose:** O(1) neighbor lookup for separation steering.

**Service:** `EnemySpatialGrid.cs`

**How it works:**
- Grid divides arena into cells
- Each enemy registers in its cell
- Separation query only checks same + adjacent cells
- Uses Cantor pairing hash for position → cell ID

**Registration:**
```csharp
void Start()
{
    EnemySpatialGrid.Instance.Register(this);
}

void OnDestroy()
{
    EnemySpatialGrid.Instance.Unregister(this);
}
```

**Query:**
```csharp
List<EnemyAi> nearby = EnemySpatialGrid.Instance.GetNearby(transform.position, radius);
```

---

## 12. Health Bar

**Visual:** UI bar above enemy.

**Pooling:** Health bars pooled separately.

**Update:**
```csharp
_healthBar.SetHealth(_currentHP / _maxHP);  // 0-1 range
```

**Visibility:**
- Always visible for bosses
- Hidden when HP = max for normal enemies (optional)

---

## 13. Aura Visual

**Component:** `EnemyAuraVisualController.cs`

**Purpose:** Pulse animation for boss/elite enemies.

**Behavior:**
- Circle sprite scales up/down
- Color tint (red for boss, yellow for elite)
- Continuous loop

**Not used for normal enemies.**

---

## 14. Performance

**Target:** 2000-5000+ enemies on screen.

**Optimizations:**
- **Pooling:** Reuse enemy instances
- **Spatial grid:** O(1) neighbor lookup
- **Layer masks:** Filter physics queries
- **Update throttling:** Not every enemy updates every frame (optional)
- **LOD:** Distant enemies skip certain updates (optional)

**Do NOT:**
- Add arbitrary enemy cap to hide performance issues
- Use `FindObjectsOfType` every frame
- Recalculate stats every frame (cache at spawn)

---

## 15. Enemy Data Schema

**File:** `Assets/Resources/Data/dataEnemy.json`

**Schema:**
```json
{
  "id": "enemy_goblin",
  "name": "Goblin",
  "role": "Normal",
  "element": "None",
  "healthPoint": 50.0,
  "damage": 5.0,
  "defense": 5.0,
  "attackSpeed": 1.0,
  "attackRange": 2.0,
  "moveSpeed": 3.0,
  "evasion": 5.0,
  "spawnWeight": 1000.0,
  "goldReward": 10,
  "meatReward": 1,
  "expReward": 5,
  "dropItems": [
    {
      "ItemId": "coal",
      "Weight": 0.7815,
      "MinTier": 1
    }
  ]
}
```

**Important:** `dropItems.Weight` is 0-1 range (0.7815 = 78.15%), NOT 0-100.

---

## 16. Testing Checklist

```
[ ] Enemy spawns outside player attack range
[ ] Enemy moves toward player
[ ] Enemy stops at its own attack range
[ ] Enemy attacks at correct AttackSpeed
[ ] Enemy damage applies to player HP
[ ] Enemy takes damage from projectiles
[ ] Enemy health bar updates correctly
[ ] Status effects apply (slow, stun, defense break)
[ ] Enemy dies at HP = 0
[ ] Death triggers reward distribution
[ ] Rewards match dataEnemy.json
[ ] Boss has damage reduction (70%)
[ ] Elite has damage reduction (85%)
[ ] Spatial grid registration/unregistration
[ ] No overlap with other enemies (separation)
[ ] Performance holds at 5000+ enemies
```

---

## 17. Common Issues

### Issue: Enemies overlap
**Cause:** Separation steering disabled, or spatial grid not working.
**Fix:** Verify separation weight, check spatial grid registration.

### Issue: Enemies don't move
**Cause:** MoveSpeed = 0, or movement calculation broken.
**Fix:** Verify MoveSpeed stat, check movement direction vector.

### Issue: Enemies one-shot player
**Cause:** Damage scaling too high, or defense not applied.
**Fix:** Verify wave/tier multipliers, check player defense.

### Issue: Boss dies too fast
**Cause:** Boss damage reduction not applied.
**Fix:** Verify `IsBoss()` check, apply 0.70× modifier in damage calculation.

---

## 18. Future Extensions

### Ranged Enemies
- Attack from distance, no melee approach

### Flying Enemies
- Ignore ground obstacles

### Summoner Enemies
- Spawn minions

### Shield Enemies
- Temporary damage immunity

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
