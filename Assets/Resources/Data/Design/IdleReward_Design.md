# Idle Reward System Design — IdleDefenseSurvival

**Purpose:** Offline reward accumulation (Gold + Meat only), 4h cap, tier/wave scaling.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Reward_Design.md](./Reward_Design.md) — reward types
- [Economy_Design.md](./Economy_Design.md) — currency granting
- [Wave_Design.md](./Wave_Design.md) — tier/wave multipliers
- [VIP_Design.md](./VIP_Design.md) — VIP multiplier (if implemented)

---

## 1. Idle Reward Identity

**Scope:** Idle reward grants **ONLY Gold + Meat**. EXP is NOT part of offline calculation.

**Owner files:**
- `Scripts/IdleReward/IdleRewardManager.cs` — singleton MonoBehaviour, DontDestroyOnLoad
- `Scripts/IdleReward/IdleRewardData.cs` — persistence schema
- `Scripts/IdleReward/IdleRewardUI.cs` — display only

**Public surface (verified):**
- `GoldReward` (property)
- `MeatReward` (property)
- `CanClaim` (bool)
- `Progress` (0-1 float for UI bar)

**No EXP property** — confirms EXP not granted offline.

---

## 2. Persistence Schema

**Data class (`IdleRewardData.cs`):**

```csharp
public class IdleRewardData
{
    public long lastClaimUtcTicks = DateTime.UtcNow.Ticks;  // UTC ticks
    public int maxDurationSeconds = 4 * 3600;                // 4h cap (14,400s)
    public int minimumClaimSeconds = 600;                    // 10 min before claim available
    public float rewardMultiplier = 1f;                      // VIP/bonus multiplier
}
```

**Stored in:** `SaveData.idleReward` (owned by `SaveManager.GetIdleRewardData()`)

**Must survive:** App close, scene transitions.

---

## 3. Accumulation Logic

**Accumulated time calculation:**

```csharp
public int GetAccumulatedSeconds()
{
    TimeSpan elapsed = DateTime.UtcNow - new DateTime(_data.lastClaimUtcTicks);
    int totalSeconds = (int)elapsed.TotalSeconds;
    
    // Cap at 4h
    return Math.Min(totalSeconds, _data.maxDurationSeconds);
}
```

**4h cap means:**
- Player offline 24h → gets only 4h worth of rewards
- Player offline 2h → gets 2h worth

---

## 4. Gold Formula

**Verified from `IdleRewardManager.CalculateGoldReward`:**

```csharp
int highestTier = SaveManager.Instance.GetHighestUnlockedTier();
int highestWaveInTier = SaveManager.Instance.GetHighestWave(highestTier);

// Total wave progress across all tiers
int totalWaveProgress = (highestTier - 1) * MAX_WAVE_PER_TIER + highestWaveInTier;

// Wave multiplier: 1.0 + (totalWaveProgress / MAX_WAVE_PER_TIER)
float waveMultiplier = 1.0f + (float)totalWaveProgress / MAX_WAVE_PER_TIER;

// Tier multiplier: 1.35^(tier - 1)
float tierMultiplier = Mathf.Pow(1.35f, highestTier - 1);

// Gold per minute
float goldPerMinute = 15.0f * tierMultiplier * waveMultiplier;

// Total gold
float minutes = GetAccumulatedSeconds() / 60f;
long gold = Mathf.RoundToInt(goldPerMinute * minutes * _data.rewardMultiplier);

return gold;
```

**Key points:**
- Base rate: **15 gold/minute**
- Tier multiplier: **1.35^(tier-1)** (exponential)
- Wave multiplier: **1.0 + (progress / 350)**
- Uses **highest tier/wave reached**, not current

**Example calculation:**
- Tier 2, Wave 100
- totalWaveProgress = (2-1)×350 + 100 = **450**
- waveMultiplier = 1.0 + 450/350 = **2.286**
- tierMultiplier = 1.35^(2-1) = **1.35**
- goldPerMinute = 15 × 1.35 × 2.286 = **46.3 gold/min**
- Offline 2h (120 min) = 46.3 × 120 = **5,556 gold**

---

## 5. Meat Formula

**Simple ratio:**

```csharp
public int CalculateMeatReward()
{
    long gold = CalculateGoldReward();
    return Mathf.RoundToInt(gold / 30f);
}
```

**Meat = Gold / 30**. No separate scaling.

**Example:**
- Gold: 5,556
- Meat: 5,556 / 30 = **185**

---

## 6. Claim Availability

**Minimum accumulation: 10 minutes** before claim available.

```csharp
public bool IsClaimAvailable()
{
    return GetAccumulatedSeconds() >= _data.minimumClaimSeconds;  // 600s
}
```

**UI progress bar:**

```csharp
public float Progress
{
    get
    {
        float accumulated = GetAccumulatedSeconds();
        return Mathf.Clamp01(accumulated / _data.minimumClaimSeconds);
    }
}
```

**Example:**
- Offline 5 min → Progress = 5/10 = **50%**, claim disabled
- Offline 10 min → Progress = 10/10 = **100%**, claim enabled
- Offline 4h → Progress = 240/10 = **100%** (capped at 1.0)

---

## 7. Claim Flow

**Player claims idle reward:**

```csharp
public void ClaimReward()
{
    // 1. Check available
    if (!IsClaimAvailable())
        return;
    
    // 2. Calculate rewards
    long gold = CalculateGoldReward();
    int meat = CalculateMeatReward();
    
    // 3. Grant currency
    EconomyManager.Instance.AddCurrency(CurrencyType.Gold, gold, "Idle reward");
    EconomyManager.Instance.AddCurrency(CurrencyType.Meat, meat, "Idle reward");
    
    // 4. Reset timer
    ResetCount();
    
    // 5. Show UI popup
    IdleRewardUI.Show(gold, meat);
}

public void ResetCount()
{
    _data.lastClaimUtcTicks = DateTime.UtcNow.Ticks;
    SaveManager.Instance.SaveAll();
}
```

**Important:** `ResetCount()` does NOT grant rewards — granting happens in the caller (e.g., `ClaimReward` or UI handler).

---

## 8. VIP Integration

**Multiplier:** `_data.rewardMultiplier` defaults to **1.0**.

**Current code scan:** No visible VIP write to `rewardMultiplier` in `IdleRewardManager`. If VIP boosts idle rewards, check:
- `SaveManager.GetIdleRewardData().rewardMultiplier`
- Or external VIP service sets it

**Design intent:** VIP could set `rewardMultiplier = 1.5` (50% bonus), but verify implementation.

---

## 9. UI Display

**Show on return from offline:**

```
Welcome Back!

You were away for: 2h 15m

Gold: +5,556
Meat: +185

[Claim]
```

**Claim button:**
- Disabled if < 10 min offline
- Enabled if ≥ 10 min
- Shows progress bar until 10 min reached

---

## 10. Persistence Requirement

**Data owned by `SaveManager.GetIdleRewardData()`:**
- Survives scene changes
- Survives app close/restart
- Independent of `IdleRewardManager` instance lifetime

**`IdleRewardManager` itself is DontDestroyOnLoad**, but data lives in SaveData.

---

## 11. Edge Cases

### 11.1 First session (no offline time)

**Result:** `GetAccumulatedSeconds() = 0`, `CanClaim = false`, progress bar empty.

---

### 11.2 Player claims immediately after launch

**Result:** If < 10 min offline, claim disabled. If ≥ 10 min, claim enabled.

---

### 11.3 Player leaves app open overnight

**Result:** `lastClaimUtcTicks` not updated until claim. On next claim:
- Accumulated = time since last claim (capped at 4h)
- Rewards granted for 4h max

---

### 11.4 4h cap prevents infinite farming

**Player offline 1 week:**
- Accumulated = 4h (capped)
- Gold = goldPerMinute × 240 min × multiplier

**Prevents abuse.**

---

## 12. Performance

**Idle reward is calculated on-demand:**
- UI open: calculate once, display
- Claim: calculate once, grant, reset timer

**No per-frame calculations.** No optimization needed.

---

## 13. Testing Checklist

```
[ ] Accumulated time calculated correctly (UtcNow - lastClaim)
[ ] 4h cap enforced (max 14,400s)
[ ] 10 min minimum before claim available
[ ] Gold formula uses highest tier/wave
[ ] TierMultiplier = 1.35^(tier-1)
[ ] WaveMultiplier = 1 + (progress / 350)
[ ] Meat = Gold / 30
[ ] Claim grants gold + meat
[ ] ResetCount stamps new lastClaimUtcTicks
[ ] Progress bar displays correctly (0-1)
[ ] VIP multiplier applies (if implemented)
[ ] Save/load preserves lastClaimUtcTicks
[ ] UI shows correct hours/minutes offline
[ ] Claim button disabled if < 10 min
```

---

## 14. Common Issues

### Issue: Idle reward too high/low
**Cause:** Formula wrong, or tier/wave not sourced correctly.
**Fix:** Verify `GetHighestUnlockedTier()`, check multiplier formulas.

### Issue: Claim always disabled
**Cause:** `GetAccumulatedSeconds()` returns 0, or minimum threshold not met.
**Fix:** Verify `lastClaimUtcTicks` saved correctly, check UTC time.

### Issue: 4h cap not working
**Cause:** `Math.Min(accumulated, maxDuration)` missing.
**Fix:** Verify cap logic in `GetAccumulatedSeconds()`.

### Issue: Meat reward zero
**Cause:** Gold / 30 rounds to 0 (gold < 30).
**Fix:** Expected at very low progress. Show "0" or hide Meat if zero.

---

## 15. Future Extensions

### Offline EXP
- Grant EXP offline (design currently excludes it)

### Idle Speed Boost
- VIP earns rewards faster (2× rate)

### Idle Cap Increase
- Unlock higher caps (8h, 12h) via progression

### Idle Missions
- Complete specific goals offline

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
