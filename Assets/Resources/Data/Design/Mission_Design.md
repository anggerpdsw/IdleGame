# Mission System Design — IdleDefenseSurvival

**Purpose:** Dynamic mission templates, lifecycle (Active→Completed→Claimed), event-driven progress, cooldown-based regeneration.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [Reward_Design.md](./Reward_Design.md) — mission rewards
- [Economy_Design.md](./Economy_Design.md) — currency rewards
- [Wave_Design.md](./Wave_Design.md) — WaveCompleted event
- [Enemy_Design.md](./Enemy_Design.md) — EnemyKilled events
- [Crafting_Design.md](./Crafting_Design.md) — Blacksmithing event

---

## 1. Mission Identity

**Owner:** `Scripts/Mission/MissionService.cs` (singleton, DontDestroyOnLoad)

**Templates:** `Assets/Resources/Data/Player/dataMission.json`

**Persistence:** `SaveData.missions` list (NOT separate domain save)

**UI:** `Scripts/Mission/MissionUI.cs`, `MissionSlot.cs`

---

## 2. Mission Event Types

**6 event types (verified `MissionEventType` enum):**

| Type | Trigger | `targetId` Semantics | Producer Call Site |
|------|---------|---------------------|-------------------|
| **EnemyKilled** | Any non-boss kill | Not matched (unconditional) | `EnemyDeathHandler` |
| **BossKilled** | Any boss kill | Not matched (unconditional) | `EnemyDeathHandler` |
| **SpecificEnemyKilled** | Kill chosen enemy | Runtime-picked from `DatabaseEnemy` (role != BOSS) | `EnemyDeathHandler` |
| **CurrencyEarned** | Economy reward added | `template.targetId` ("Gold"/"Gem"/"Meat") | `EconomyManager.AddCurrency` |
| **WaveCompleted** | Wave clear | Not matched (unconditional) | `WaveManager.CompleteWave` |
| **Blacksmithing** | Equipment crafted | Runtime-picked from `EquipmentType` enum (excluding None) | `CraftCompletionService` |

**Routing:** Producers call `MissionService.Instance?.UpdateProgress(MissionEventType type, string targetId, long amount)`.

---

## 3. Mission Lifecycle

**3 states:**

```
Active → Completed → Claimed
Active → Cancelled
```

**Transitions:**

| From | To | Trigger | Cooldown |
|------|----|---------|---------| 
| Active | Completed | `currentCount >= targetCount` | 30 min (template-overridable) |
| Active | Cancelled | Player clicks cancel | 15 min (template-overridable) |
| Completed | Claimed | Player claims reward | None (instant) |
| Claimed | Active | Cooldown expires | `CheckCooldowns()` regenerates |
| Cancelled | Active | Cooldown expires | `CheckCooldowns()` regenerates |

**Cooldown storage:** `DateTimeOffset` strings serialized via `"o"` format.

---

## 4. Slot Cap

**Max missions:** `SaveData.account.maxMission` (default `1`, minimum `1`)

**Expansion:** `MissionService.SetMaxMission(int)` — clamps ≥ 1, writes through `SaveManager.GetAccountData()`, calls `GenerateMissingMissions()` + `SaveMissions()`.

**UI pool:** `MissionUI.EnsurePool` grows up to `GetMaxMission()`, never shrinks — slot layout stays stable across cap changes.

---

## 5. Mission Template Schema

**File:** `Assets/Resources/Data/Player/dataMission.json`

**Schema example:**

```json
{
  "id": "mission_kill_goblins",
  "eventType": "SpecificEnemyKilled",
  "targetId": "",
  "targetCount": 50,
  "goldReward": 500,
  "gemReward": 0,
  "meatReward": 10,
  "expReward": 100,
  "cooldownMinutes": 30,
  "cancelCooldownMinutes": 15,
  "weight": 1.0
}
```

**Dynamic `targetId`:** For `SpecificEnemyKilled` and `Blacksmithing`, `targetId` is runtime-selected and stored on `MissionInstance.targetId`. Templates leave it blank.

---

## 6. Progress Tracking

**Update flow:**

```csharp
public void UpdateProgress(MissionEventType type, string targetId, long amount)
{
    foreach (var mission in _activeMissions)
    {
        if (mission.Status != MissionStatus.Active)
            continue;
        
        // Match event type
        if (!DoesEventMatchMission(type, targetId, mission))
            continue;
        
        // Increment progress
        mission.CurrentCount += amount;
        
        // Clamp at target
        if (mission.CurrentCount > mission.TargetCount)
            mission.CurrentCount = mission.TargetCount;
        
        // Check completion
        if (mission.CurrentCount >= mission.TargetCount)
        {
            mission.Status = MissionStatus.Completed;
            mission.CompletedAt = DateTimeOffset.UtcNow.ToString("o");
            OnMissionStatusChanged?.Invoke(mission.InstanceId);
        }
        
        OnMissionProgressChanged?.Invoke(mission.InstanceId, mission.CurrentCount);
    }
    
    SaveMissions();
}
```

**Match logic:**

```csharp
bool DoesEventMatchMission(MissionEventType type, string targetId, MissionInstance mission)
{
    // Type must match
    if (mission.Template.EventType != type)
        return false;
    
    // Unconditional types
    if (type == EnemyKilled || type == BossKilled || type == WaveCompleted)
        return true;
    
    // Target-keyed types
    return IsTargetMatch(targetId, mission.TargetId);
}
```

---

## 7. Reward Flow

**Claim flow:**

```csharp
public void ClaimMission(string instanceId)
{
    MissionInstance mission = GetMission(instanceId);
    
    if (mission.Status != MissionStatus.Completed)
        return;
    
    // Build reward list
    List<RewardData> rewards = new();
    
    if (mission.GoldReward > 0)
        rewards.Add(new RewardData { Type = Currency, Currency = Gold, Amount = mission.GoldReward });
    if (mission.GemReward > 0)
        rewards.Add(new RewardData { Type = Currency, Currency = Gem, Amount = mission.GemReward });
    if (mission.MeatReward > 0)
        rewards.Add(new RewardData { Type = Currency, Currency = Meat, Amount = mission.MeatReward });
    if (mission.ExpReward > 0)
        rewards.Add(new RewardData { Type = EXP, Amount = mission.ExpReward });
    
    // Grant via RewardManager (popup path) or inline
    if (rewards.Count > 0)
    {
        RewardManager.Instance.Show(rewards, () => NotifyClaimed(instanceId));
    }
    else
    {
        // No rewards (or all zero), inline grant
        foreach (var reward in rewards)
            ServiceLocator.EconomyService.AddCurrency(reward.Currency, reward.Amount, "MissionReward");
        
        NotifyClaimed(instanceId);
    }
}

void NotifyClaimed(string instanceId)
{
    MissionInstance mission = GetMission(instanceId);
    mission.Status = MissionStatus.Claimed;
    mission.CooldownUntil = DateTimeOffset.UtcNow.AddMinutes(mission.Template.CooldownMinutes).ToString("o");
    
    SaveMissions();
    
    OnMissionStatusChanged?.Invoke(instanceId);
    OnMissionsChanged?.Invoke();
}
```

**Do NOT grant rewards directly from UI or `MissionSlot`** — only via `MissionService.ClaimMission`.

---

## 8. Mission Generation

**Generate new mission:**

```csharp
void GenerateMission(int slotIndex)
{
    // 1. Select random template (weighted)
    MissionTemplate template = SelectRandomTemplate();
    
    // 2. Create instance
    MissionInstance instance = new()
    {
        InstanceId = Guid.NewGuid().ToString(),
        TemplateId = template.Id,
        Template = template,
        SlotIndex = slotIndex,
        Status = MissionStatus.Active,
        CurrentCount = 0,
        TargetCount = template.TargetCount
    };
    
    // 3. Assign dynamic target (if needed)
    if (template.EventType == SpecificEnemyKilled)
    {
        instance.TargetId = SelectRandomEnemy();
    }
    else if (template.EventType == Blacksmithing)
    {
        instance.TargetId = SelectRandomEquipmentType();
    }
    else
    {
        instance.TargetId = template.TargetId;
    }
    
    // 4. Add to active list
    _activeMissions.Add(instance);
    
    SaveMissions();
    OnMissionsChanged?.Invoke();
}
```

---

## 9. Cooldown Regeneration

**Check cooldowns every frame (`MissionService.Update`):**

```csharp
void Update()
{
    CheckCooldowns();
}

void CheckCooldowns()
{
    DateTimeOffset now = DateTimeOffset.UtcNow;
    
    bool changed = false;
    
    for (int i = 0; i < GetMaxMission(); i++)
    {
        MissionInstance mission = GetMissionInSlot(i);
        
        if (mission == null)
        {
            GenerateMission(i);
            changed = true;
            continue;
        }
        
        if (mission.Status == Claimed || mission.Status == Cancelled)
        {
            if (!string.IsNullOrEmpty(mission.CooldownUntil))
            {
                DateTimeOffset cooldownEnd = DateTimeOffset.Parse(mission.CooldownUntil);
                
                if (now >= cooldownEnd)
                {
                    // Regenerate new mission
                    _activeMissions.Remove(mission);
                    GenerateMission(i);
                    changed = true;
                }
            }
        }
    }
    
    if (changed)
    {
        SaveMissions();
        OnMissionsChanged?.Invoke();
    }
}
```

**Keep check cheap** — only runs when panel open or background service ticks.

---

## 10. UI Integration

**`MissionUI.cs`:**

- Pool grows up to `GetMaxMission()`, never shrinks
- Subscribes to `OnMissionsChanged`, `OnMissionStatusChanged`, `OnMissionProgressChanged`
- Icon resolved per event type via `EnemyResources.GetEnemySprite` or `ItemResources.GetItemSource`

**`MissionSlot.cs`:**

- Single row, refreshed every 1s while panel open (countdown timer)
- Background color: `Completed = green`, `Claimed = gray`, `Cancelled = red`
- Button color via `ButtonResources.GetColor("Green"|"Red"|"Grey")`
- Enter animation uses DOTween (`SetLink(gameObject)`)

---

## 11. Persistence

**Saved per mission:**

```csharp
public class MissionInstance
{
    public string InstanceId;         // GUID
    public string TemplateId;         // Template reference
    public int SlotIndex;             // 0-based slot position
    public MissionStatus Status;      // Active/Completed/Claimed/Cancelled
    public string TargetId;           // Runtime-assigned target
    public long CurrentCount;         // Progress
    public long TargetCount;          // Goal
    public string CompletedAt;        // DateTimeOffset "o" format
    public string CooldownUntil;      // DateTimeOffset "o" format
}
```

**Save trigger:** Every `UpdateProgress`, `ClaimMission`, `GenerateMission`, `CheckCooldowns` calls `SaveMissions()`.

---

## 12. Adding New Event Type

**Workflow:**

1. Add value to `MissionEventType` enum
2. Update `MissionService.DoesEventMatchMission` switch — return `true` for unconditional types, `IsTargetMatch` for target-keyed types
3. Add at least one template row to `dataMission.json` exercising the type
4. Find producer(s) and call `MissionService.Instance?.UpdateProgress(newType, targetId, amount)` at right hook point
5. If icon is target-keyed, extend `MissionUI.GetMissionIcon` switch
6. Verify: fresh save shows template, save→load preserves instanceId/slotIndex/cooldownUntil, claim routes through `RewardManager`, cancel regenerates after cooldown

---

## 13. Testing Checklist

```
[ ] All 6 event types trigger correctly
[ ] SpecificEnemyKilled matches chosen enemy
[ ] Blacksmithing matches chosen equipment type
[ ] Progress increments on event
[ ] Completion triggers at targetCount
[ ] Claim grants all rewards (gold/gem/meat/exp)
[ ] Cooldown prevents immediate regeneration
[ ] Cancelled mission regenerates after cooldown
[ ] Slot cap expansion works
[ ] Save/load preserves mission state
[ ] Icon displays correctly per event type
[ ] Countdown timer updates every 1s
[ ] UI pool grows/shrinks correctly
```

---

## 14. Common Issues

### Issue: Progress never updates
**Cause:** Event type mismatch, or producer not calling `UpdateProgress`.
**Fix:** Verify event type match, check producer call site.

### Issue: Mission never regenerates
**Cause:** Cooldown not expiring, or `CheckCooldowns` not called.
**Fix:** Verify cooldown string format ("o"), check Update loop.

### Issue: Claim grants no reward
**Cause:** Reward values all zero, or grant logic skipped.
**Fix:** Verify template reward values, check claim flow.

### Issue: UI shows wrong icon
**Cause:** Icon resolution logic broken, or resource path wrong.
**Fix:** Verify icon path per event type, check resource load.

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
