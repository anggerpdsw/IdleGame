# Spawn System Design — IdleDefenseSurvival

**Purpose:** Enemy spawning mechanics, spawn weights, spawn timing, spawn positioning.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Wave_Design.md](./Wave_Design.md) — wave progression and difficulty
- [Enemy_Design.md](./Enemy_Design.md) — enemy stats and behavior
- [Combat_Design.md](./Combat_Design.md) — enemy scaling

---

## 1. Spawn System Identity

**Owner:** `Scripts/Enemy/EnemySpawner.cs`

**Purpose:** Continuously spawn enemies during ActiveWave based on wave config.

**Core mechanics:**
- Weighted random selection
- Spawn rate (enemies per second)
- Spawn count (total per wave)
- Spawn positioning (outside player range)
- Pooling (reuse enemy instances)

---

## 2. Spawn Flow

**During ActiveWave:**

```
1. WaveManager starts ActiveWave
2. EnemySpawner.StartSpawning()
3. Every spawnInterval seconds:
   a. Check if spawnCount reached
   b. Select random enemy from pool (weighted)
   c. Calculate spawn position
   d. Instantiate or get from pool
   e. Initialize enemy with wave/tier scaling
   f. Register in EnemySpatialGrid
4. When wave ends OR spawnCount reached:
   Stop spawning
```

---

## 3. Spawn Rate

**Spawn interval calculation:**

```csharp
float spawnInterval = 1f / spawnRate;  // seconds between spawns
```

**Example:**
- Spawn rate: 2.0 enemies/second
- Interval: 1.0 / 2.0 = **0.5 seconds**

**With ProgressionSpeed:**
```csharp
float effectiveSpawnRate = spawnRate * ProgressionSpeed;
float spawnInterval = 1f / effectiveSpawnRate;
```

**Example (5× speed):**
- Base rate: 2.0/s
- Effective rate: 2.0 × 5 = **10.0/s**
- Interval: 1.0 / 10.0 = **0.1 seconds**

---

## 4. Spawn Count

**Total enemies to spawn per wave:**

```csharp
int remainingSpawns = _spawnCount;

void Update()
{
    if (remainingSpawns <= 0) return;
    
    _spawnTimer += Time.deltaTime;
    
    if (_spawnTimer >= _spawnInterval)
    {
        _spawnTimer = 0f;
        SpawnEnemy();
        remainingSpawns--;
    }
}
```

**Wave ends when:**
- Wave duration expires (victory), OR
- Player dies (defeat)

**Spawn count does NOT block wave completion** — remaining enemies may be alive when wave ends.

---

## 5. Enemy Pool (Weighted Selection)

**Data source:** `dataWave.json` → `enemyPool` array

**Schema example:**
```json
{
  "enemyPool": [
    {
      "enemyId": "enemy_goblin",
      "weight": 1000.0
    },
    {
      "enemyId": "enemy_orc",
      "weight": 500.0
    },
    {
      "enemyId": "enemy_troll",
      "weight": 100.0
    }
  ]
}
```

**Weight interpretation:**
- Higher weight = more likely to spawn
- Weights are **relative** (not percentages)

**Total weight:**
```
Total = 1000 + 500 + 100 = 1600
```

**Probabilities:**
- Goblin: 1000/1600 = **62.5%**
- Orc: 500/1600 = **31.25%**
- Troll: 100/1600 = **6.25%**

---

## 6. Weighted Random Selection

**Algorithm:**

```csharp
string SelectRandomEnemy(List<EnemySpawnEntry> pool)
{
    // 1. Calculate total weight
    float totalWeight = 0f;
    foreach (var entry in pool)
        totalWeight += entry.weight;
    
    // 2. Roll random value [0, totalWeight)
    float roll = Random.Range(0f, totalWeight);
    
    // 3. Find which enemy the roll lands on
    float cumulative = 0f;
    foreach (var entry in pool)
    {
        cumulative += entry.weight;
        if (roll < cumulative)
            return entry.enemyId;
    }
    
    // Fallback (should never reach)
    return pool[0].enemyId;
}
```

**Example roll:**
- Total weight: 1600
- Roll: 850
- Cumulative: 0 → 1000 (Goblin) → 850 < 1000 → **Goblin selected**

**Example roll 2:**
- Roll: 1200
- Cumulative: 0 → 1000 (Goblin) → 1500 (Orc) → 1200 < 1500 → **Orc selected**

---

## 7. Spawn Positioning

**Goal:** Spawn outside player's attack range, avoid overlap.

**Algorithm:**

```csharp
Vector2 CalculateSpawnPosition()
{
    // 1. Random angle around player
    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
    
    // 2. Distance: player attack range + buffer
    float distance = PlayerStatsManager.GetFinalStat(AttackRange) + _spawnBuffer;
    
    // 3. Calculate position
    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    Vector2 spawnPos = (Vector2)Player.Instance.transform.position + offset;
    
    // 4. Clamp to arena bounds (optional)
    spawnPos = ClampToArenaBounds(spawnPos);
    
    return spawnPos;
}
```

**Spawn buffer:** Extra distance to ensure enemy is out of range (e.g., +2 units).

**Visual:** Enemies spawn in a circle around the player.

---

## 8. Enemy Initialization

**After spawn:**

```csharp
void SpawnEnemy()
{
    // 1. Select enemy type
    string enemyId = SelectRandomEnemy(_enemyPool);
    
    // 2. Get from pool or instantiate
    GameObject enemyObj = EnemyPool.Get(enemyId);
    
    // 3. Position
    enemyObj.transform.position = CalculateSpawnPosition();
    
    // 4. Get EnemyAi component
    EnemyAi enemy = enemyObj.GetComponent<EnemyAi>();
    
    // 5. Apply wave/tier scaling
    ApplyScaling(enemy, CurrentWave, CurrentTier);
    
    // 6. Activate
    enemyObj.SetActive(true);
    
    // 7. Register in spatial grid
    EnemySpatialGrid.Instance.Register(enemy);
}
```

---

## 9. Wave/Tier Scaling

**Scaling formula:**

```csharp
void ApplyScaling(EnemyAi enemy, int wave, int tier)
{
    // 1. Get base stats from EnemyData
    EnemyData data = EnemyDatabase.Get(enemy.EnemyId);
    
    // 2. Calculate multipliers
    float waveMultiplier = Utilityku.WaveMultiplier(DecayCount, wave, MaxWave);
    float tierMultiplier = CalculateTierMultiplier(tier);
    
    // 3. Apply to stats
    enemy.MaxHP = data.healthPoint * waveMultiplier * tierMultiplier;
    enemy.CurrentHP = enemy.MaxHP;
    enemy.Damage = data.damage * waveMultiplier * tierMultiplier;
    enemy.Defense = data.defense * waveMultiplier * tierMultiplier;
    // ... other stats
}
```

**Tier multiplier example:**
```csharp
float CalculateTierMultiplier(int tier)
{
    return Mathf.Pow(1.5f, tier - 1);  // 1.0×, 1.5×, 2.25×, 3.375×, ...
}
```

**Do NOT hardcode scaling** — use authoritative `Utilityku.WaveMultiplier`.

---

## 10. Boss Spawning

**Special case:** Boss waves spawn ONE boss instead of many enemies.

```csharp
void SpawnBoss(string bossId)
{
    // 1. Get boss prefab
    GameObject bossObj = EnemyPool.Get(bossId);
    
    // 2. Position (center or fixed location)
    bossObj.transform.position = CalculateBossSpawnPosition();
    
    // 3. Apply scaling
    EnemyAi boss = bossObj.GetComponent<EnemyAi>();
    ApplyBossScaling(boss, CurrentWave, CurrentTier);
    
    // 4. Mark as boss
    boss.SetBossFlag(true);
    
    // 5. Activate
    bossObj.SetActive(true);
}
```

**Boss scaling:** Typically higher multiplier than normal enemies (e.g., 10-20× HP).

---

## 11. Spawn Limits

**Performance consideration:** Too many enemies = lag.

**Caps:**
- **Soft cap:** Spawn count per wave (e.g., 100-200)
- **Hard cap:** Max alive enemies (e.g., 5000)

**Hard cap enforcement:**
```csharp
void SpawnEnemy()
{
    if (GetAliveEnemyCount() >= _maxAliveEnemies)
    {
        // Wait until some die
        return;
    }
    
    // Proceed with spawn
}
```

**WARNING:** Do NOT use arbitrary cap to hide performance issues. Fix performance instead.

---

## 12. Pooling

**Enemy pooling:**

```csharp
public class EnemyPool
{
    private Dictionary<string, Queue<GameObject>> _pools = new();
    
    public GameObject Get(string enemyId)
    {
        if (_pools.TryGetValue(enemyId, out var pool) && pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        
        // Pool empty, instantiate new
        return Instantiate(GetEnemyPrefab(enemyId));
    }
    
    public void Return(string enemyId, GameObject obj)
    {
        obj.SetActive(false);
        
        if (!_pools.ContainsKey(enemyId))
            _pools[enemyId] = new Queue<GameObject>();
        
        _pools[enemyId].Enqueue(obj);
    }
}
```

**Benefits:**
- Avoid Instantiate/Destroy spam
- Reuse enemy instances
- Better performance at 5000+ enemies

---

## 13. Spawn Events

**Events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnEnemySpawned` | Enemy instantiated | Statistics, mission progress |
| `OnSpawnStarted` | Wave spawn begins | UI, audio |
| `OnSpawnStopped` | Wave spawn ends | UI |

**Subscribe pattern:**
```csharp
EnemySpawner.Instance.OnEnemySpawned += HandleEnemySpawn;
```

---

## 14. Performance

**High-frequency system** — spawns multiple enemies per second.

**Optimizations:**
- **Pooling:** Reuse instances
- **Batch spawning:** Spawn multiple at once (careful with position overlap)
- **Spawn throttling:** Cap alive enemies
- **Spatial grid:** O(1) overlap checks

**Do NOT:**
- Instantiate every enemy fresh
- Check all enemies for overlap (use spatial grid)
- Spawn infinite enemies (enforce cap)

---

## 15. Testing Checklist

```
[ ] Enemies spawn at correct rate
[ ] Spawn count limits work
[ ] Weighted selection probabilities correct
[ ] Spawn positioning outside player range
[ ] No overlap with other enemies
[ ] Wave/tier scaling applies correctly
[ ] Boss spawns at correct waves
[ ] Pooling reuses instances
[ ] Spawn stops when wave ends
[ ] Spawn cap prevents infinite spawning
[ ] Events fire correctly
[ ] Performance holds at 5000+ enemies
```

---

## 16. Common Issues

### Issue: All enemies spawn same type
**Cause:** Weight selection broken, or only one enemy in pool.
**Fix:** Verify weighted random algorithm, check `enemyPool` array.

### Issue: Enemies spawn inside player range
**Cause:** Spawn buffer too small, or calculation wrong.
**Fix:** Increase `_spawnBuffer`, verify position calculation.

### Issue: Enemies overlap
**Cause:** No separation, or spawn position same.
**Fix:** Add random offset, enable separation steering.

### Issue: Spawn rate too fast
**Cause:** ProgressionSpeed too high, or interval calculation wrong.
**Fix:** Clamp ProgressionSpeed, verify `1 / spawnRate` formula.

---

## 17. Future Extensions

### Dynamic Spawn Positions
- Spawn from specific directions (north, south, east, west)
- Wave-specific spawn patterns

### Spawn Waves
- Burst spawning (all at once)
- Staggered spawning (groups over time)

### Enemy Variants
- Random stat rolls per spawn
- Elite chance (rare high-stat enemy)

### Spawn Portals
- Visual portal animation at spawn location

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
