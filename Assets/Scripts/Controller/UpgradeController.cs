using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.UI.Upgrade;

namespace IdleDefenseSurvival.Controller
{
    public class UpgradeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private Button _backButton;
        [SerializeField] private UpgradePanelUI _upgradePanel;

        private void Start()
        {
            if (_title != null)
                _title.text = "UPGRADE EQUIPMENT";

            if (_upgradePanel != null)
                _upgradePanel.gameObject.SetActive(true);
        }

        public void OnBack()
        {
            SceneLoader.Instance.ReturnToMainMenuFromUpgrade();
        }

        private void OnEnable()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(OnBack);
        }

        private void OnDisable()
        {
            if (_backButton != null)
                _backButton.onClick.RemoveListener(OnBack);
        }
    }
}
