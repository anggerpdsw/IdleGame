using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opens/closes a panel.
/// Only one PanelOpener can have its panel open at a time.
/// </summary>
public class PanelOpener : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _button;

    private bool _isOpen;
    private static PanelOpener _currentOpen;

    private void Start()
    {
        if (_button != null) _button.onClick.AddListener(TogglePanel);
        if (_panel != null) _panel.SetActive(false);
        _isOpen = false;
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(TogglePanel);
        if (_currentOpen == this) _currentOpen = null;
    }

    public void TogglePanel()
    {
        if (_panel == null) return;
        // Jika panel ini sedang terbuka → tutup
        if (_isOpen)
        {
            ClosePanel();
            return;
        }
        // Tutup panel lain yang sedang terbuka
        if (_currentOpen != null && _currentOpen != this)
            _currentOpen.ClosePanel();
        // Buka panel ini
        OpenPanel();
    }

    private void OpenPanel()
    {
        _isOpen = true;
        _panel.SetActive(true);
        _currentOpen = this;
    }

    private void ClosePanel()
    {
        _isOpen = false;
        if (_panel != null) _panel.SetActive(false);
        if (_currentOpen == this) _currentOpen = null;
    }
}