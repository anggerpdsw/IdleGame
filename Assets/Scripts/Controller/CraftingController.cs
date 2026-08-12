using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Crafting scene controller.
    /// </summary>
    public class CraftingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button _backButton;

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromCrafting();

        private void OnEnable() => _backButton?.onClick.AddListener(OnBack);
        private void OnDisable() => _backButton?.onClick.RemoveListener(OnBack);

    }
}
