using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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


    public void OpenARScene()
    {
        // Open a new scene for AR experience
        SceneManager.LoadScene("AR Scene");

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
