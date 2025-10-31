using UnityEngine;
using UnityEngine.UI;

public class HumbergerMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject menuPanel;          
    public Sprite humMenuSprite;          
    public Sprite closeSprite;            

    Button button;
    Image buttonImage;
    bool lastPanelState;

    void Awake()
    {
        button = GetComponent<Button>();
        buttonImage = GetComponent<Image>();

        if (menuPanel == null)
            Debug.LogWarning("HumbergerMenu: `menuPanel` is not assigned.", this);
        if (button == null)
            Debug.LogWarning("HumbergerMenu: Button component missing.", this);
        if (buttonImage == null)
            Debug.LogWarning("HumbergerMenu: Image component missing. The button needs an Image to swap sprites.", this);

        if (button != null)
        {
            // ensure no double add listener
            button.onClick.RemoveListener(ToggleMenu);
            button.onClick.AddListener(ToggleMenu);
        }

        lastPanelState = menuPanel != null && menuPanel.activeSelf;
        UpdateButtonSprite(lastPanelState);
    }

    // Toggle the menu panel visibility
    public void ToggleMenu()
    {
        if (menuPanel == null) return;

        bool newState = !menuPanel.activeSelf;
        menuPanel.SetActive(newState);
        UpdateButtonSprite(newState);
        lastPanelState = newState;
    }

    // Keep button sprite synced in case the panel is toggled from elsewhere
    void Update()
    {
        if (menuPanel == null || buttonImage == null) return;

        bool current = menuPanel.activeSelf;
        if (current != lastPanelState)
        {
            UpdateButtonSprite(current);
            lastPanelState = current;
        }
    }

    void UpdateButtonSprite(bool panelActive)
    {
        if (buttonImage == null) return;
        buttonImage.sprite = panelActive ? closeSprite : humMenuSprite;
    }
}
