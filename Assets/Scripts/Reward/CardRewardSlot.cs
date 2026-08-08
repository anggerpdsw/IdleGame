using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Reward
{
    /// <summary>
    /// Specialized slot for displaying card roll results with duplicate, upgrade, and pity indicators
    /// </summary>
    public class CardRewardSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image cardIcon;
        [SerializeField] private Image rarityFrame;
        [SerializeField] private GameObject duplicateBadge;
        [SerializeField] private TextMeshProUGUI duplicateCountText;
        [SerializeField] private GameObject newCardBadge;
        [SerializeField] private GameObject pityGuaranteedBadge;

        public void Setup(CardReward cardReward, CardRollResult rollResult)
        {
            var cardData = CardDatabase.Instance.GetCard(cardReward.CardId);
            if (cardData == null) return;

            // Set card icon
            if (cardIcon != null && !string.IsNullOrEmpty(cardData.Id))
                cardIcon.sprite = CardResources.GetIcon(cardData.Id);

            // Set rarity frame color
            if (rarityFrame != null)
                rarityFrame.sprite = CardResources.GetFrame(cardReward.ItemRarity.ToString());

            // Show duplicate badge
            if (duplicateBadge != null)
            {
                bool isDuplicate = cardReward.IsDuplicate || rollResult.Cards.FindIndex(c => c.CardId == cardReward.CardId) != rollResult.Cards.FindLastIndex(c => c.CardId == cardReward.CardId);
                duplicateBadge.SetActive(isDuplicate);
                if (isDuplicate && duplicateCountText != null)
                {
                    duplicateCountText.text = "x" + cardReward.Quantity;
                }
            }

            // Show new card badge
            if (newCardBadge != null)
                newCardBadge.SetActive(cardReward.IsNewCard || (!cardReward.IsDuplicate && rollResult.HasNewCard));

            // Show pity guaranteed badge
            if (pityGuaranteedBadge != null)
                pityGuaranteedBadge.SetActive(cardReward.IsPityGuaranteed);
        }

    }
}