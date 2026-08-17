using UnityEngine;
using UnityEditor;
using IdleDefenseSurvival.Controller;
using TMPro;
using UnityEngine.UI;

public class CraftingSceneWireTool : MonoBehaviour
{
    [MenuItem("Tools/Wire Crafting UI")]
    public static void Wire()
    {
        var menu = GameObject.Find("CraftingMenu");
        if (menu == null) { Debug.LogError("CraftingMenu not found"); return; }

        var controller = menu.AddComponent<CraftingUIController>();

        // Find refs
        SerializedObject so = new SerializedObject(controller);

        // Helper to find and set
        void SetRef(string field, string name) {
            var go = GameObject.Find(name);
            if (go != null) so.FindProperty(field).objectReferenceValue = go.GetComponent(field.Contains("Text") ? typeof(TextMeshProUGUI) : (field.Contains("Button") ? typeof(Button) : (field.Contains("Slider") ? typeof(Slider) : typeof(RectTransform))));
            else Debug.LogError($"Could not find {name}");
        }

        SetRef("_recipeContent", "Content");
        SetRef("_recipeEntryTemplate", "RecipeEntry");
        SetRef("_resultIcon", "ResultIcon");
        SetRef("_resultName", "ResultName");
        SetRef("_descriptionText", "DescriptionText");
        SetRef("_rarityText", "RarityText");
        SetRef("_materialList", "MaterialList");
        SetRef("_materialRowTemplate", "MaterialRow");
        SetRef("_goldCostText", "GoldCost");
        SetRef("_gemCostText", "GemCost");
        SetRef("_quantityText", "QuantityText");
        SetRef("_plusButton", "Plus");
        SetRef("_minusButton", "Minus");
        SetRef("_craftButton", "CraftButton");
        SetRef("_progressSlider", "Progress");
        SetRef("_feedbackText", "Message");

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(menu);
        AssetDatabase.SaveAssets();
        Debug.Log("Crafting UI wired successfully.");
    }
}