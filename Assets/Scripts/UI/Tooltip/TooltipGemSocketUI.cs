using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.UI.Tooltip
{
    public class TooltipGemSocketUI : MonoBehaviour
    {
        [SerializeField] private Image _socketBackground;
        [SerializeField] private Image _gemIcon;
        [SerializeField] private Image _gemTypeColor;
        [SerializeField] private TextMeshProUGUI _gemLevelText;
        [SerializeField] private GameObject _lockedOverlay;

        public void Initialize(SocketData socket, InventoryItem parentItem)
        {
            if (socket.IsUnlocked)
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(false);
            }
            else
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(true);
            }

            if (socket.IsEmpty)
            {
                if (_gemIcon != null) _gemIcon.enabled = false;
                if (_gemTypeColor != null) _gemTypeColor.enabled = false;
                if (_gemLevelText != null) _gemLevelText.enabled = false;
            }
            else
            {
                var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
                if (gemData != null)
                {
                    if (_gemIcon != null && gemData.Icon != null)
                    {
                        _gemIcon.sprite = gemData.Icon;
                        _gemIcon.enabled = true;
                    }
                    if (_gemTypeColor != null)
                    {
                        _gemTypeColor.color = gemData.GemColor;
                        _gemTypeColor.enabled = true;
                    }
                    if (_gemLevelText != null)
                    {
                        _gemLevelText.text = $"Lv.{socket.GemLevel}";
                        _gemLevelText.enabled = true;
                    }
                }
            }
        }
    }

}