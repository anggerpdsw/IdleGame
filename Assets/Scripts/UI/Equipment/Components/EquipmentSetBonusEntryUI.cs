using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Equipment
{
    public class EquipmentSetBonusEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _setNameText;
        [SerializeField] private TextMeshProUGUI _tierText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;

        public void Initialize(string setName, string tierName, string description, int currentPieces, int requiredPieces)
        {
            if (_setNameText != null) _setNameText.text = setName;
            if (_tierText != null) _tierText.text = tierName;
            if (_descriptionText != null) _descriptionText.text = description;
            if (_progressBar != null) _progressBar.fillAmount = (float)currentPieces / requiredPieces;
            if (_progressText != null) _progressText.text = $"{currentPieces}/{requiredPieces}";
        }
    }
}