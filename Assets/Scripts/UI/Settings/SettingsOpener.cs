using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple script to open/close Settings Panel when button is clicked.
/// Attach to SettingsButton GameObject.
/// </summary>
public class SettingsOpener : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField] private GameObject _settingsPanel;

    private Button _button;
    private bool _isOpen = false;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void Start()
    {
        if (_button != null) _button.onClick.AddListener(TogglePanel);

        // Ensure panel starts hidden
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (_settingsPanel == null) return;

        _isOpen = !_isOpen;
        _settingsPanel.SetActive(_isOpen);
    }

}