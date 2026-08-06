using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Reward
{
    public class RewardSlot : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI amountText;

        public void Setup(RewardData reward)
        {
            // Set amount
            if (amountText != null)
                amountText.text = reward.Amount.ToString("N0");
                
            // Set icon
            if (icon != null)
                icon.sprite = RewardResources.GetRewardType(reward.Type.ToString());
        }
        
    }
}