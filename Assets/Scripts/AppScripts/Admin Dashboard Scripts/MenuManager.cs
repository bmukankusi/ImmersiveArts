using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject sideMenuPanel;
    public GameObject dashboardPanel;
    public GameObject artworkmgtPanel;
    public GameObject adminSettingsPanel;
    public GameObject homePanel;
    public GameObject navPanel;

    [Header("Buttons")]
    public Button menuToggleButton;
    public Button closeMenuButton;
    public Button logoutButton;

    [Header("Menu Sprites")]
    public Sprite menuClosedSprite;
    public Sprite menuOpenSprite;

    Image _menuImage;

    void Start()
    {
        _menuImage = menuToggleButton?.GetComponent<Image>();

        // initialize
        SetMenuSprite(false);
        closeMenuButton?.onClick.AddListener(() => SetMenuState(false));
        menuToggleButton?.onClick.AddListener(ToggleSideMenu);
        logoutButton?.onClick.AddListener(Logout);
    }

    // Toggle side menu
    public void ToggleSideMenu() => SetMenuState(!(sideMenuPanel?.activeSelf ?? false));

    // Set side menu open/closed and update sprite / buttons
    void SetMenuState(bool open)
    {
        sideMenuPanel?.SetActive(open);
        SetMenuSprite(open);
        closeMenuButton?.gameObject.SetActive(open);
        // keep main toggle visible so sprite change is visible
        if (menuToggleButton != null) menuToggleButton.gameObject.SetActive(true);
    }

    // Helper to set burger sprite (open/closed)
    void SetMenuSprite(bool open)
    {
        if (_menuImage == null) _menuImage = menuToggleButton?.GetComponent<Image>();
        if (_menuImage != null)
            _menuImage.sprite = open ? menuOpenSprite ?? _menuImage.sprite : menuClosedSprite ?? _menuImage.sprite;
    }

    // Show exactly one panel (hides others) and resets menu state
    void ShowOnly(GameObject panel)
    {
        sideMenuPanel?.SetActive(false);
        dashboardPanel?.SetActive(false);
        artworkmgtPanel?.SetActive(false);
        adminSettingsPanel?.SetActive(false);
        navPanel?.SetActive(false);

        panel?.SetActive(true);
        SetMenuState(false);
    }

    public void OpenDashboardPanel() => ShowOnly(dashboardPanel);
    public void OpenArtworkmgtPanel() => ShowOnly(artworkmgtPanel);
    public void OpenAadminSettingsPanel() => ShowOnly(adminSettingsPanel);

    // Logout Function
    public void Logout()
    {
        Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut();
        ShowOnly(homePanel);
    }
}
