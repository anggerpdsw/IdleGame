# Reward System Design — IdleDefenseSurvival

**Purpose:** Reward calculation, distribution, types, integration points.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Economy_Design.md](./Economy_Design.md) — currency granting
- [Wave_Design.md](./Wave_Design.md) — wave completion rewards
- [DailyReward_Design.md](./DailyReward_Design.md) — daily reward sequence
- [IdleReward_Design.md](./IdleReward_Design.md) — offline rewards
- [Mission_Design.md](./Mission_Design.md) — mission rewards
- [EnemyDrop_Design.md](./EnemyDrop_Design.md) — enemy drops

---

## 1. Reward Identity

**Owner files:**
- `Scripts/Reward/RewardData.cs` — reward definition
- `Scripts/Reward/RewardManager.cs` — reward display/grant
- `Scripts/Reward/RewardPopup.cs` — UI popup
- `Scripts/Reward/RewardSlot.cs` — individual slot UI

**Purpose:** Unified system for granting and displaying rewards from any source.

---

## 2. Reward Types

**Supported reward types:**

| Type | Data | Grant Method |
|------|------|--------------|
| **Currency** | `CurrencyType` + amount | `EconomyService.AddCurrency()` |
| **Item** | `ItemId` + quantity | `InventoryService.AddItem()` |
| **Card** | `CardId` | `CardInventory.AddCard()` |
| **EXP** | amount | `AccountManager.AddExp()` |
| **UltimateStone** | `stoneType` | `InventoryService.AddItem()` |

---

## 3. Reward Data Schema

**Class:**

```csharp
public class RewardData
{
    public RewardType Type;
    public string ItemId;       // For Item/Card/UltimateStone
    public CurrencyType Currency; // For Currency
    public long Amount;         // Quantity
    public string IconPath;     // Display icon
    public string DisplayName;  // UI text
}
```

**Example (Gold reward):**
```csharp
new RewardData
{
    Type = RewardType.Currency,
    Currency = CurrencyType.Gold,
    Amount = 1000,
    IconPath = "UI/Icons/gold",
    DisplayName = "1000 Gold"
}
```

**Example (Item reward):**
```csharp
new RewardData
{
    Type = RewardType.Item,
    ItemId = "potion_hp_small",
    Amount = 5,
    IconPath = "Items/Potion/hp_small",
    DisplayName = "Health Potion x5"
}
```

---

## 4. Reward Sources

**Where rewards come from:**

### 4.1 Wave Completion

**Formula:**
```
GoldReward = BaseGold × WaveMultiplier × TierMultiplier
MeatReward = BaseGold / 30
ExpReward = BaseExp × WaveMultiplier
```

**Granted via:** `WaveManager.CompleteWave()` → `RewardManager.GrantWaveReward()`

---

### 4.2 Enemy Kills

**Per-enemy rewards:**
- Gold (from `dataEnemy.json`)
- Meat (from `dataEnemy.json`)
- EXP (from `dataEnemy.json`)
- Material drops (weighted random, see `EnemyDrop_Design.md`)

**Granted via:** `EnemyDeathHandler` → `EnemyRewardDistributor`

---

### 4.3 Daily Rewards

**7-reward sequence in 1 day** (see `DailyReward_Design.md`):
- Reward 0: Gold (max of constant or highest earned)
- Reward 1: Gem (hardcoded 11)
- Reward 2: Meat (max of constant or highest earned)
- Reward 3: CardRoll ticket
- Reward 4: EXP (max of constant or tier-scaled highest)
- Reward 5: 3× UltimateStone (random variants)
- Reward 6: SkinShard

**Granted via:** `DailyRewardService.ClaimReward()` → `RewardManager.Show()`

---

### 4.4 Idle Rewards

**Offline accumulation** (see `IdleReward_Design.md`):
- Gold (formula: 15 × tierMultiplier × waveMultiplier × minutes × rewardMultiplier)
- Meat (Gold / 30)

**Granted via:** `IdleRewardManager.ClaimReward()` → `EconomyService.AddCurrency()`

---

### 4.5 Mission Rewards

**Mission completion** (see `Mission_Design.md`):
- Currency (gold/gem/meat)
- Items
- EXP

**Granted via:** `MissionService.ClaimMission()` → `RewardManager.Show()` or direct grant

---

### 4.6 Crafting Completion

**After craft finishes:**
- Equipment (newly crafted)
- Materials (refund on failure, if applicable)

**Granted via:** `CraftCompletionService` → `InventoryService.AddItem()`

---

## 5. Reward Granting Flow

**Unified grant method:**

```csharp
public void GrantReward(RewardData reward, string reason)
{
    switch (reward.Type)
    {
        case RewardType.Currency:
            EconomyService.AddCurrency(reward.Currency, reward.Amount, reason);
            break;
        
        case RewardType.Item:
            InventoryService.AddItem(reward.ItemId, (int)reward.Amount);
            break;
        
        case RewardType.Card:
            CardInventory.AddCard(reward.ItemId);
            break;
        
        case RewardType.EXP:
            AccountManager.AddExp(reward.Amount, reason);
            break;
        
        case RewardType.UltimateStone:
            InventoryService.AddItem(reward.ItemId, 1);
            UltimateManager.UnlockUltimate(reward.ItemId);
            break;
    }
    
    // Fire event
    OnRewardGranted?.Invoke(reward);
}
```

---

## 6. Reward Display

**Two display modes:**

### 6.1 Popup (Modal)

**Use for:** Major rewards (wave completion, daily claim, mission claim)

**Flow:**
```csharp
RewardManager.Show(List<RewardData> rewards, Action onClose)
{
    // 1. Pause game (optional)
    // 2. Show popup UI
    // 3. Display all rewards in grid
    // 4. Player clicks "Claim" or "OK"
    // 5. Grant all rewards
    // 6. Close popup
    // 7. Call onClose callback
    // 8. Resume game
}
```

**Visual:** Full-screen overlay with reward icons, quantities, "Claim All" button.

---

### 6.2 Floating Text (Non-blocking)

**Use for:** Small rewards (enemy kills, combat loot)

**Flow:**
```csharp
RewardPopup.SpawnFloating(Vector2 position, RewardData reward)
{
    // 1. Instantiate popup at position
    // 2. Show icon + amount
    // 3. Animate upward + fade out
    // 4. Destroy after 2s
}
```

**Visual:** Small icon + number floating above enemy corpse or player.

---

## 7. Reward Multipliers

**Bonuses that scale rewards:**

### 7.1 Wave/Tier Progression

**Formula:** `BaseReward × WaveMultiplier × TierMultiplier`

**See:** `Wave_Design.md` for multiplier formulas.

---

### 7.2 Card Bonuses

**Card effects:**
- `+50% Gold from enemies`
- `+30% EXP gain`
- `+20% Material drop rate`

**Applied via:** `CardModifierService` → reward calculation reads final stats.

---

### 7.3 Equipment Bonuses

**Affix examples:**
- `of Wealth` — +X% gold
- `of Fortune` — +X% drop rate

**Applied via:** `EquipmentModifierService` → reward modifiers.

---

### 7.4 VIP Bonuses

**VIP flags (see `VIP_Design.md`):**
- `daily`: Daily rewards claimable early
- `maxSpeed`: Faster game speed (indirectly affects rewards/time)
- `autoCollect`: Reserved for future (auto-pickup drops)

**Applied via:** Specific system checks (e.g., `DailyRewardService.IsDailyEnabled()`).

---

## 8. Reward Scaling

**Progression ensures rewards stay relevant:**

### 8.1 Wave/Tier Scaling

**Enemy rewards scale exponentially:**
- Wave 1 enemy: 10 gold
- Wave 100 enemy: ~500 gold (example)
- Wave 350 enemy: ~5000 gold (example)

**Prevents early-game gold from becoming obsolete.**

---

### 8.2 Daily Reward Scaling

**Daily rewards use highest-earned:**
```
DailyGold = Max(DAILY_GOLD_REWARD, HighestGoldEarned)
```

**Example:**
- Player reached Wave 200, earned 50,000 gold
- Daily reward Day 1: **50,000 gold** (not 10,000)

**Scales with player progress.**

---

### 8.3 Idle Reward Scaling

**Idle rewards scale with highest tier/wave:**
```
GoldPerMinute = 15 × tierMultiplier × waveMultiplier
```

**Ensures offline rewards match active play rewards.**

---

## 9. Reward Caps

**Some rewards have caps:**

### 9.1 Idle Reward Duration

**Cap:** 4 hours (14,400 seconds)

**Reason:** Prevent infinite offline farming.

**Effect:** Player offline 24h gets only 4h worth of rewards.

---

### 9.2 Daily Reward Count

**Cap:** 7 rewards per day

**After claiming all 7:** Must wait until daily reset (UTC midnight).

---

### 9.3 Mission Rewards

**No cap** — complete missions = get rewards, but mission slots limited (default 1, expandable).

---

## 10. Reward Events

**Events fired:**

| Event | Trigger | Listeners |
|-------|---------|-----------|
| `OnRewardGranted` | Reward given | Statistics, analytics |
| `OnRewardDisplayed` | Reward popup shown | Audio (fanfare), UI |
| `OnRewardClaimed` | Player claims from popup | UI close, analytics |

---

## 11. Reward Validation

**Before granting:**

```csharp
bool ValidateReward(RewardData reward)
{
    // 1. Amount positive
    if (reward.Amount <= 0)
        return false;
    
    // 2. ItemId valid (if item reward)
    if (reward.Type == RewardType.Item && !ItemDatabase.Exists(reward.ItemId))
        return false;
    
    // 3. Inventory has space (if item reward)
    if (reward.Type == RewardType.Item && !InventoryService.HasSpace(reward.ItemId, (int)reward.Amount))
    {
        ShowError("Inventory full");
        return false;
    }
    
    return true;
}
```

**Invalid rewards are skipped** with warning log.

---

## 12. Performance

**Reward system is lightweight:**
- Granting happens on player action (not per-frame)
- Popup display is simple UI
- Floating text uses pooling

**No optimization needed** unless thousands of rewards per second.

---

## 13. Testing Checklist

```
[ ] Wave completion grants correct gold/meat/exp
[ ] Enemy kill grants rewards immediately
[ ] Daily reward grants all 7 types correctly
[ ] Idle reward calculates correctly (gold, meat)
[ ] Mission reward grants on claim
[ ] Reward popup displays all rewards
[ ] Reward multipliers apply (cards, equipment, VIP)
[ ] Reward scaling matches wave/tier progression
[ ] Idle reward capped at 4h
[ ] Daily reward resets at UTC midnight
[ ] Inventory full prevents item rewards
[ ] Events fire correctly
[ ] Save/load preserves reward state
```

---

## 14. Common Issues

### Issue: Rewards not granted
**Cause:** Validation failed, or grant method not called.
**Fix:** Check validation logic, verify grant call.

### Issue: Reward amounts wrong
**Cause:** Multipliers not applied, or formula incorrect.
**Fix:** Verify multiplier calculation, check scaling formulas.

### Issue: Popup doesn't show
**Cause:** Popup UI not instantiated, or rewards list empty.
**Fix:** Verify popup prefab exists, check rewards list not null.

### Issue: Floating text spam
**Cause:** Too many rewards spawned simultaneously.
**Fix:** Batch small rewards, use pooling.

---

## 15. Future Extensions

### Reward Chests
- Random rewards from chest opening

### Reward Milestones
- Bonus rewards at specific wave numbers

### Reward Boosters
- Temporary 2× reward events

### Reward Challenges
- Special missions with bonus rewards

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
