# Wave System Design — IdleDefenseSurvival

**Purpose:** Wave progression, tier system, difficulty scaling, inter-wave/active-wave cycle.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Spawn_Design.md](./Spawn_Design.md) — spawn timing and weights
- [Enemy_Design.md](./Enemy_Design.md) — enemy stats scaling
- [Reward_Design.md](./Reward_Design.md) — wave completion rewards
- [Combat_Design.md](./Combat_Design.md) — difficulty modifiers

---

## 1. Wave Identity

**Owner:** `Scripts/Manager/WaveManager.cs`

**Data source:** `Assets/Resources/Data/dataWave.json`

**Core concepts:**
- **CurrentWave:** 1-350 within current tier
- **CurrentTier:** 1, 2, 3, ... (infinite progression)
- **Wave cycle:** InterWave → ActiveWave → InterWave → repeat
- **Difficulty scaling:** Wave + Tier multipliers

---

## 2. Wave Limits

**Constants (verified `Constantku.cs`, `GameConstants.cs`):**

```csharp
MAX_WAVE_PER_TIER = 350
```

**Wave progression:**
- Wave 1-350 in Tier 1
- After Wave 350: advance to Tier 2, reset to Wave 1
- Wave 1-350 in Tier 2
- Repeat infinitely

**Wave clamping (verified `WaveManager.cs:79`):**
```csharp
CurrentWave = Mathf.Clamp(CurrentWave, 1, _maxWave);
```

**Do NOT assume 100 waves per tier** — old documentation was wrong. Current verified limit is **350**.

---

## 3. Wave Cycle

**Two-phase cycle:**

### Phase 1: InterWave (Preparation)

**Duration:** `_interWaveDuration` seconds (configurable, scaled by `ProgressionSpeed`)

**Player actions:**
- Heal HP/Mana
- Open menus (inventory, cards, equipment)
- Craft items
- Allocate attributes
- Prepare for next wave

**No enemies spawn.**

**UI:** Shows countdown timer, "Next Wave" button (skip to ActiveWave early).

### Phase 2: ActiveWave (Combat)

**Duration:** `_waveDuration` seconds (configurable, scaled by `ProgressionSpeed`)

**Enemy spawning:** Continuous spawns based on wave config.

**Victory condition:** Survive until timer ends.

**Defeat condition:** Player HP → 0.

**At end:** Return to InterWave.

---

## 4. Wave Data Schema

**File:** `Assets/Resources/Data/dataWave.json`

**Schema example:**
```json
{
  "tier": 1,
  "wave": 1,
  "interWaveDuration": 30.0,
  "activeDuration": 60.0,
  "spawnRate": 2.0,
  "spawnCount": 10,
  "enemyPool": [
    {
      "enemyId": "enemy_goblin",
      "weight": 1000.0
    },
    {
      "enemyId": "enemy_orc",
      "weight": 500.0
    }
  ],
  "bossId": null,
  "difficultyMultiplier": 1.0
}
```

**Note:** Not every wave has a JSON entry — waves can interpolate from neighbors or use default config.

---

## 5. Difficulty Scaling

**Wave multiplier formula (verified `Utilityku.WaveMultiplier`):**

```
WaveMultiplier = Utilityku.WaveMultiplier(DecayCount, CurrentWave, MaxWave)
```

**Exact formula:** Check `Utilityku.cs` — do NOT duplicate this formula in other systems.

**Tier multiplier:** Applied on top of wave multiplier.

**Final enemy stats:**
```
FinalStat = BaseStat × WaveMultiplier × TierMultiplier
```

**Example (HP scaling):**
- Base HP: 50
- Wave 100 multiplier: ~2.5× (example)
- Tier 2 multiplier: 1.5× (example)
- Final HP: 50 × 2.5 × 1.5 = **187.5**

**Difficulty progression is exponential** — later waves become significantly harder.

---

## 6. Wave Progress Fraction

**Formula (verified `WaveManager.GetWaveProgressMultiplier`):**

```csharp
float progress = Mathf.Clamp01((CurrentWave - 1f) / (_maxWave - 1f));
```

**Usage:**
- Reward interpolation
- Difficulty interpolation
- UI progress bar

**Example:**
- Wave 1: (1-1)/(350-1) = **0.0** (0%)
- Wave 175: (175-1)/(350-1) = **0.498** (~50%)
- Wave 350: (350-1)/(350-1) = **1.0** (100%)

---

## 7. Tier Progression

**Tier advancement:**

```csharp
void AdvanceTier()
{
    if (CurrentWave >= _maxWave)
    {
        CurrentTier++;
        CurrentWave = 1;
        
        OnTierAdvanced?.Invoke(CurrentTier);
        SaveProgress();
    }
}
```

**Tier 2+ behavior:**
- Enemy stats scale higher
- New enemy types may unlock
- Better rewards
- Harder difficulty

**No tier limit** — progression is infinite.

---

## 8. Wave Completion

**Victory flow:**

```csharp
void CompleteWave()
{
    // 1. Stop enemy spawning
    EnemySpawner.StopSpawning();
    
    // 2. Destroy remaining enemies (optional)
    // OR: let player finish them
    
    // 3. Calculate rewards
    CalculateWaveRewards();
    
    // 4. Update statistics
    WaveStatistics.RecordWaveCleared(CurrentWave, CurrentTier);
    
    // 5. Update mission progress
    MissionService.Instance?.UpdateProgress(MissionEventType.WaveCompleted, "", 1);
    
    // 6. Advance wave
    CurrentWave++;
    
    // 7. Check tier advancement
    if (CurrentWave > _maxWave)
        AdvanceTier();
    
    // 8. Save progress
    SaveProgress();
    
    // 9. Enter InterWave
    StartInterWave();
    
    // 10. Fire event
    OnWaveCompleted?.Invoke(CurrentWave, CurrentTier);
}
```

---

## 9. Wave Defeat

**Defeat flow:**

```csharp
void OnPlayerDeath()
{
    // 1. Stop spawning
    EnemySpawner.StopSpawning();
    
    // 2. Pause game
    Time.timeScale = 0;
    
    // 3. Show defeat UI
    DefeatUI.Show(CurrentWave, CurrentTier);
    
    // 4. Calculate run rewards
    CalculateRunRewards();
    
    // 5. Fire event
    OnWaveDefeated?.Invoke();
}
```

**No automatic restart** — player chooses restart or quit.

---

## 10. Boss Waves

**Boss spawn conditions:**
- Specific wave numbers (e.g., Wave 50, 100, 150, ...)
- End of tier (Wave 350)
- Special event waves

**Boss behavior:**
- Single powerful enemy instead of many weak enemies
- Higher HP, damage, defense
- Guaranteed material drops
- Unique visual (aura, larger sprite)

**Boss flag in wave config:**
```json
{
  "wave": 50,
  "bossId": "boss_orc_chieftain",
  "spawnCount": 1
}
```

---

## 11. Spawn Rate and Count

**Spawn rate:** Enemies per second during ActiveWave.

```csharp
float spawnInterval = 1f / _spawnRate;  // seconds between spawns
```

**Spawn count:** Total enemies to spawn during wave.

**Example:**
- Wave duration: 60s
- Spawn rate: 2.0 enemies/second
- Max possible spawns: 60 × 2 = **120 enemies**
- Spawn count cap: 100 (config)
- Result: 100 enemies spawn over 60s (1 every 0.6s)

**See:** `Spawn_Design.md` for detailed spawn mechanics.

---

## 12. Progression Speed

**VIP feature:** `ProgressionSpeed` multiplier.

**Default:** 1.0× (normal speed)

**VIP:** Up to 7.5× (verified `GameSpeedController.cs`)

**Effect:**
- InterWave duration: `_interWaveDuration / ProgressionSpeed`
- ActiveWave duration: `_waveDuration / ProgressionSpeed`
- Spawn rate: `_spawnRate × ProgressionSpeed`

**Example (5× speed):**
- InterWave: 30s → **6s**
- ActiveWave: 60s → **12s**
- Spawn rate: 2/s → **10/s**

**WARNING:** High speed may cause performance issues (too many enemies).

---

## 13. Wave Statistics

**Tracked stats:**
- Highest wave reached (per tier)
- Highest tier reached
- Total waves cleared
- Total enemies killed
- Total gold earned
- Total time played

**Source:** `Scripts/Manager/EnemyStatisticsManager.cs` + wave tracking in `WaveManager`.

**Persistence:** Saved in `SaveData.statistics` (or equivalent).

---

## 14. Wave Events

**Events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnWaveStarted` | ActiveWave begins | UI, spawner, audio |
| `OnWaveCompleted` | ActiveWave ends (victory) | UI, rewards, missions |
| `OnWaveDefeated` | Player dies | UI, game over screen |
| `OnTierAdvanced` | Tier increases | UI, unlock system |
| `OnInterWaveStarted` | InterWave begins | UI, crafting |

**Subscribe pattern:**
```csharp
WaveManager.Instance.OnWaveCompleted += HandleWaveComplete;
```

---

## 15. Persistence

**Saved data:**
- `CurrentWave`
- `CurrentTier`
- `HighestWaveReached` (per tier)
- `HighestTierReached`
- Wave statistics

**Save triggers:**
- Wave completion
- Tier advancement
- Player death
- Manual save

**Load on game start:**
- Restore last wave/tier
- Player continues from last checkpoint

---

## 16. Performance

**Wave system is relatively cheap:**
- Updates once per second (timer)
- No per-frame calculations

**Performance bottleneck:** Enemy spawning and combat, NOT wave management.

**Optimization:** Use events to notify systems instead of polling.

---

## 17. Testing Checklist

```
[ ] InterWave → ActiveWave → InterWave cycle works
[ ] Wave advances after ActiveWave ends
[ ] Tier advances after Wave 350
[ ] Wave resets to 1 after tier advancement
[ ] CurrentWave clamped [1, 350]
[ ] Difficulty scales with wave/tier
[ ] Boss spawns at correct waves
[ ] Wave completion triggers rewards
[ ] Player death triggers defeat UI
[ ] ProgressionSpeed affects durations
[ ] Wave progress persists across restarts
[ ] Statistics update correctly
[ ] Events fire at correct times
```

---

## 18. Common Issues

### Issue: Wave never ends
**Cause:** ActiveWave timer not running, or enemies still spawning.
**Fix:** Verify timer logic, check spawn stop condition.

### Issue: Tier never advances
**Cause:** Wave cap not checked, or tier advancement logic broken.
**Fix:** Verify `CurrentWave > _maxWave` condition.

### Issue: Difficulty too easy/hard
**Cause:** Wave/tier multipliers incorrect.
**Fix:** Verify `Utilityku.WaveMultiplier` formula, adjust base stats.

### Issue: Boss doesn't spawn
**Cause:** Boss wave config missing, or spawn logic wrong.
**Fix:** Verify `dataWave.json` boss entries, check spawn conditions.

---

## 19. Future Extensions

### Endless Mode
- No tier cap, infinite progression

### Challenge Waves
- Special modifiers (all enemies elite, time limit, no healing)

### Wave Modifiers
- Random buffs/debuffs per wave

### Wave Selection
- Replay specific waves for rewards

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
