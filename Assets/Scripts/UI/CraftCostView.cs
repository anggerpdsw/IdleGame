using UnityEngine;
using TMPro;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Crafting; // For CurrencySnapshot

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays craft costs for Gold and Gems, indicating affordability.
    /// </summary>
    public class CraftCostView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _gemText;

        public void Show(CurrencySnapshot snap)
        {
            if (_goldText == null || _gemText == null)
            {
                Debug.LogWarning("[CraftCostView] Gold or Gem Text references missing.");
                return;
            }

            gameObject.SetActive(snap.GoldSnapshot > 0 || snap.GemSnapshot > 0 || snap.AdditionalCosts.Length > 0);

            _goldText.text = snap.GoldSnapshot.ToString();
            _goldText.color = EconomyManager.Instance.GetCurrency(CurrencyType.Gold) >= snap.GoldSnapshot ? GameColors.white : GameColors.red;

            _gemText.text = snap.GemSnapshot.ToString();
            _gemText.color = EconomyManager.Instance.GetCurrency(CurrencyType.Gem) >= snap.GemSnapshot ? GameColors.white : GameColors.red;
        }
    }
}
