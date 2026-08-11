using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Binds ViewInventory → Viewport → Content to DropBagManager (run drop recap).
    ///
    /// Panel starts INACTIVE (toggled by the "Bag" button via PanelOpener), so:
    /// - Subscribe/unsubscribe in OnEnable/OnDisable (no duplicates, no leaks).
    /// - Rebuild the whole list from the authoritative state on enable, so drops
    ///   that happened while the panel was closed still show up.
    /// No per-frame polling — event-driven via DropBagManager.
    /// </summary>
    public class DropBagUI : MonoBehaviour
    {
        [Tooltip("ViewInventory/Viewport/Content")]
        [SerializeField] private RectTransform _content;
        [Tooltip("Optional: prefab with DropBagEntryUI (Icon/Name/Qty wired)")]
        [SerializeField] private GameObject _slotPrefab;
        [Tooltip("Fallback icon when the item definition is missing")]
        [SerializeField] private Sprite _fallbackSprite;

        private readonly Dictionary<string, DropBagEntryUI> _entries = new();

        private void Awake()
        {
            // Safe fallback: panel-local path only, never a global GameObject.Find.
            if (_content == null)
            {
                var viewport = transform.Find("Viewport");
                if (viewport != null) _content = viewport.Find("Content") as RectTransform;
            }
        }

        private void OnEnable()
        {
            if (DropBagManager.Instance == null) return;
            DropBagManager.Instance.OnDropAdded += HandleDropAdded;
            DropBagManager.Instance.OnCleared += HandleCleared;
            RebuildAll(); // panel was closed during drops — refresh from state
        }

        private void OnDisable()
        {
            if (DropBagManager.Instance == null) return;
            DropBagManager.Instance.OnDropAdded -= HandleDropAdded;
            DropBagManager.Instance.OnCleared -= HandleCleared;
        }

        private void HandleDropAdded(string itemId, int quantity) => UpdateEntry(itemId, quantity);

        private void HandleCleared()
        {
            foreach (var entry in _entries.Values)
                Destroy(entry.gameObject);
            _entries.Clear();
        }

        /// <summary>Source of truth = DropBagManager.Items; this list is only its visual representation.</summary>
        private void RebuildAll()
        {
            HandleCleared();
            if (DropBagManager.Instance == null || _content == null) return;
            foreach (var kv in DropBagManager.Instance.Items)
                UpdateEntry(kv.Key, kv.Value);
        }

        private void UpdateEntry(string itemId, int quantity)
        {
            if (_content == null) return;

            var itemData = ItemDatabase.Instance?.GetItem(itemId);

            if (!_entries.TryGetValue(itemId, out var entry))
            {
                entry = CreateEntry(itemId);
                if (entry == null) return;
                _entries[itemId] = entry;
            }
            entry.Set(itemData, itemId, quantity, _fallbackSprite);
        }

        private DropBagEntryUI CreateEntry(string itemId)
        {
            if (_slotPrefab != null)
            {
                var go = Instantiate(_slotPrefab, _content);
                var comp = go.GetComponent<DropBagEntryUI>();
                if (comp != null) return comp;
                Destroy(go);
            }

            // Runtime entry: [Icon] Name xQty — sized for the existing GridLayoutGroup.
            var root = new GameObject($"DropEntry_{itemId}", typeof(RectTransform), typeof(DropBagEntryUI));
            root.transform.SetParent(_content, false);
            var entry = root.GetComponent<DropBagEntryUI>();

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            var icon = iconGo.GetComponent<Image>();
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.sizeDelta = new Vector2(60, 60);
            icon.preserveAspect = true;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(root.transform, false);
            var nameRect = (RectTransform)nameGo.transform;
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 0.5f);
            nameRect.offsetMin = new Vector2(70, -22f);
            nameRect.offsetMax = new Vector2(-90, 22f);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = 24f;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            var qtyGo = new GameObject("Qty", typeof(RectTransform), typeof(TextMeshProUGUI));
            qtyGo.transform.SetParent(root.transform, false);
            var qtyRect = (RectTransform)qtyGo.transform;
            qtyRect.anchorMin = new Vector2(1, 0.5f);
            qtyRect.anchorMax = new Vector2(1, 0.5f);
            qtyRect.anchoredPosition = new Vector2(-8, 0);
            qtyRect.sizeDelta = new Vector2(80, 30);
            var qtyText = qtyGo.GetComponent<TextMeshProUGUI>();
            qtyText.fontSize = 22f;
            qtyText.alignment = TextAlignmentOptions.Right;

            // Bind references on the component (private fields, via reflection-free path:
            // DropBagEntryUI re-binds through its own fields below).
            BindEntry(entry, icon, nameText, qtyText);
            return entry;
        }

        private static void BindEntry(DropBagEntryUI entry, Image icon, TextMeshProUGUI nameText, TextMeshProUGUI qtyText)
        {
            // Private serialized fields cannot be set directly; DropBagEntryUI exposes
            // a bind API instead of making fields public.
            entry.Bind(icon, nameText, qtyText);
        }
    }
}
