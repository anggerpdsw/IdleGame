using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Controller
{
    public class UpgradeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private Button _backButton;

        private void Start()
        {
            if (_title != null) _title.SetText("UPGRADE EQUIPMENT & SKILLS");
        }

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromUpgrade();

        private void OnEnable() => _backButton?.onClick.AddListener(OnBack);
        private void OnDisable() => _backButton?.onClick.RemoveListener(OnBack);

    }
}
