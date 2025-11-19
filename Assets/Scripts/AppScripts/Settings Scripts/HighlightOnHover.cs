using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Highlights a TextMeshPro text when hovered. 
/// </summary>
public class HighlightOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text targetText;
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
        // Ensure we restore original color when component is disabled
        RestoreColor();
    }

    // UI hover
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
