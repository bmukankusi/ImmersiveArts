using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Highlights a TextMeshPro (3D or UGUI) text when hovered. 
/// Works with UI (IPointerEnter/Exit) and world-space texts (OnMouseEnter/Exit — requires a Collider).
/// Restores the original color on exit or disable.
/// </summary>
public class HighlightOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("TextMeshPro component to highlight. If null the component will try to find one on this GameObject or its children.")]
    [SerializeField] private TMP_Text targetText;

    [Tooltip("Color used for the highlight.")]
    [SerializeField] private Color highlightColor = Color.yellow;

    private Color originalColor;
    private bool originalCaptured;

    void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>();

        if (targetText == null)
        {
            Debug.LogWarning($"[{nameof(HighlightOnHover)}] No TMP_Text found on '{gameObject.name}'. This component will do nothing.");
            return;
        }

        // Capture original color once at startup
        originalColor = targetText.color;
        originalCaptured = true;
    }

    void OnDisable()
    {
        // Ensure we restore original color when component/GO is disabled
        RestoreColor();
    }

    // UI hover (requires EventSystem + GraphicRaycaster)
    public void OnPointerEnter(PointerEventData eventData) => ApplyHighlight();
    public void OnPointerExit(PointerEventData eventData) => RestoreColor();

    // 3D hover (requires Collider on this GameObject)
    void OnMouseEnter() => ApplyHighlight();
    void OnMouseExit() => RestoreColor();

    private void ApplyHighlight()
    {
        if (targetText == null) return;
        targetText.color = highlightColor;
    }

    private void RestoreColor()
    {
        if (targetText == null || !originalCaptured) return;
        targetText.color = originalColor;
    }
}
