using UnityEngine;
using UnityEngine.UI;

public class AdminSideMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject dashboardPanel;
    public GameObject adminSettingsPanel;
    public GameObject adminSideMenuPanel;
    public GameObject artworkMgtPanel;
    public GameObject navButtonsPanel;


    // Show Dashboard Panel
    public void ShowDashboardPanel()
    {
        dashboardPanel.SetActive(true);
        adminSettingsPanel.SetActive(false);
        adminSideMenuPanel.SetActive(false);
        artworkMgtPanel.SetActive(false);
        navButtonsPanel.SetActive(false);
    }

    // Show Admin Settings Panel
    public void ShowAdminSettingsPanel()
    {
        adminSettingsPanel.SetActive(true);
        dashboardPanel.SetActive(false);
        adminSideMenuPanel.SetActive(false);
        artworkMgtPanel.SetActive(false);
        navButtonsPanel.SetActive(false);
    }

    // Show Artwork Management Panel
    public void ShowArtworkMgtPanel()
    {
        artworkMgtPanel.SetActive(true);
        dashboardPanel.SetActive(false);
        adminSettingsPanel.SetActive(false);
        adminSideMenuPanel.SetActive(false);
        navButtonsPanel.SetActive(false);
    }
}
