
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Controller
{
    public class VictoryController : MonoBehaviour
    {
        private VictoryData _result;
        [SerializeField] private GameObject _panelnya;
        [SerializeField] private GameObject _defeat;
        [SerializeField] private RectTransform _totalReward;
        [SerializeField] private TextMeshProUGUI _tier;
        [SerializeField] private TextMeshProUGUI _wave;
        [SerializeField] private TextMeshProUGUI _goldEarned;
        [SerializeField] private TextMeshProUGUI _meatEarned;
        [SerializeField] private TextMeshProUGUI _bonusGold;
        [SerializeField] private TextMeshProUGUI _bonusMeat;
        [SerializeField] private TextMeshProUGUI _totalGold;
        [SerializeField] private TextMeshProUGUI _totalMeat;
        [SerializeField] private Button _close;

        private float _posBG;

        private void OnEnable()
        {
            WaveManager.OnRunCompleted += ShowPopup;
            if (_close != null) _close.onClick.AddListener(OnClaim);
        }

        private void OnDisable()
        {
            WaveManager.OnRunCompleted -= ShowPopup;
            if (_close != null) _close.onClick.RemoveListener(OnClaim);
        }

        private void ShowPopup(VictoryData result)
        {
            _result = result;
            if (result.State == WaveState.Defeat) {
                _posBG = -271f;
                _defeat.SetActive(true);
            }
            else
            {
                _posBG = -370f;
                _defeat.SetActive(false);
            }

            _totalReward.anchoredPosition = new Vector2(_totalReward.anchoredPosition.x, _posBG);
            _panelnya.SetActive(true);

            _tier.text = $"T {result.Tier}";
            _wave.text = $"W {result.HighestWave}";
            _goldEarned.text = result.GoldEarned.ToString();
            _meatEarned.text = result.MeatEarned.ToString();
            _bonusGold.text  = result.BonusGold.ToString();
            _bonusMeat.text  = result.BonusMeat.ToString();
            _totalGold.text  = result.TotalGold.ToString();
            _totalMeat.text  = result.TotalMeat.ToString();
        }
        
        public void OnClaim()
        {
            EconomyManager.Instance.AddCurrency(CurrencyType.Gold, _result.BonusGold);
            EconomyManager.Instance.AddCurrency(CurrencyType.Meat, _result.BonusMeat);
            
            // SceneManager.LoadScene("MainMenu");
            SceneLoader.Instance.ReturnToMainMenuFromGame();
        }
    }
    
}