using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("UI References")]
    public Button menuButton;             // Button the user clicks
    public Image menuIcon;                // Image component to swap sprites
    public Sprite menuOpenSprite;         // Sprite shown when menu is closed (hint: show "open" icon)
    public Sprite menuCloseSprite;        // Sprite shown when menu is open (hint: show "close" icon)
    public RectTransform sidePanel;       // The sliding panel's RectTransform

    [Header("Layout")]
    public bool anchoredFromLeft = true;  // true if panel slides in/out from left
    public bool startOpen = false;        // whether panel starts opened
    public float offscreenMargin = 10f;   // extra offset when hiding panel

    private Vector2 openedPos;
    private Vector2 closedPos;
    private bool isOpen;

    void Awake()
    {
        // auto-wire common references
        if (menuButton == null)
            menuButton = GetComponent<Button>();

        if (menuIcon == null && menuButton != null)
            menuIcon = menuButton.GetComponent<Image>();
    }

    void Start()
    {
        if (sidePanel == null)
        {
            Debug.LogError("MenuManager: sidePanel is not assigned.");
            enabled = false;
            return;
        }

        if (menuButton == null)
        {
            Debug.LogError("MenuManager: menuButton is not assigned.");
            enabled = false;
            return;
        }

        ComputePositions();

        isOpen = startOpen;
        sidePanel.anchoredPosition = isOpen ? openedPos : closedPos;
        if (menuIcon != null)
            menuIcon.sprite = isOpen ? menuCloseSprite : menuOpenSprite;

        menuButton.onClick.AddListener(ToggleMenu);
    }

    void OnDestroy()
    {
        if (menuButton != null)
            menuButton.onClick.RemoveListener(ToggleMenu);
    }

    // Public API to recompute positions if the panel size/layout changes at runtime
    public void ComputePositions()
    {
        openedPos = sidePanel.anchoredPosition;
        float width = sidePanel.rect.width;
        closedPos = openedPos;
        if (anchoredFromLeft)
            closedPos.x = openedPos.x - (width + offscreenMargin);
        else
            closedPos.x = openedPos.x + (width + offscreenMargin);
    }

    public void ToggleMenu()
    {
        SetIsOpen(!isOpen);
    }

    public void SetIsOpen(bool open)
    {
        // Recompute in case layout changed
        ComputePositions();

        sidePanel.anchoredPosition = open ? openedPos : closedPos;

        if (menuIcon != null)
            menuIcon.sprite = open ? menuCloseSprite : menuOpenSprite;

        isOpen = open;
    }
}
