using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Crafting scene controller.
    /// </summary>
    public class CraftingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button _backButton;

        void Start()
        {
            CraftingManager.Instance?.CheckAutoUnlocks();
        }

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromCrafting();

        private void OnEnable() {
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
        }
        private void OnDisable() {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
        }


    }
}
