using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PasswordViewToggle : MonoBehaviour
{
    public TMP_InputField passwordInput;
    public Button viewButton;
    public TMP_Text viewButtonText;

    public string viewLabel = "View";
    public string hideLabel = "Hide";

    void OnEnable()
    {
        if (viewButton != null)
        {
            viewButton.onClick.RemoveListener(TogglePasswordVisibility);
            viewButton.onClick.AddListener(TogglePasswordVisibility);
        }
        UpdateButtonLabel();
    }

    void OnDisable()
    {
        if (viewButton != null)
            viewButton.onClick.RemoveListener(TogglePasswordVisibility);
    }

    public void TogglePasswordVisibility()
    {
        if (passwordInput == null) return;

        bool currentlyMasked = passwordInput.contentType == TMP_InputField.ContentType.Password
                             || passwordInput.contentType == TMP_InputField.ContentType.Pin;

        passwordInput.contentType = currentlyMasked
            ? TMP_InputField.ContentType.Standard
            : TMP_InputField.ContentType.Password;

        if (viewButtonText != null)
            viewButtonText.text = currentlyMasked ? hideLabel : viewLabel;

        passwordInput.ForceLabelUpdate();
        if (passwordInput.textComponent != null)
            passwordInput.textComponent.ForceMeshUpdate();

        int caret = Mathf.Clamp(passwordInput.caretPosition, 0, (passwordInput.text ?? "").Length);
        passwordInput.Select();
        passwordInput.caretPosition = caret;
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
