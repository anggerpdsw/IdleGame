using IdleDefenseSurvival;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardRollButtonUI : MonoBehaviour
{
    [SerializeField] private GameObject _gemRoot;
    [SerializeField] private TextMeshProUGUI _gemCost;
    [SerializeField] private TextMeshProUGUI _gemCount;
    [SerializeField] private GameObject _cardRollRoot;
    [SerializeField] private TextMeshProUGUI _cardRollCount;
    [SerializeField] private Button _button;

    public void Setup(int rollCount, int gemCostValue, bool useCardRoll,
        UnityEngine.Events.UnityAction onClick)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(onClick);

        // Payable via CardRoll first; gem cost only matters when no CardRoll available.
        bool hasGem = EconomyManager.Instance.HasEnoughCurrency(CurrencyType.Gem, gemCostValue);
        _button.image.sprite = ButtonResources.GetColor(useCardRoll || hasGem ? "Blue" : "Grey");

        _gemRoot.SetActive(!useCardRoll);
        _cardRollRoot.SetActive(useCardRoll);

        if (useCardRoll)
        {
            _cardRollCount.text = $"x{rollCount}";
        }
        else
        {
            _gemCount.text = $"x{rollCount}";
            _gemCost.text = gemCostValue.ToString();
        }
    }
}