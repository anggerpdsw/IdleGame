using System;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Mission
{
    /// <summary>
    /// Single mission row inside MissionUI. Bind label, progress label,
    /// background, icon, button, and state text in the inspector.
    ///</summary>
    public class MissionSlot : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _progressLabel;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _state;

        public Image Background => _background;

        private int _slotIndex;
        private string _instanceId;
        private Action<int, string> _onClaim;
        private Action<int, string> _onCancel;

        public void Initialize(int slotIndex, Action<int, string> onClaim, Action<int, string> onCancel)
        {
            _slotIndex = slotIndex;
            _onClaim = onClaim;
            _onCancel = onCancel;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        public void Refresh(MissionInstance mission, MissionTemplate template, Sprite icon)
        {
            if (mission == null)
            {
                if (_label != null) _label.text = $"Slot {_slotIndex + 1}";
                if (_progressLabel != null) _progressLabel.text = string.Empty;
                if (_state != null) _state.text = string.Empty;
                if (_icon != null) _icon.enabled = false;
                if (_button != null) _button.interactable = false;
                _instanceId = null;
                return;
            }

            _instanceId = mission.instanceId;

            if (_label != null)
                _label.text = !string.IsNullOrEmpty(template?.name)
                    ? template.name
                    : mission.missionId;

            if (_progressLabel != null)
                _progressLabel.text = $"{Utilityku.FormatNumber(mission.currentCount)} / {Utilityku.FormatNumber(mission.targetCount)}";

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_background != null)
                _background.color = GetBackgroundColor(mission.status);

            if (_button != null)
            {
                bool actionable = mission.status == MissionStatus.Completed || mission.status == MissionStatus.Active;
                _button.interactable = actionable;
                if (_button.image != null)
                    _button.image.sprite = ButtonResources.GetColor(GetButtonColor(mission.status));
            }

            if (_state != null)
                _state.text = GetStateText(mission);
        }

        private void OnButtonClicked()
        {
            var service = MissionService.Instance;
            if (service == null || string.IsNullOrEmpty(_instanceId)) return;

            var m = service.GetMission(_instanceId);
            if (m == null) return;

            if (m.status == MissionStatus.Completed) _onClaim?.Invoke(_slotIndex, _instanceId);
            else if (m.status == MissionStatus.Active) _onCancel?.Invoke(_slotIndex, _instanceId);
        }

        private static Color GetBackgroundColor(MissionStatus status) => status switch
        {
            MissionStatus.Completed => GameColors.green,
            MissionStatus.Claimed => GameColors.gray,
            MissionStatus.Cancelled => GameColors.red,
            _ => new Color(0.184f, 0.184f, 0.239f, 0.95f),
        };

        private static string GetButtonColor(MissionStatus status) => status switch
        {
            MissionStatus.Completed => "Green",
            MissionStatus.Active => "Red",
            _ => "Grey",
        };

        private static string GetStateText(MissionInstance m)
        {
            switch (m.status)
            {
                case MissionStatus.Completed: return "Claim";
                case MissionStatus.Active: return "Cancel";
                case MissionStatus.Claimed:
                case MissionStatus.Cancelled:
                    if (!string.IsNullOrEmpty(m.cooldownUntil)
                        && DateTimeOffset.TryParse(m.cooldownUntil, out var end))
                    {
                        var remaining = end - DateTimeOffset.UtcNow;
                        return remaining > TimeSpan.Zero
                            ? Utilityku.FormatDuration(remaining)
                            : "Soon";
                    }
                    return m.status == MissionStatus.Claimed ? "Claimed" : "Cancelled";
                default: return string.Empty;
            }
        }
    }
}
