using UnityEngine;
using IdleDefenseSurvival.Core;

public class EventDebug : MonoBehaviour 
{
    void OnGUI() 
    {
        if (GUI.Button(new Rect(10, 10, 150, 40), "Start Abyss Event")) {
            ServiceLocator.EventService?.StartEvent("abyss_awakens");
        }
        
        if (ServiceLocator.EventService?.IsEventActive() == true) {
            GUI.Label(new Rect(10, 60, 300, 20), 
                $"Threat: {ServiceLocator.EventService.CurrentThreat}/100");
            
            if (GUI.Button(new Rect(10, 90, 150, 40), "Show Choice Dialog")) {
                // Trigger manual via EventChoiceUI reference di scene
                var choiceUI = FindFirstObjectByType<IdleDefenseSurvival.UI.Event.EventChoiceUI>();
                choiceUI?.Show();
            }
        }
    }
}
