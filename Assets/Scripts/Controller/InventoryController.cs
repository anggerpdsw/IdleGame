using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Inventory scene controller.
    /// Builds the whole inventory UI at runtime from InventoryService:
    /// left = equipment paper-doll (existing scene art), right = item grid + info panel.
    /// Subscribes to InventoryService events so any loot/equip change refreshes live.
    /// </summary>
    public class InventoryController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private Button _backButton;

        private void Start()
        {
            if (_title != null) _title.SetText("INVENTORY");
        }

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromInventory();

        private void OnEnable() => _backButton?.onClick.AddListener(OnBack);
        private void OnDisable() => _backButton?.onClick.RemoveListener(OnBack);

    }
}
