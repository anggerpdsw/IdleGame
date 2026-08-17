using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Items;

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
        private TextMeshProUGUI _nameText;
        private Image _iconImage;
        private Button _button;

        private string _recipeId;
        private CraftingUIController _controller;

        public void Initialize(string recipeId, string displayName, Sprite icon, CraftingUIController controller)
        {
            _recipeId = recipeId;
            _controller = controller;

            _nameText = GetComponentInChildren<TextMeshProUGUI>(true);
            _iconImage = GetComponentInChildren<Image>(true);
            _button = GetComponentInChildren<Button>(true);

            if (_nameText != null) _nameText.text = displayName;
            if (_iconImage != null && icon != null) _iconImage.sprite = icon;

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