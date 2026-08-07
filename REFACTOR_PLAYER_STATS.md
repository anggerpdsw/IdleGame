# Player Stats Refactor - Documentation

**Date:** 2026-07-10  
**Status:** ✅ Complete

## 🎯 Objective

Refactor Player stats system dari 15 individual fields menjadi scalable Dictionary-based architecture untuk memudahkan penambahan stat baru di masa depan.

---

## 📁 Files Changed

### New Files (3):

1. **[Assets/Scripts/Player/PlayerStatType.cs](Assets/Scripts/Player/PlayerStatType.cs)**
   - Enum containing all 15 stat types
   - Type-safe identifier untuk setiap stat
   - Menghindari string literals

2. **[Assets/Scripts/Player/PlayerStats.cs](Assets/Scripts/Player/PlayerStats.cs)**
   - Dictionary-based storage: `Dictionary<PlayerStatType, float>`
   - Public API: `GetFloat()`, `GetInt()`, `SetStat()`, `AddStat()`, `HasStat()`
   - Integer stats (BounceCount, MultiShootCount) disimpan sebagai float, dikonversi saat dibaca

3. **[Assets/Scripts/Player/StatLoader.cs](Assets/Scripts/Player/StatLoader.cs)**
   - Helper untuk mapping JSON → PlayerStats
   - 2 methods:
     - `LoadFromPlayerData()` - Load base stats (no upgrades)
     - `LoadWithUpgrades()` - Load stats dengan current upgrade levels dari UpgradeManager

### After (New System):

```csharp
// Player.cs - Single PlayerStats instance
private PlayerStats _stats = new PlayerStats();

public float AttackRange => _stats.GetFloat(PlayerStatType.AttackRange);
public float AttackSpeed => _stats.GetFloat(PlayerStatType.AttackSpeed);
public float AttackDamage => _stats.GetFloat(PlayerStatType.AttackDamage);
public int BounceCount => _stats.GetInt(PlayerStatType.BounceCount);
// ... 11 more properties

private void LoadPlayerData()
{
    TextAsset playerJson = Resources.Load<TextAsset>("Data/dataPlayer");
    if (playerJson == null) return;
    
    PlayerData pData = JsonConvert.DeserializeObject<PlayerData>(playerJson.text);
    StatLoader.LoadFromPlayerData(_stats, pData);  // ← Single line!
}
```

## 🚀 Adding New Stats (Step-by-Step Guide)

### Example: Adding `CriticalChance` and `CriticalDamage`

#### Step 1: Add to Enum
**File:** [Assets/Scripts/Player/PlayerStatType.cs](Assets/Scripts/Player/PlayerStatType.cs)

```csharp
public enum PlayerStatType
{
    AttackRange,
    AttackSpeed,
    AttackDamage,
    // ... existing stats
    StuntDuration,
    
    // NEW: Add here
    CriticalChance,
    CriticalDamage
}
```

#### Step 2: Add to JSON Data Structure
**File:** [Assets/Scripts/Data/PlayerData.cs](Assets/Scripts/Data/PlayerData.cs)

```csharp
[Serializable]
public class PlayerSkills
{
    public SkillData attackRange;
    // ... existing skills
    public SkillData stuntDuration;
    
    // NEW: Add here
    public SkillData criticalChance;
    public SkillData criticalDamage;
}
```

#### Step 3: Add to JSON File
**File:** `Assets/Resources/Data/dataPlayer.json`

```json
{
  "skills": {
    "attackRange": { "level": 1, "maxLevel": 100, "min": 2, "max": 10, "locked": false, "description": "Maximum range of player" },
    ...
    "criticalChance": { "level": 0, "maxLevel": 100, "min": 0, "max": 50, "locked": true, "description": "Chance of each projectile to deal critical damage" },
    "criticalDamage": { "level": 0, "maxLevel": 100, "min": 100, "max": 300, "locked": true, "description": "Damage multiplier for critical hits" }
  }
}
```

#### Step 4: Add Mapping to StatLoader
**File:** [Assets/Scripts/Player/StatLoader.cs](Assets/Scripts/Player/StatLoader.cs)

In `LoadFromPlayerData()`:
```csharp
stats.SetStat(PlayerStatType.StuntDuration, PlayerStatsCalculator.CalculateSkillFloatValue(s.stuntDuration));

// NEW: Add here
stats.SetStat(PlayerStatType.CriticalChance, PlayerStatsCalculator.CalculateSkillFloatValue(s.criticalChance));
stats.SetStat(PlayerStatType.CriticalDamage, PlayerStatsCalculator.CalculateSkillFloatValue(s.criticalDamage));
```

In `LoadWithUpgrades()`:
```csharp
stats.SetStat(PlayerStatType.StuntDuration, CalcFloat("stuntDuration", s.stuntDuration));

// NEW: Add here
stats.SetStat(PlayerStatType.CriticalChance, CalcFloat("criticalChance", s.criticalChance));
stats.SetStat(PlayerStatType.CriticalDamage, CalcFloat("criticalDamage", s.criticalDamage));
```

#### Step 5: Use in Gameplay Code
```csharp
// Anywhere in your code
if (Utilityku.Chance(_player.Stats.GetFloat(PlayerStatType.CriticalChance)))
{
    float critDamage = baseDamage * _player.Stats.GetFloat(PlayerStatType.CriticalDamage);
    enemy.TakeDamage(critDamage);
}
```

**That's it!** No more manual assignments, no more repetitive code.

---

## 📝 Future Enhancements

Potential improvements (not urgent):

1. **Stat Modifiers System**
   ```csharp
   // Add temporary buffs/debuffs
   _stats.AddModifier(PlayerStatType.AttackSpeed, 1.5f, duration: 10f);
   ```

2. **Stat Events**
   ```csharp
   // Listen for stat changes
   _stats.OnStatChanged += (statType, oldValue, newValue) => { ... };
   ```

3. **Stat Clamping**
   ```csharp
   // Enforce min/max constraints
   _stats.SetStat(PlayerStatType.AttackSpeed, value, min: 0.5f, max: 10f);
   ```

4. **Stat Serialization**
   ```csharp
   // Save/load player progress
   string json = _stats.ToJson();
   _stats.FromJson(json);
   ```

---

## 👥 Contributors

- **Refactor by:** Claude Code (AI Assistant)
- **Requested by:** Howby
- **Date:** 2026-07-10

---

## 📚 Related Files

- [CLAUDE.md](CLAUDE.md) - Project conventions
- [Assets/Scripts/Player/Player.cs](Assets/Scripts/Player/Player.cs) - Main player class
- [Assets/Scripts/Data/PlayerData.cs](Assets/Scripts/Data/PlayerData.cs) - JSON data structure
- [Assets/Scripts/Upgrade/UpgradeManager.cs](Assets/Scripts/Upgrade/UpgradeManager.cs) - Upgrade system
