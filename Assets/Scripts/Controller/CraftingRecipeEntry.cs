using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Small component attached to each instantiated RecipeEntry clone.
    /// Holds the RecipeId and notifies the parent controller on click.
    /// Self-resolves UI components so it works when added via AddComponent at runtime
    /// (serialized fields would be null otherwise).
    /// </summary>
    public class CraftingRecipeEntry : MonoBehaviour
    {
        [SerializeField] private Image _rarity;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Button _button;

        private string _recipeId;
        private CraftingUIController _controller;

        public void Initialize(string recipeId, Sprite icon, Rarity rarity, CraftingUIController controller)
        {
            _recipeId = recipeId;
            _controller = controller;

            if (_iconImage != null && icon != null) _iconImage.sprite = icon;

            // Apply rarity color to the rarity image
            if (_rarity != null)
                _rarity.color = GameColors.GetRarityColor(rarity);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClicked);
            }
            else
            {
                Debug.LogWarning($"[CraftingRecipeEntry] No Button found on {gameObject.name}");
            }
        }

        public void SetAffordable(bool affordable, Color dimColor, Color normalColor)
        {
            // Keep button interactable so user can click to view details
            // The main craft button in CraftingUIController handles the actual craft disable
            if (_iconImage != null)
                _iconImage.color = affordable ? normalColor : dimColor;
        }

        private void OnClicked()
        {
            if (_controller != null && !string.IsNullOrEmpty(_recipeId))
                _controller.OnRecipeSelected(_recipeId);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveAllListeners();
        }
    }
}