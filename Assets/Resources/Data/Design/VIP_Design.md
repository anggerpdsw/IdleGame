# VIP System Design — IdleDefenseSurvival

**Purpose:** VIP flags (daily, maxSpeed, autoCollect), integration points, extension guide.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [DailyReward_Design.md](./DailyReward_Design.md) — daily flag effect
- [Crafting_Design.md](./Crafting_Design.md) — VIP modifier integration
- [Economy_Design.md](./Economy_Design.md) — potential VIP currency bonuses

---

## 1. VIP Identity

**Owner:** `Scripts/Data/VIPData.cs` (plain serializable class)

**Schema:**

```csharp
public class VipData
{
    public bool daily;        // Daily reward claim-while-Waiting override
    public bool maxSpeed;     // Game speed cap 5.5x → 7.5x
    public bool autoCollect;  // Reserved Phase 2: item auto-collect delay
}
```

**Persistence:** `SaveData.account.vip` (single `VipData` instance per save — no per-account VIP level/tier model yet)

**Access:** `SaveManager.Instance.GetVipData()` or domain-specific pass-throughs

---

## 2. VIP Flags

### 2.1 `daily` Flag

**Effect:** Forces `DailyRewardService` state `Waiting → Claimable`.

**Integration (verified `DailyRewardService.GetState`):**

```csharp
if (utcNow.Ticks < _saveData.nextUnlockUtcTicks)
{
    // Still on cooldown
    if (SaveManager.Instance.IsDailyEnabled())
        return RewardState.Claimable;  // VIP override
    else
        return RewardState.Waiting;
}
```

**Result:** VIP players skip 5-min cooldown between daily rewards — all 7 claimable immediately.

---

### 2.2 `maxSpeed` Flag

**Effect:** Raises `GameSpeedController` max from **5.5×** → **7.5×**.

**Integration (verified `GameSpeedController.CheckVIP`):**

```csharp
public void CheckVIP()
{
    float maxSpeed = SaveManager.Instance.GetVipData().maxSpeed ? 7.5f : 5.5f;
    
    // Re-clamp current speed to new max
    _currentSpeed = Mathf.Clamp(_currentSpeed, 1.0f, maxSpeed);
    
    UpdateSpeedUI();
}
```

**Called:** When VIP state changes, or on scene load.

---

### 2.3 `autoCollect` Flag

**Status:** **Reserved for Phase 2**.

**Design intent:** Reduce delay before item auto-collection (e.g., drop bag items collected faster, or idle rewards auto-claimed).

**Current integration:** None (flag exists but no consumer yet).

---

## 3. Integration Points

**Verified live consumers:**

| Consumer | Behavior | File |
|----------|----------|------|
| `DailyRewardService` | `IsDailyEnabled()` forces `Waiting → Claimable` | `Scripts/Daily/DailyRewardService.cs` |
| `GameSpeedController` | `CheckVIP()` re-clamps max speed | `Scripts/Controller/GameSpeedController.cs` |
| `CraftContextBuilder` | Reads VIP via context for modifier aggregation | `Scripts/Crafting/CraftContextBuilder.cs` |
| `CraftRecipeData` | `JobTag.VIP = 8` reserved for VIP-gated recipes | `Scripts/Crafting/CraftRecipeData.cs` |
| `SaveManager.IsDailyEnabled` | Pass-through accessor to `_currentVip.daily` | `Scripts/Manager/SaveManager.cs` |

---

## 4. VIP Acquisition

**Current implementation:** No in-game purchase flow visible in read-first scan.

**Design options:**

### 4.1 Direct Toggle (Dev/Debug)

```csharp
void ToggleVIP(VipFlag flag)
{
    VipData vip = SaveManager.Instance.GetVipData();
    
    switch (flag)
    {
        case VipFlag.Daily:
            vip.daily = !vip.daily;
            break;
        case VipFlag.MaxSpeed:
            vip.maxSpeed = !vip.maxSpeed;
            GameSpeedController.Instance?.CheckVIP();
            break;
        case VipFlag.AutoCollect:
            vip.autoCollect = !vip.autoCollect;
            break;
    }
    
    SaveManager.SaveAll();
}
```

---

### 4.2 Currency Purchase

```csharp
public bool PurchaseVIP(VipFlag flag, long gemCost)
{
    // 1. Check cost
    if (!EconomyService.HasCurrency(CurrencyType.Gem, gemCost))
    {
        ShowError("Not enough gems");
        return false;
    }
    
    // 2. Remove currency
    if (!EconomyService.RemoveCurrency(CurrencyType.Gem, gemCost, "VIP purchase"))
        return false;
    
    // 3. Grant VIP flag
    VipData vip = SaveManager.Instance.GetVipData();
    
    switch (flag)
    {
        case VipFlag.Daily:
            vip.daily = true;
            break;
        case VipFlag.MaxSpeed:
            vip.maxSpeed = true;
            GameSpeedController.Instance?.CheckVIP();
            break;
        case VipFlag.AutoCollect:
            vip.autoCollect = true;
            break;
    }
    
    SaveManager.SaveAll();
    
    // 4. Show confirmation
    ShowSuccess($"VIP {flag} unlocked!");
    
    return true;
}
```

**Gem costs (example):**
- Daily: 500 gems (permanent unlock)
- MaxSpeed: 300 gems
- AutoCollect: 200 gems

---

### 4.3 Real-Money IAP (Future)

**Flow:**
1. Player clicks "Buy VIP" button
2. Unity IAP opens platform purchase dialog
3. On purchase success → receipt validation
4. Grant VIP flags permanently
5. Save + sync across devices (if cloud save)

---

## 5. VIP UI

**Suggested VIP panel (MainMenu):**

```
VIP Benefits

[✓] Daily Rewards — Skip cooldowns
    Status: Active

[✗] Max Speed Boost — 7.5× speed
    [Unlock: 300 Gems]

[✗] Auto Collect — Faster pickup
    Status: Coming Soon
```

**Icons:**
- Active: green checkmark
- Locked: gray lock icon
- Purchaseable: gem icon + price

---

## 6. VIP Expiry (Optional Future)

**Current design:** VIP flags are **permanent** (bool, no expiry).

**Optional extension:** Time-limited VIP (e.g., 30-day subscription).

**Schema change:**

```csharp
public class VipData
{
    public bool daily;
    public bool maxSpeed;
    public bool autoCollect;
    
    // Optional expiry tracking
    public long dailyExpiryUtcTicks;
    public long maxSpeedExpiryUtcTicks;
    public long autoCollectExpiryUtcTicks;
}
```

**Check on session start:**

```csharp
void ValidateVIPExpiry()
{
    VipData vip = SaveManager.Instance.GetVipData();
    long now = DateTime.UtcNow.Ticks;
    
    if (vip.daily && vip.dailyExpiryUtcTicks > 0 && now >= vip.dailyExpiryUtcTicks)
    {
        vip.daily = false;
        ShowNotification("Daily VIP expired");
    }
    
    // ... repeat for other flags
    
    SaveManager.SaveAll();
}
```

**YAGNI:** Avoid complexity unless subscription model required.

---

## 7. Extending VIP

**To add new VIP feature:**

1. Add field to `VipData` (default `false`)
2. Newtsoft round-trip via `SaveManager` handles new bool field without migration — old saves load with `false`
3. Add consumer call site under right domain owner — **never let UI flip VIP state directly**. Toggle goes through `SaveManager` or explicit VIP service.
4. If perk gates a feature, surface it in `MainMenuController` (VIP button) as explicit on/off
5. Update this document's integration table

**Example (new `fasterCrafting` flag):**

```csharp
// 1. Add to VipData
public bool fasterCrafting;

// 2. Integrate in CraftContextBuilder
if (SaveManager.Instance.GetVipData().fasterCrafting)
    context.CraftSpeedMultiplier *= 1.5f;

// 3. Add UI toggle in VIP panel
```

---

## 8. Testing Checklist

```
[ ] Daily VIP skips 5-min cooldown
[ ] MaxSpeed VIP raises cap to 7.5×
[ ] AutoCollect flag exists but unused (Phase 2)
[ ] VIP flags persist across save/load
[ ] VIP purchase deducts gems correctly
[ ] VIP toggle updates UI immediately
[ ] GameSpeedController re-clamps on VIP toggle
[ ] Crafting reads VIP modifiers correctly
[ ] No VIP exploits (e.g., toggle on/off repeatedly)
```

---

## 9. Common Issues

### Issue: Daily VIP doesn't skip cooldown
**Cause:** `IsDailyEnabled()` returns false, or DailyRewardService not reading flag.
**Fix:** Verify VIP flag set, check state logic in `GetState`.

### Issue: MaxSpeed VIP doesn't raise cap
**Cause:** `CheckVIP()` not called after flag change.
**Fix:** Call `GameSpeedController.Instance?.CheckVIP()` after toggling `maxSpeed`.

### Issue: VIP purchase takes gems but doesn't grant flag
**Cause:** Flag not set after transaction, or save not called.
**Fix:** Verify flag assignment, call `SaveManager.SaveAll()`.

### Issue: VIP lost after restart
**Cause:** VIP data not saved, or save corrupted.
**Fix:** Verify `SaveData.account.vip` persistence, check save serialization.

---

## 10. Future Extensions

### VIP Levels
- VIP 1, 2, 3 with cumulative benefits

### VIP Subscription
- Monthly renewal via IAP

### VIP Rewards
- Exclusive daily gifts for VIP players

### VIP Cosmetics
- Special skins, effects for VIP

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
