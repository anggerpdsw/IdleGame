using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple script to open/close Panel when button is clicked.
/// Attach to Button GameObject.
/// </summary>
public class PanelOpener : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _button;
    private bool _isOpen = false;

    private void Start()
    {
        if (_button != null) _button.onClick.AddListener(TogglePanel);
        // Ensure panel starts hidden
        if (_panel != null) _panel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (_panel == null) return;
        _isOpen = !_isOpen;
        _panel.SetActive(_isOpen);
    }

}