# Economy System Design — IdleDefenseSurvival

**Purpose:** Currency management, transactions, validation, persistence.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Reward_Design.md](./Reward_Design.md) — currency rewards
- [DailyReward_Design.md](./DailyReward_Design.md) — daily currency
- [IdleReward_Design.md](./IdleReward_Design.md) — offline currency
- [Card_Design.md](./Card_Design.md) — gem costs
- [Crafting_Design.md](./Crafting_Design.md) — gold costs

---

## 1. Economy Identity

**Owner:** `Scripts/Economy/EconomyManager.cs`

**Interface:** `Scripts/Core/Interfaces/IEconomyService.cs`

**Purpose:** Centralized currency mutation, validation, events.

**Currencies:** Gold, Gem, Meat, EXP

---

## 2. Currency Types

### 2.1 Gold

**Primary currency** for most operations.

**Sources:**
- Enemy kills
- Wave completion
- Daily rewards
- Idle rewards
- Selling items

**Uses:**
- Equipment crafting
- Equipment enhancement
- Equipment repair
- Card slot unlocks
- Shop purchases
- Attribute respec (if implemented)

**Cap:** No hard cap (effectively unlimited)

---

### 2.2 Gem

**Premium currency** (can be purchased with real money).

**Sources:**
- Daily rewards
- Wave milestones
- Achievements
- In-app purchase (if implemented)

**Uses:**
- Card rolling (20/190/1800 gems)
- Speed-ups (crafting, timers)
- Premium shop items
- VIP unlock (if gem-based)

**Cap:** No hard cap

---

### 2.3 Meat

**Secondary resource** for pet feeding.

**Sources:**
- Enemy kills
- Daily rewards
- Idle rewards

**Uses:**
- Pet feeding (see `PetSystem.md`)
- Special crafting recipes (if implemented)

**Cap:** No hard cap

---

### 2.4 EXP

**Player experience** for leveling.

**Sources:**
- Enemy kills
- Wave completion
- Daily rewards
- Quests

**Uses:**
- Player level-up (grants attribute points)

**Cap:** No cap, but level has soft cap at high values

---

## 3. Currency Storage

**Persistence:** `SaveData.account`

**Schema:**
```csharp
public class AccountData
{
    public long gold = 0;
    public long gem = 0;
    public long meat = 0;
    public long exp = 0;
    
    public int level = 1;
    public int unspentAttributePoints = 0;
}
```

**Use `long` (64-bit)** to prevent overflow at high values.

---

## 4. Currency Operations

### 4.1 Add Currency

```csharp
public bool AddCurrency(CurrencyType type, long amount, string reason)
{
    // 1. Validate amount
    if (amount <= 0)
    {
        Debug.LogWarning($"Invalid amount: {amount}");
        return false;
    }
    
    // 2. Get current value
    long current = GetCurrency(type);
    
    // 3. Add
    long newValue = current + amount;
    
    // 4. Check overflow (long max = 9,223,372,036,854,775,807)
    if (newValue < current)
    {
        Debug.LogError("Currency overflow");
        newValue = long.MaxValue;
    }
    
    // 5. Set new value
    SetCurrency(type, newValue);
    
    // 6. Track statistics
    TrackCurrencyEarned(type, amount);
    
    // 7. Fire event
    OnCurrencyChanged?.Invoke(type, newValue);
    
    // 8. Log
    AnalyticsManager.Instance?.LogCurrencyGain(type, amount, reason);
    
    // 9. Save
    SaveManager.SaveAll();
    
    return true;
}
```

**Reason examples:**
- `"Enemy kill"`
- `"Wave completion"`
- `"Daily reward"`
- `"Item sold"`
- `"Mission reward"`

---

### 4.2 Remove Currency

```csharp
public bool RemoveCurrency(CurrencyType type, long amount, string reason)
{
    // 1. Validate amount
    if (amount <= 0)
        return false;
    
    // 2. Check has enough
    long current = GetCurrency(type);
    if (current < amount)
    {
        Debug.LogWarning($"Insufficient {type}: need {amount}, have {current}");
        return false;
    }
    
    // 3. Subtract
    long newValue = current - amount;
    SetCurrency(type, newValue);
    
    // 4. Track statistics
    TrackCurrencySpent(type, amount);
    
    // 5. Fire event
    OnCurrencyChanged?.Invoke(type, newValue);
    
    // 6. Log
    AnalyticsManager.Instance?.LogCurrencySpend(type, amount, reason);
    
    // 7. Save
    SaveManager.SaveAll();
    
    return true;
}
```

**Reason examples:**
- `"Card roll"`
- `"Crafting cost"`
- `"Enhancement cost"`
- `"Shop purchase"`

---

### 4.3 Check Currency

```csharp
public bool HasCurrency(CurrencyType type, long amount)
{
    return GetCurrency(type) >= amount;
}
```

**Always check BEFORE attempting transaction.**

---

## 5. Transaction Pattern

**Correct flow:**

```csharp
// 1. Check cost
if (!EconomyService.HasCurrency(CurrencyType.Gold, cost))
{
    ShowError("Not enough gold");
    return false;
}

// 2. Perform action (validation)
if (!CanPerformAction())
{
    ShowError("Action invalid");
    return false;
}

// 3. Remove currency
bool success = EconomyService.RemoveCurrency(CurrencyType.Gold, cost, "Action name");
if (!success)
{
    ShowError("Transaction failed");
    return false;
}

// 4. Apply action effect
ApplyActionEffect();

// 5. Show result
ShowSuccess("Action completed");
```

**Do NOT remove currency before validation** — if action fails, currency is lost.

---

## 6. Currency UI

**Display format:**

```csharp
string FormatCurrency(long amount)
{
    if (amount >= 1_000_000_000)
        return $"{amount / 1_000_000_000.0:F1}B";
    else if (amount >= 1_000_000)
        return $"{amount / 1_000_000.0:F1}M";
    else if (amount >= 1_000)
        return $"{amount / 1_000.0:F1}K";
    else
        return amount.ToString();
}
```

**Examples:**
- 500 → `"500"`
- 1,234 → `"1.2K"`
- 5,678,900 → `"5.7M"`
- 1,234,567,890 → `"1.2B"`

**Update triggers:**
- Subscribe to `OnCurrencyChanged` event
- Update all currency displays

---

## 7. Currency Rewards

**From combat:**

```csharp
void OnEnemyKilled(EnemyAi enemy)
{
    // 1. Get base rewards
    int goldReward = enemy.GoldReward;
    int meatReward = enemy.MeatReward;
    int expReward = enemy.ExpReward;
    
    // 2. Apply multipliers (wave, tier, bonuses)
    goldReward = ApplyGoldMultipliers(goldReward);
    meatReward = ApplyMeatMultipliers(meatReward);
    expReward = ApplyExpMultipliers(expReward);
    
    // 3. Grant rewards
    EconomyService.AddCurrency(CurrencyType.Gold, goldReward, "Enemy kill");
    EconomyService.AddCurrency(CurrencyType.Meat, meatReward, "Enemy kill");
    EconomyService.AddCurrency(CurrencyType.Exp, expReward, "Enemy kill");
}
```

**Multipliers:**
- Wave/tier progression
- Card bonuses (`Gold%`, `Meat%`)
- Equipment bonuses
- Buff effects
- VIP bonuses

---

## 8. Currency Statistics

**Track lifetime stats:**

```csharp
public class CurrencyStatistics
{
    public long TotalGoldEarned;
    public long TotalGoldSpent;
    public long TotalGemEarned;
    public long TotalGemSpent;
    public long TotalMeatEarned;
    public long TotalMeatSpent;
    public long TotalExpEarned;
}
```

**Uses:**
- Achievements
- Milestones
- Analytics
- Idle reward calculation (highest gold/meat)

---

## 9. Currency Validation

**Prevent exploits:**

```csharp
bool ValidateTransaction(CurrencyType type, long amount, string reason)
{
    // 1. Amount must be positive
    if (amount <= 0)
    {
        LogSuspicious($"Invalid amount: {amount}");
        return false;
    }
    
    // 2. Reason must be provided
    if (string.IsNullOrEmpty(reason))
    {
        LogSuspicious("Missing reason");
        return false;
    }
    
    // 3. Check for unreasonable amounts (exploit detection)
    if (amount > SUSPICIOUS_THRESHOLD)
    {
        LogSuspicious($"Suspicious amount: {amount} from {reason}");
        // May allow but flag for review
    }
    
    return true;
}
```

---

## 10. EXP and Leveling

**EXP → Level conversion:**

```csharp
void AddExp(long amount, string reason)
{
    // 1. Add EXP
    EconomyService.AddCurrency(CurrencyType.Exp, amount, reason);
    
    // 2. Check level-up
    while (CanLevelUp())
    {
        LevelUp();
    }
}

bool CanLevelUp()
{
    long currentExp = GetCurrency(CurrencyType.Exp);
    long requiredExp = GetExpRequiredForLevel(CurrentLevel + 1);
    
    return currentExp >= requiredExp;
}

void LevelUp()
{
    // 1. Increment level
    CurrentLevel++;
    
    // 2. Grant attribute points
    UnspentAttributePoints += GameConstants.POINTS_PER_LEVEL;  // +5
    
    // 3. Fire event
    OnLevelUp?.Invoke(CurrentLevel);
    
    // 4. Show UI
    LevelUpUI.Show(CurrentLevel);
    
    // 5. Save
    SaveManager.SaveAll();
}
```

**EXP curve:**
- Level 1→2: 100 EXP
- Level 2→3: 200 EXP
- Level 3→4: 350 EXP
- ... (exponential or polynomial curve)

**Verify:** Check `dataPlayer.json` or exp curve data for exact values.

---

## 11. Currency Events

**Events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnCurrencyChanged` | Currency value changed | UI display, statistics |
| `OnCurrencyEarned` | Currency added | Popup, sound effect |
| `OnCurrencySpent` | Currency removed | Statistics |
| `OnLevelUp` | Player leveled up | UI, attribute panel |

**Subscribe pattern:**
```csharp
EconomyManager.Instance.OnCurrencyChanged += UpdateCurrencyUI;
```

---

## 12. Performance

**Currency operations are cheap:**
- Simple arithmetic
- Dictionary lookup
- Event dispatch

**No optimization needed** unless thousands of transactions per frame.

---

## 13. Testing Checklist

```
[ ] Add currency increases value correctly
[ ] Remove currency decreases value correctly
[ ] Cannot remove more than owned
[ ] Overflow handled (long.MaxValue cap)
[ ] Negative amounts rejected
[ ] Transaction validation works
[ ] Currency UI updates on change
[ ] Events fire correctly
[ ] EXP grants level-ups
[ ] Level-up grants attribute points
[ ] Statistics track correctly
[ ] Save/load preserves currency values
[ ] Analytics logs transactions
```

---

## 14. Common Issues

### Issue: Currency doesn't update
**Cause:** UI not subscribed to event, or event not fired.
**Fix:** Subscribe to `OnCurrencyChanged`, verify event invoked.

### Issue: Negative currency
**Cause:** Remove called without checking balance.
**Fix:** Always check `HasCurrency()` before remove.

### Issue: Currency overflow
**Cause:** Adding to near-max value.
**Fix:** Check for overflow, clamp to `long.MaxValue`.

### Issue: EXP doesn't level up
**Cause:** Level-up check not called, or exp curve wrong.
**Fix:** Call level-up check after adding EXP, verify curve.

---

## 15. Future Extensions

### Currency Exchange
- Convert gold ↔ gems at fixed rate

### Currency Caps
- Maximum gold/gem storage (forces spending)

### Currency Decay
- Lose % of gold per day (idle penalty)

### Multiple Wallets
- Separate currencies per game mode

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
