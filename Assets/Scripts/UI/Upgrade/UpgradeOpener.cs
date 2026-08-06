using UnityEngine;
using UnityEngine.UI;

public class UpgradeOpener : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField] private GameObject _upgradePanel;

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
        if (_upgradePanel != null) _upgradePanel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (_upgradePanel == null) return;

        _isOpen = !_isOpen;
        _upgradePanel.SetActive(_isOpen);
    }
}