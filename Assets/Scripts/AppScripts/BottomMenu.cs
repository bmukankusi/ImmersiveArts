using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the navigation between different panels in the application.
/// </summary>
/// <remarks>This class provides methods to switch between predefined panels, such as the home, explore, and
/// settings panels. It ensures that only one panel is active at a time.</remarks>

public class AppNavigation : MonoBehaviour
{
    [Header("Panels")]
    public GameObject homePanel;
    public GameObject explorePanel;
    public GameObject settingsPanel;
    

    private GameObject[] panels;

    private void Awake()
    {
        panels = new[] { homePanel, explorePanel, settingsPanel };
    }

    public void ShowHomePanel()
    {
        SetActivePanel(homePanel);
    }

    public void ShowExplorePanel()
    {
        SetActivePanel(explorePanel);
    }

    public void ShowSettingsPanel()
    {
        SetActivePanel(settingsPanel);
    }


    private void SetActivePanel(GameObject activePanel)
    {
        foreach (var panel in panels)
        {
            if (panel != null)
                panel.SetActive(panel == activePanel);
        }
    }
}
