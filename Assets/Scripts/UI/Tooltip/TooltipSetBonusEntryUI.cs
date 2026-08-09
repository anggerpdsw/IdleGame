using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.UI.Tooltip
{
    public class TooltipSetBonusEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _setNameText;
        [SerializeField] private TextMeshProUGUI _tierText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;

        public void Initialize(SetBonusData setData, SetBonusTier tier, int currentPieces)
        {
            if (_setNameText != null)
                _setNameText.text = setData.SetName;

            if (_tierText != null)
                _tierText.text = tier.TierName;

            if (_descriptionText != null)
                _descriptionText.text = tier.Description;

            if (_progressBar != null)
            {
                _progressBar.fillAmount = (float)currentPieces / tier.RequiredPieces;
            }

            if (_progressText != null)
            {
                _progressText.text = $"{currentPieces}/{tier.RequiredPieces}";
            }
        }
    }

}