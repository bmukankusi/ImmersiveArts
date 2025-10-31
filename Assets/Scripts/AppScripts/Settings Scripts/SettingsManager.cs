using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Manages the visibility of various panels in the application, including the About, Terms and Conditions, Settings,
/// and Bottom Menu panels.
/// </summary>
/// <remarks>This class provides methods to control the active state of specific panels, ensuring that only the
/// relevant panel is displayed at a time. It is designed to be used in a UI context where panels are toggled based on
/// user interaction.</remarks>

public class SettingsManager : MonoBehaviour
{
    public GameObject aboutPanel;
    public GameObject termsconditionsPanel;
    public GameObject settingsPanel;
    public GameObject bottomMenuPanel;

    // Open aboutPanel
    public void ShowAboutPanel()
    {
        aboutPanel.SetActive(true);
        settingsPanel.SetActive(false);
        bottomMenuPanel.SetActive(false);
    }

    // Open termsconditions Panel
    public void ShowTermsPanel()
    { 
        termsconditionsPanel.SetActive(true);
        settingsPanel.SetActive(false);
        bottomMenuPanel.SetActive(false);
    }

    // Close aboutPanel or termsconditionsPanel and return to settingsPanel
    public void BackToSettingsPanel()
    {
        aboutPanel.SetActive(false);
        termsconditionsPanel.SetActive(false);
        settingsPanel.SetActive(true);
        bottomMenuPanel.SetActive(true);
    }
}
