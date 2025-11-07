using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component that toggles password visibility for a TMP_InputField using a Button.
/// Attach to any GameObject (often same GameObject as the view button). Assign the
/// button, the password input field and an optional text label for the button.
/// </summary>
public class PasswordViewToggle : MonoBehaviour
{
    [Header("Bindings")]
    public TMP_InputField passwordInput;
    public Button viewButton;
    public TMP_Text viewButtonText;

    [Header("Labels")]
    public string viewLabel = "View";
    public string hideLabel = "Hide";

    void Start()
    {
        if (viewButton != null)
            viewButton.onClick.AddListener(TogglePasswordVisibility);

        // initialize button label to match current content type
        UpdateButtonLabel();
    }

    void OnDestroy()
    {
        if (viewButton != null)
            viewButton.onClick.RemoveListener(TogglePasswordVisibility);
    }

    public void TogglePasswordVisibility()
    {
        if (passwordInput == null) return;

        var current = passwordInput.contentType;
        if (current == TMP_InputField.ContentType.Password || current == TMP_InputField.ContentType.Pin)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Standard;
            if (viewButtonText != null) viewButtonText.text = hideLabel;
        }
        else
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            if (viewButtonText != null) viewButtonText.text = viewLabel;
        }

        // Refresh the visual state
        passwordInput.ForceLabelUpdate();
        passwordInput.ActivateInputField();
    }

    void UpdateButtonLabel()
    {
        if (viewButtonText == null || passwordInput == null) return;

        var current = passwordInput.contentType;
        viewButtonText.text = (current == TMP_InputField.ContentType.Password || current == TMP_InputField.ContentType.Pin)
            ? viewLabel
            : hideLabel;
    }
}
