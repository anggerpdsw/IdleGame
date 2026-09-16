# SaveManager System Design — IdleDefenseSurvival

**Purpose:** Central save/load orchestration, version management, migration, corruption handling.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [ScenePersistence_Design.md](./ScenePersistence_Design.md) — persistent services
- [Economy_Design.md](./Economy_Design.md) — currency persistence
- [Card_Design.md](./Card_Design.md) — card inventory
- [Equipment_Design.md](./Equipment_Design.md) — equipment persistence
- [Crafting_Design.md](./Crafting_Design.md) — craft queue
- [Mission_Design.md](./Mission_Design.md) — mission state
- All system docs — persistence integration

---

## 1. SaveManager Identity

**Owner:** `Scripts/Manager/SaveManager.cs` (singleton)

**Purpose:** Central authority for all save/load operations.

**Save location:** `Application.persistentDataPath/SaveData.json`

**Serialization:** Newtonsoft.Json

---

## 2. Save Version

**Current version (verified `GameConstants.cs`):**

```csharp
public const int CURRENT_SAVE_VERSION = 4;
```

**Version history:**
- v1: Initial save structure
- v2: Added equipment system
- v3: Added crafting queue
- v4: Added pets array

---

## 3. SaveData Schema

**Root schema:**

```csharp
public class SaveData
{
    public int SaveVersion = GameConstants.CURRENT_SAVE_VERSION;
    
    // Account progression
    public AccountData Account = new();
    
    // VIP flags
    public VipData Vip = new();
    
    // Game state
    public GameStateData GameState = new();
    
    // Wave progress
    public int CurrentTier = 1;
    public int CurrentWave = 1;
    public int HighestTierReached = 1;
    public Dictionary<int, int> HighestWavePerTier = new();
    
    // Economy
    public long Gold = 0;
    public long Gem = 0;
    public long Meat = 0;
    public long Exp = 0;
    
    // Inventory
    public List<InventoryItem> Inventory = new();
    
    // Equipment
    public Dictionary<int, string> EquippedItems = new();  // slotIndex → instanceId
    public List<EquipmentInstance> OwnedEquipment = new();
    
    // Cards
    public CardInventoryData CardInventory = new();
    
    // Crafting
    public List<CraftJob> CraftingJobs = new();
    
    // Missions
    public List<MissionInstance> Missions = new();
    
    // Daily reward
    public DailyRewardSaveData DailyReward = new();
    
    // Idle reward
    public IdleRewardData IdleReward = new();
    
    // Pets
    public List<PetInstance> Pets = new();
    
    // Statistics
    public StatisticsData Statistics = new();
}
```

---

## 4. Save Operations

### 4.1 SaveAll

**Full save:**

```csharp
public void SaveAll()
{
    try
    {
        // 1. Build SaveData from all systems
        SaveData data = BuildSaveData();
        
        // 2. Serialize to JSON
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        
        // 3. Write to file
        string path = GetSavePath();
        File.WriteAllText(path, json);
        
        // 4. Fire event
        OnSaveCompleted?.Invoke();
        
        Debug.Log("Save completed");
    }
    catch (Exception ex)
    {
        Debug.LogError($"Save failed: {ex.Message}");
        OnSaveFailed?.Invoke(ex);
    }
}
```

---

### 4.2 LoadAll

**Full load:**

```csharp
public bool LoadAll()
{
    try
    {
        // 1. Check file exists
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            Debug.Log("No save file, creating new");
            InitializeNewSave();
            return false;
        }
        
        // 2. Read file
        string json = File.ReadAllText(path);
        
        // 3. Deserialize
        SaveData data = JsonConvert.DeserializeObject<SaveData>(json);
        
        // 4. Validate version
        if (data.SaveVersion < CURRENT_SAVE_VERSION)
        {
            Debug.Log($"Migrating save from v{data.SaveVersion} to v{CURRENT_SAVE_VERSION}");
            MigrateSave(data);
        }
        
        // 5. Apply to systems
        ApplySaveData(data);
        
        // 6. Fire event
        OnLoadCompleted?.Invoke();
        
        Debug.Log("Load completed");
        return true;
    }
    catch (Exception ex)
    {
        Debug.LogError($"Load failed: {ex.Message}");
        OnLoadFailed?.Invoke(ex);
        
        // Create new save on corruption
        InitializeNewSave();
        return false;
    }
}
```

---

## 5. Save Version Migration

**Migration flow:**

```csharp
void MigrateSave(SaveData data)
{
    int fromVersion = data.SaveVersion;
    int toVersion = CURRENT_SAVE_VERSION;
    
    // Apply migrations sequentially
    for (int v = fromVersion + 1; v <= toVersion; v++)
    {
        ApplyMigration(data, v);
    }
    
    // Update version
    data.SaveVersion = toVersion;
}

void ApplyMigration(SaveData data, int targetVersion)
{
    switch (targetVersion)
    {
        case 2:
            MigrateToV2(data);
            break;
        case 3:
            MigrateToV3(data);
            break;
        case 4:
            MigrateToV4(data);
            break;
    }
}
```

**Example migration (v3 → v4):**

```csharp
void MigrateToV4(SaveData data)
{
    // v4 added pets array
    if (data.Pets == null)
    {
        data.Pets = new List<PetInstance>();
        Debug.Log("Migrated to v4: initialized pets array");
    }
}
```

---

## 6. New Save Initialization

**Fresh save:**

```csharp
void InitializeNewSave()
{
    _currentSave = new SaveData
    {
        SaveVersion = CURRENT_SAVE_VERSION,
        
        Account = new AccountData
        {
            Level = 1,
            Exp = 0,
            UnspentAttributePoints = 0,
            Attributes = new AttributeData
            {
                Constitution = 5,
                Strength = 5,
                Intelligence = 5,
                Dexterity = 5
            }
        },
        
        CurrentTier = 1,
        CurrentWave = 1,
        
        Gold = 1000,    // Starting gold
        Gem = 0,
        Meat = 100,     // Starting meat
        
        Inventory = new List<InventoryItem>(),
        OwnedEquipment = new List<EquipmentInstance>(),
        
        CardInventory = new CardInventoryData
        {
            OwnedCards = new Dictionary<string, CardInstance>(),
            EquippedCards = new string[GameConstants.CARD_START_SLOT]
        },
        
        Pets = new List<PetInstance>(),
        
        DailyReward = new DailyRewardSaveData
        {
            CurrentRewardIndex = 0,
            NextUnlockUtcTicks = DateTime.UtcNow.Ticks,
            LastResetDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
        },
        
        IdleReward = new IdleRewardData
        {
            LastClaimUtcTicks = DateTime.UtcNow.Ticks
        }
    };
    
    SaveAll();
}
```

---

## 7. Build SaveData

**Aggregate from all systems:**

```csharp
SaveData BuildSaveData()
{
    SaveData data = new()
    {
        SaveVersion = CURRENT_SAVE_VERSION
    };
    
    // Account
    data.Account = GetAccountData();
    
    // VIP
    data.Vip = GetVipData();
    
    // Currency
    data.Gold = EconomyManager.Instance.GetCurrency(CurrencyType.Gold);
    data.Gem = EconomyManager.Instance.GetCurrency(CurrencyType.Gem);
    data.Meat = EconomyManager.Instance.GetCurrency(CurrencyType.Meat);
    data.Exp = EconomyManager.Instance.GetCurrency(CurrencyType.Exp);
    
    // Wave
    data.CurrentTier = WaveManager.Instance.CurrentTier;
    data.CurrentWave = WaveManager.Instance.CurrentWave;
    data.HighestTierReached = WaveManager.Instance.HighestTierReached;
    data.HighestWavePerTier = WaveManager.Instance.HighestWavePerTier;
    
    // Inventory
    data.Inventory = InventoryService.Instance.GetAllItems();
    
    // Equipment
    data.EquippedItems = EquipmentService.Instance.GetEquippedSlots();
    data.OwnedEquipment = EquipmentService.Instance.GetAllEquipment();
    
    // Cards
    data.CardInventory = CardInventory.Instance.GetSaveData();
    
    // Crafting
    data.CraftingJobs = CraftQueueService.Instance.GetActiveJobs();
    
    // Missions
    data.Missions = MissionService.Instance.GetAllMissions();
    
    // Daily
    data.DailyReward = GetDailyRewardData();
    
    // Idle
    data.IdleReward = GetIdleRewardData();
    
    // Pets
    data.Pets = PetService.Instance.GetAllPets();
    
    return data;
}
```

---

## 8. Apply SaveData

**Distribute to all systems:**

```csharp
void ApplySaveData(SaveData data)
{
    // Account
    SetAccountData(data.Account);
    
    // VIP
    SetVipData(data.Vip);
    
    // Currency
    EconomyManager.Instance.SetCurrency(CurrencyType.Gold, data.Gold);
    EconomyManager.Instance.SetCurrency(CurrencyType.Gem, data.Gem);
    EconomyManager.Instance.SetCurrency(CurrencyType.Meat, data.Meat);
    EconomyManager.Instance.SetCurrency(CurrencyType.Exp, data.Exp);
    
    // Wave
    WaveManager.Instance.LoadState(data.CurrentTier, data.CurrentWave, data.HighestWavePerTier);
    
    // Inventory
    InventoryService.Instance.LoadItems(data.Inventory);
    
    // Equipment
    EquipmentService.Instance.LoadEquipment(data.EquippedItems, data.OwnedEquipment);
    
    // Cards
    CardInventory.Instance.LoadData(data.CardInventory);
    
    // Crafting
    CraftQueueService.Instance.LoadJobs(data.CraftingJobs);
    
    // Missions
    MissionService.Instance.LoadMissions(data.Missions);
    
    // Daily
    SetDailyRewardData(data.DailyReward);
    
    // Idle
    SetIdleRewardData(data.IdleReward);
    
    // Pets
    PetService.Instance.LoadPets(data.Pets);
    
    // Invalidate caches
    PlayerStatsManager.InvalidateCache();
}
```

---

## 9. Save Triggers

**Auto-save triggers:**
- Currency change
- Inventory change
- Equipment equip/unequip
- Card equip/unequip
- Attribute allocation
- Wave completion
- Mission claim
- Craft start/complete
- Scene transition

**Manual save:** Player can trigger via menu.

---

## 10. Backup System

**Backup on save:**

```csharp
void BackupSave()
{
    string savePath = GetSavePath();
    string backupPath = GetBackupPath();
    
    if (File.Exists(savePath))
    {
        File.Copy(savePath, backupPath, overwrite: true);
    }
}
```

**Restore from backup:**

```csharp
void RestoreBackup()
{
    string backupPath = GetBackupPath();
    string savePath = GetSavePath();
    
    if (File.Exists(backupPath))
    {
        File.Copy(backupPath, savePath, overwrite: true);
        Debug.Log("Restored from backup");
    }
}
```

---

## 11. Error Handling

**Corruption detection:**

```csharp
bool ValidateSaveData(SaveData data)
{
    // 1. Version in valid range
    if (data.SaveVersion < 1 || data.SaveVersion > CURRENT_SAVE_VERSION)
        return false;
    
    // 2. Critical fields not null
    if (data.Account == null)
        return false;
    
    if (data.Inventory == null)
        return false;
    
    // 3. Currency not negative
    if (data.Gold < 0 || data.Gem < 0)
        return false;
    
    return true;
}
```

**Recovery on corruption:**
1. Try load backup
2. If backup also corrupt, create new save
3. Log error for debugging

---

## 12. Testing Checklist

```
[ ] SaveAll writes file correctly
[ ] LoadAll reads file correctly
[ ] Migration v1→v4 works
[ ] New save initializes correctly
[ ] All systems save state
[ ] All systems load state
[ ] Save/load round-trip preserves data
[ ] Backup created on save
[ ] Restore from backup works
[ ] Corruption detected
[ ] Recovery creates new save
```

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
