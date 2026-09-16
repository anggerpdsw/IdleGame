# UI System Design — IdleDefenseSurvival

**Purpose:** UI architecture principles, tooltip system, panel management, event-driven updates.

**Last Updated:** 2026-09-16

---

## Related Design Documents

- [ScenePersistence_Design.md](./ScenePersistence_Design.md) — UI persistence rules
- All system docs — UI consumes domain state

---

## 1. UI Identity

**Core principle:** UI is presentation layer, NOT business logic owner.

**UI should:**
- Display state
- Send user intent to services
- Subscribe to domain events
- Request operations from services

**UI should NOT own:**
- Save logic
- Item definitions
- Currency mutation
- Equipment state
- Card progression
- Combat formulas
- Stat calculations

---

## 2. UI Architecture Layers

**Layer 1: Domain Services**
- `PlayerStatsManager`
- `EconomyService`
- `InventoryService`
- `CardInventory`
- `EquipmentService`

**Layer 2: UI Controllers**
- `MainMenuController`
- `GameController`
- `InventoryController`
- `CardCollectionController`
- `CraftingController`

**Layer 3: UI Components**
- Panels (full-screen overlays)
- Slots (individual item/card rows)
- Tooltips
- Popups
- HUD elements

**Data flow:** Domain → Controller → Component (one-way)

---

## 3. Event-Driven Updates

**Do NOT poll domain state every frame.**

**Correct pattern:**

```csharp
void OnEnable()
{
    // Subscribe to events
    EconomyManager.Instance.OnCurrencyChanged += UpdateCurrencyDisplay;
    InventoryService.Instance.OnInventoryChanged += RefreshInventory;
}

void OnDisable()
{
    // Unsubscribe
    if (EconomyManager.Instance != null)
        EconomyManager.Instance.OnCurrencyChanged -= UpdateCurrencyDisplay;
    
    if (InventoryService.Instance != null)
        InventoryService.Instance.OnInventoryChanged -= RefreshInventory;
}
```

**Benefits:**
- Updates only when data changes
- No per-frame overhead
- Clear dependencies

---

## 4. Tooltip System

**Tooltip positioning must account for:**
- Canvas size
- Tooltip size
- Mouse/screen position
- Offset
- Screen boundaries

**Anti-pattern:** Hardcoded offsets that only work at one resolution.

**Correct positioning:**

```csharp
void PositionTooltip(Vector2 mousePos, RectTransform tooltip)
{
    // 1. Get screen bounds
    Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);
    
    // 2. Calculate preferred position (right of cursor)
    Vector2 position = mousePos + new Vector2(20, 0);
    
    // 3. Get tooltip bounds
    Rect tooltipRect = new Rect(position, tooltip.sizeDelta);
    
    // 4. Clamp to screen
    if (tooltipRect.xMax > screenRect.xMax)
        position.x = mousePos.x - tooltip.sizeDelta.x - 20;  // Flip to left
    
    if (tooltipRect.yMax > screenRect.yMax)
        position.y = screenRect.yMax - tooltip.sizeDelta.y;
    
    if (tooltipRect.yMin < screenRect.yMin)
        position.y = screenRect.yMin;
    
    // 5. Apply
    tooltip.position = position;
}
```

---

## 5. Tooltip Persistence

**If tooltip exists in multiple scenes, decide ownership:**

**Option A: Scene-local**
- Each scene has own tooltip instance
- Destroyed on scene exit
- Simple, no persistence issues

**Option B: Single persistent**
- One tooltip survives all scenes
- DontDestroyOnLoad singleton
- Shared across scenes

**Decision must be explicit.** Do NOT blindly mark DontDestroyOnLoad.

---

## 6. Panel Management

**Open/close pattern:**

```csharp
public void OpenPanel(GameObject panel)
{
    // 1. Close other panels (if exclusive)
    CloseAllPanels();
    
    // 2. Activate panel
    panel.SetActive(true);
    
    // 3. Refresh data
    RefreshPanelData(panel);
    
    // 4. Play animation (optional)
    PlayOpenAnimation(panel);
}

public void ClosePanel(GameObject panel)
{
    // 1. Save state (if needed)
    SavePanelState(panel);
    
    // 2. Deactivate
    panel.SetActive(false);
}
```

---

## 7. Stat Display

**Never compute stats in UI:**

```csharp
// WRONG
float totalAttack = baseAttack;
foreach (var item in equipped)
    totalAttack += item.attackBonus;
// Missing: cards, attributes, buffs, percent modifiers!
```

**Correct:**

```csharp
// CORRECT
float totalAttack = PlayerStatsManager.GetFinalStat(SecondaryStatType.AttackDamage);
```

**UI reads authoritative pipeline, never rebuilds it.**

---

## 8. Currency Display

**Format large numbers:**

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

**Update on event:**

```csharp
void UpdateCurrencyDisplay(CurrencyType type, long newValue)
{
    switch (type)
    {
        case Gold:
            _goldText.text = FormatCurrency(newValue);
            break;
        case Gem:
            _gemText.text = FormatCurrency(newValue);
            break;
        // ...
    }
}
```

---

## 9. Inventory UI

**Slot pooling:**

```csharp
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private Transform _slotContainer;
    
    private List<InventorySlot> _slots = new();
    
    public void RefreshInventory()
    {
        // 1. Get inventory data
        List<InventoryItem> items = InventoryService.GetAllItems();
        
        // 2. Ensure enough slots
        EnsureSlotCount(items.Count);
        
        // 3. Update each slot
        for (int i = 0; i < items.Count; i++)
        {
            _slots[i].SetItem(items[i]);
            _slots[i].gameObject.SetActive(true);
        }
        
        // 4. Hide unused slots
        for (int i = items.Count; i < _slots.Count; i++)
        {
            _slots[i].gameObject.SetActive(false);
        }
    }
    
    void EnsureSlotCount(int required)
    {
        while (_slots.Count < required)
        {
            GameObject slotObj = Instantiate(_slotPrefab, _slotContainer);
            _slots.Add(slotObj.GetComponent<InventorySlot>());
        }
    }
}
```

---

## 10. Button States

**Disable buttons when action unavailable:**

```csharp
void UpdateCraftButton()
{
    bool canCraft = CraftService.CanCraft(selectedRecipe);
    _craftButton.interactable = canCraft;
    
    if (!canCraft)
    {
        // Show reason in tooltip
        _craftButton.GetComponent<TooltipTrigger>().SetText(
            CraftService.GetCraftBlockReason(selectedRecipe)
        );
    }
}
```

**Visual feedback:**
- Disabled: gray + not interactable
- Enabled: colored + interactable
- Hovered: highlight effect

---

## 11. Loading Screens

**Long operations show loading:**

```csharp
public class LoadingOverlay : MonoBehaviour
{
    public static void Show(string message = "Loading...")
    {
        // Block interaction
        // Show spinner
        // Display message
    }
    
    public static void Hide()
    {
        // Hide overlay
        // Re-enable interaction
    }
}

// Usage
LoadingOverlay.Show("Crafting...");
await CraftService.CraftAsync(recipe);
LoadingOverlay.Hide();
```

---

## 12. Error Messages

**Show validation errors:**

```csharp
public class ErrorPopup : MonoBehaviour
{
    public static void Show(string message)
    {
        // Instantiate popup
        // Set message text
        // Auto-dismiss after 3s
    }
}

// Usage
if (!InventoryService.HasSpace())
{
    ErrorPopup.Show("Inventory full");
    return;
}
```

---

## 13. Confirmation Dialogs

**Destructive actions need confirmation:**

```csharp
public class ConfirmDialog : MonoBehaviour
{
    public static void Show(string message, Action onConfirm, Action onCancel = null)
    {
        // Show dialog
        // Wire buttons
    }
}

// Usage
void OnSellAllClick()
{
    ConfirmDialog.Show(
        "Sell all items? This cannot be undone.",
        () => InventoryService.SellAll(),
        () => Debug.Log("Cancelled")
    );
}
```

---

## 14. Performance

**UI optimization:**
- Pool slots instead of Instantiate/Destroy
- Subscribe to events, don't poll
- Cache RectTransform references
- Use LayoutGroup sparingly (expensive)
- Disable panels when hidden (not just alpha=0)

**Do NOT:**
- Update every frame if data unchanged
- Use GetComponent in Update()
- Rebuild entire UI on minor change

---

## 15. Testing Checklist

```
[ ] UI updates on domain events
[ ] No per-frame polling
[ ] Tooltips position correctly at all resolutions
[ ] Buttons disabled when action unavailable
[ ] Currency format shows K/M/B
[ ] Inventory slots pool correctly
[ ] Panels save state on close
[ ] Loading overlay blocks interaction
[ ] Error messages auto-dismiss
[ ] Confirmation dialogs work for destructive actions
[ ] No memory leaks from event subscriptions
```

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-09-16 | Initial design doc | Documentation refactor |
