# Scene Persistence Design — IdleDefenseSurvival

**Purpose:** Scene architecture, persistent managers, DontDestroyOnLoad patterns, scene transitions.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [SaveManager_Design.md](./SaveManager_Design.md) — save/load across scenes
- [UI_Design.md](./UI_Design.md) — UI persistence rules

---

## 1. Scene Architecture

**Important scenes:**
- `Bootstrap.unity` — initialization, service registration
- `MainMenu.unity` — main menu, VIP panel, settings
- `Game.unity` — gameplay arena
- `CardCollection.unity` — card management
- `Inventory.unity` — inventory UI
- `Crafting.unity` — crafting station

---

## 2. Persistent Services

**Services must NOT duplicate when changing scenes.**

**Pattern:** Singleton with DontDestroyOnLoad

```csharp
public class ServiceName : MonoBehaviour
{
    public static ServiceName Instance { get; private set; }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

**Persistent services:**
- `SaveManager`
- `EconomyManager`
- `CardManager` (some paths)
- `MissionService`
- `AudioManager`
- `AnalyticsManager`
- `IdleRewardManager`

---

## 3. UI Persistence Rule

**A UI component in multiple scenes must decide:**

**Option 1: Scene-local** (default)
- Each scene has own instance
- No DontDestroyOnLoad
- Destroyed on scene unload

**Option 2: Single persistent service**
- One instance survives all scenes
- DontDestroyOnLoad
- Singleton pattern enforced

**Anti-pattern:** Blindly marking every UI `DontDestroyOnLoad` creates duplicates.

**Example (TooltipUI):**
- If scene-local: each scene has own tooltip, destroyed on exit
- If persistent: one tooltip survives, shared across scenes

**Decision must be explicit per UI component.**

---

## 4. Scene Transition

**Load flow:**

```csharp
public void LoadScene(string sceneName)
{
    // 1. Save current state
    SaveManager.SaveAll();
    
    // 2. Unload additive scenes (if any)
    // ...
    
    // 3. Load new scene
    SceneManager.LoadScene(sceneName);
    
    // 4. Persistent services survive
    // 5. Scene-local objects destroyed
}
```

**Do NOT assume objects survive transition** unless explicitly persistent.

---

## 5. Bootstrap Pattern

**`Bootstrap.unity` initialization:**

```csharp
public class BootstrapInitializer : MonoBehaviour
{
    void Start()
    {
        // 1. Initialize core services
        InitializeSaveSystem();
        InitializeEconomy();
        InitializeAudio();
        InitializeAnalytics();
        
        // 2. Load save data
        SaveManager.LoadAll();
        
        // 3. Validate VIP expiry (if time-limited)
        ValidateVIPExpiry();
        
        // 4. Check idle rewards
        CheckIdleRewards();
        
        // 5. Load main menu
        SceneManager.LoadScene("MainMenu");
    }
}
```

**Bootstrap runs once** at app start.

---

## 6. ServiceLocator

**Existing service concepts:**
- Save service
- Economy service
- Audio service
- Ads service
- Analytics service
- Game manager access

**Register pattern:**

```csharp
ServiceLocator.Register<IEconomyService>(EconomyManager.Instance);
ServiceLocator.Register<IAudioService>(AudioManager.Instance);
```

**Access pattern:**

```csharp
ServiceLocator.Get<IEconomyService>().AddCurrency(Gold, 100, "Test");
```

**Do NOT register duplicate services** under different paths.

---

## 7. Scene-Local State

**Some state is scene-specific:**
- Current wave (Game scene only)
- Active enemies (Game scene only)
- Open UI panels (per scene)
- Camera position (per scene)

**Do NOT persist scene-local state globally** unless intentional.

---

## 8. Save on Scene Exit

**Before leaving Game scene:**

```csharp
void OnDestroy()
{
    if (this == Instance)
    {
        // Save current progress
        SaveManager.SaveAll();
        
        // Clean up scene-local state
        CleanupEnemies();
        CleanupProjectiles();
    }
}
```

**Ensures progress not lost** on unexpected exit.

---

## 9. Idle Reward on Return

**When returning from offline:**

```csharp
void OnApplicationFocus(bool hasFocus)
{
    if (hasFocus)
    {
        // Check idle rewards
        if (IdleRewardManager.Instance.CanClaim())
        {
            IdleRewardUI.Show();
        }
    }
}
```

**Triggered:** App regains focus after background.

---

## 10. Scene Cleanup

**Cleanup pattern:**

```csharp
public class SceneCleanupHandler : MonoBehaviour
{
    void OnDestroy()
    {
        // Unsubscribe all events
        UnsubscribeEvents();
        
        // Clear pooled objects
        ClearPools();
        
        // Release resources
        ReleaseResources();
    }
}
```

**Prevents memory leaks** from dangling references.

---

## 11. Testing Checklist

```
[ ] Persistent services survive scene transitions
[ ] No duplicate managers after scene change
[ ] Scene-local objects destroyed on exit
[ ] Save/load works across scenes
[ ] Idle rewards check on app focus
[ ] Scene cleanup prevents memory leaks
[ ] Bootstrap initializes all services
[ ] ServiceLocator accessible from all scenes
```

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
