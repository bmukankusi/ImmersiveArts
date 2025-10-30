using UnityEngine;
using UnityEngine.UI;

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
