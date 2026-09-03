using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI.Tooltip
{
    /// <summary>
    /// Hover tooltip for crafting material rows. Shows material name and enemy drop sources.
    /// </summary>
    public class MaterialTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string ItemId;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(ItemId)) return;

            var itemData = ItemDatabase.Instance?.GetItem(ItemId);
            string name = itemData?.Name ?? ItemId;

            var enemies = DatabaseJSONCache.DatabaseEnemy?.enemies?
                .Where(e => e.dropItems != null && e.dropItems.Any(d => d.ItemId == ItemId))
                .Select(e => e.id)
                .ToArray() ?? System.Array.Empty<string>();

            var sb = new System.Text.StringBuilder();
            sb.Append("Material: ").Append(name);
            if (enemies.Length > 0)
            {
                sb.Append("\nDropped by:");
                foreach (var enemy in enemies)
                    sb.Append("\n- ").Append(enemy);
            }

            TooltipUI.Instance?.ShowText(sb.ToString(), eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}