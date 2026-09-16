# Daily Reward System Design — IdleDefenseSurvival

**Purpose:** 7-reward sequence within 1 day, cooldown-based progression, VIP integration.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Reward_Design.md](./Reward_Design.md) — reward granting
- [Economy_Design.md](./Economy_Design.md) — currency addition
- [VIP_Design.md](./VIP_Design.md) — VIP daily flag
- [Mission_Design.md](./Mission_Design.md) — mission reward comparison

---

## 1. Daily Reward Identity

**Critical distinction:** Daily Reward is **7 rewards in 1 day**, NOT a 7-day login streak.

**Owner files:**
- `Scripts/Daily/DailyRewardService.cs` — logic, state management
- `Scripts/Daily/DailyRewardManager.cs` — UI controller
- `Scripts/Daily/DailyRewardSaveData.cs` — persistence
- `Scripts/Daily/DailyRewardSlot.cs` — individual slot UI
- `Scripts/Daily/DailyRewardUI.cs` — panel UI

**Verified constants (`Scripts/Utilities/Constantku.cs`):**

```csharp
REWARD_COUNT = 7
COOLDOWN_MINUTES = 5
DAILY_GOLD_REWARD = 10_000      // NOT 100,000
DAILY_MEAT_REWARD = 500          // NOT 1,000
DAILY_EXP_REWARD = 2_500         // NOT 3,000
DATE_FORMAT = "yyyy-MM-dd"
```

---

## 2. Reward Sequence

**7 rewards, claimed sequentially (verified `DailyRewardProvider.GetReward`):**

| Index | Type | Grant Method | Amount Rule |
|-------|------|--------------|-------------|
| **0** | Gold | `EconomyManager.AddCurrency(Gold, amount, "Daily reward")` | `Max(DAILY_GOLD_REWARD, HighestGoldEarned)` |
| **1** | Gem | `EconomyManager.AddCurrency(Gem, 11, ...)` | **Hardcoded 11** |
| **2** | Meat | `EconomyManager.AddCurrency(Meat, amount, ...)` | `Max(DAILY_MEAT_REWARD, HighestMeatEarned)` |
| **3** | Item | `InventoryManager.AddItem("CardRoll", 1)` | Free card roll ticket |
| **4** | EXP | `AccountManager.AddExp(amount, ...)` | `Max(DAILY_EXP_REWARD, HighestExpEarned / 2 * tier)` |
| **5** | Item | `InventoryManager.AddItem("UltimateStone", 3)` | **3 random UltimateStones** |
| **6** | Item | `InventoryManager.AddItem("SkinShard", 1)` | Skin exchange currency |

---

## 3. Reward 5 (UltimateStone) Details

**Rolls 3× independently from 8 variants:**

```csharp
string[] variants = new string[]
{
    "UltimateStone_None",
    "UltimateStone_Metal",
    "UltimateStone_Wood",
    "UltimateStone_Fire",
    "UltimateStone_Water",
    "UltimateStone_Earth",
    "UltimateStone_Lightning",
    "UltimateStone_Wind"
};

for (int i = 0; i < 3; i++)
{
    int index = UnityEngine.Random.Range(0, variants.Length);
    string stoneId = variants[index];
    InventoryManager.AddItem(stoneId, 1);
}
```

**Important:** `UltimateStone_None` is a valid roll = **no-op token**. No filter currently exists to prevent it.

---

## 4. Reward State Machine

**3 states per slot:**

| State | Condition | UI | Actions |
|-------|-----------|----|------------|
| **Locked** | `currentRewardIndex < slotIndex` | Grayed out, padlock icon | None |
| **Waiting** | `currentRewardIndex == slotIndex` AND `UtcNow < nextUnlockUtcTicks` | Countdown timer | None |
| **Claimable** | `currentRewardIndex == slotIndex` AND `UtcNow >= nextUnlockUtcTicks` | Green button | Click to claim |

**VIP override:** `IsDailyEnabled()` forces `Waiting → Claimable`.

---

## 5. Cooldown Mechanics

**5-minute cooldown between claims:**

```csharp
public void ClaimReward(int index)
{
    // 1. Validate state
    if (GetState(index, DateTime.UtcNow) != RewardState.Claimable)
        return;
    
    // 2. Grant reward
    GrantReward(index);
    
    // 3. Advance index
    _saveData.currentRewardIndex++;
    
    // 4. Set next unlock time
    _saveData.nextUnlockUtcTicks = DateTime.UtcNow.AddMinutes(COOLDOWN_MINUTES).Ticks;
    
    // 5. Check completion
    if (_saveData.currentRewardIndex >= REWARD_COUNT)
    {
        _saveData.completedToday = true;
        // All 7 claimed, wait for daily reset
    }
    
    // 6. Save
    SaveManager.SaveAll();
}
```

---

## 6. Daily Reset

**Reset trigger:** UTC date string comparison.

```csharp
public void EnsureReset(DateTime utcNow)
{
    string today = utcNow.ToString(DATE_FORMAT);  // "yyyy-MM-dd"
    
    if (_saveData.lastResetDate != today)
    {
        // New day, reset everything
        _saveData.currentRewardIndex = 0;
        _saveData.completedToday = false;
        _saveData.claimedToday = 0;
        _saveData.nextUnlockUtcTicks = utcNow.Ticks;  // Immediate first claim
        _saveData.lastResetDate = today;
        
        SaveManager.SaveAll();
    }
}
```

**Called:** On every `DailyRewardService` method entry (lazy reset check).

---

## 7. Persistence Schema

**Data class (`DailyRewardSaveData.cs`):**

```csharp
public class DailyRewardSaveData
{
    public int currentRewardIndex = 0;       // 0-7 (0-6 active, 7 = all claimed)
    public long nextUnlockUtcTicks = 0;      // DateTime.UtcNow.Ticks for next claim
    public bool completedToday = false;      // All 7 claimed
    public string lastResetDate = "";        // "yyyy-MM-dd"
    public int claimedToday = 0;             // Counter, mostly informational
}
```

**Stored in:** `SaveData.dailyReward` (or equivalent)

**Must survive:**
- Scene changes
- App close
- App restart

**Eligibility is NEVER derived from UI state** — `DailyRewardService.GetState(utcNow)` is source of truth.

---

## 8. VIP Integration

**VIP flag:** `SaveManager.Instance.IsDailyEnabled()`

**Effect:**

```csharp
public RewardState GetState(int index, DateTime utcNow)
{
    if (index < _saveData.currentRewardIndex)
        return RewardState.Claimed;  // Already claimed
    
    if (index > _saveData.currentRewardIndex)
        return RewardState.Locked;   // Future reward
    
    // index == currentRewardIndex (next reward)
    
    if (_saveData.completedToday)
        return RewardState.Claimed;  // All done today
    
    // Check cooldown
    if (utcNow.Ticks < _saveData.nextUnlockUtcTicks)
    {
        // Still on cooldown
        if (SaveManager.Instance.IsDailyEnabled())
            return RewardState.Claimable;  // VIP override
        else
            return RewardState.Waiting;
    }
    
    return RewardState.Claimable;
}
```

**VIP players skip cooldown** — all rewards claimable immediately.

---

## 9. UI Flow

**Player opens Daily Reward panel:**

```
1. DailyRewardService.EnsureReset(UtcNow)  // Check daily reset
2. For each slot (0-6):
   a. Get state (Locked/Waiting/Claimable)
   b. Update slot UI (icon, state color, timer)
3. If state = Waiting:
   a. Start countdown timer (updates every 1s)
   b. Show "MM:SS remaining"
4. If state = Claimable:
   a. Button enabled, green color
   b. Show "Claim" text
5. Player clicks slot:
   a. Call DailyRewardService.ClaimReward(index)
   b. Grant reward via RewardManager.Show() or direct grant
   c. Advance to next reward
   d. Refresh all slots
```

---

## 10. Scaling Logic

**Rewards scale with player progress:**

### 10.1 Gold (Reward 0)

```csharp
long goldAmount = Math.Max(
    DAILY_GOLD_REWARD,
    SaveManager.Instance.GetHighestGoldEarned()
);
```

**Example:**
- New player (Wave 1): 10,000 gold
- Advanced player (Wave 200, highest 500,000): **500,000 gold**

---

### 10.2 Meat (Reward 2)

```csharp
long meatAmount = Math.Max(
    DAILY_MEAT_REWARD,
    SaveManager.Instance.GetHighestMeatEarned()
);
```

---

### 10.3 EXP (Reward 4)

```csharp
long expAmount = Math.Max(
    DAILY_EXP_REWARD,
    SaveManager.Instance.GetHighestExpEarned() / 2 * currentTier
);
```

**Scales with tier** to stay relevant.

---

## 11. Completion Behavior

**After claiming reward 6:**

```csharp
if (_saveData.currentRewardIndex >= REWARD_COUNT)
{
    _saveData.completedToday = true;
    
    // All buttons disabled
    foreach (var slot in _slotUIs)
        slot.SetState(RewardState.Claimed);
    
    // Show "Come back tomorrow" message
    _completionText.gameObject.SetActive(true);
}
```

**Player must wait until daily reset** (UTC midnight) to claim again.

---

## 12. Edge Cases

### 12.1 Claim during cooldown (non-VIP)

**Result:** Button disabled, shows countdown timer.

---

### 12.2 Claim all 7 in rapid succession (VIP)

**Result:** All 7 claimed within seconds, `completedToday = true`, wait for reset.

---

### 12.3 Player closes app mid-sequence

**Result:** `currentRewardIndex` + `nextUnlockUtcTicks` saved. On next launch:
- If cooldown expired: next reward claimable
- If cooldown not expired: continue countdown

---

### 12.4 Daily reset mid-session

**Result:** `EnsureReset()` detects date change, resets all state, player sees reward 0 claimable again.

---

## 13. Performance

**Daily reward is UI-only:**
- State checks are simple date/time comparisons
- Countdown timer updates once per second (only while panel open)

**No optimization needed.**

---

## 14. Testing Checklist

```
[ ] All 7 rewards grant correct types/amounts
[ ] Rewards claimed sequentially (0→1→2→...→6)
[ ] 5-minute cooldown enforces between claims
[ ] Countdown timer displays correctly
[ ] VIP skips cooldown (immediate claim)
[ ] Daily reset at UTC midnight
[ ] After 7 claims, all buttons disabled
[ ] Save/load preserves state (index, cooldown, completion)
[ ] Reward 5 rolls 3 UltimateStones
[ ] Reward scaling uses highest earned
[ ] Date format "yyyy-MM-dd" stable
[ ] Close/reopen app preserves progress
```

---

## 15. Common Issues

### Issue: Rewards reset mid-day
**Cause:** Date comparison wrong, or timezone mismatch.
**Fix:** Use `DateTime.UtcNow`, verify date string format.

### Issue: Cooldown never expires
**Cause:** `nextUnlockUtcTicks` stored in wrong timezone, or comparison wrong.
**Fix:** Use UTC ticks consistently, verify `UtcNow.Ticks`.

### Issue: VIP doesn't skip cooldown
**Cause:** `IsDailyEnabled()` returns false, or state check wrong.
**Fix:** Verify VIP flag set, check state logic.

### Issue: Reward 5 always gives None
**Cause:** Random roll lands on `UltimateStone_None`.
**Fix:** Filter out `_None` variant in reward provider.

---

## 16. Future Extensions

### Weekly Bonus
- Extra reward after 7 consecutive days

### Skip Cooldown (Ads)
- Watch ad to skip cooldown (non-VIP)

### Reward Choices
- Pick 1 of 3 variants per slot

### Streak Bonuses
- Claim multiple days in a row for bonus

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
