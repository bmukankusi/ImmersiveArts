using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Extensions;
using TMPro;

public class Login : MonoBehaviour
{
    [Header("UI References (assign in Inspector)")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button signInButton;
    public TMP_Text feedbackText;

    // Button + label used to toggle password visibility
    public Button viewPasswordButton;
    public TMP_Text viewPasswordButtonText;

    [Header("Panels")]
    public GameObject adminPanel; // assign the admin panel GameObject (inactive by default)
    public GameObject homePanel;
    public GameObject loginPanel;
    public GameObject navButtonsPanel;

    [Header("Behavior")]
    public float loggedInDisplayDuration = 2f;

    private FirebaseAuth auth;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;

        // Ensure UI references exist
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);

        if (adminPanel != null)
            adminPanel.SetActive(false);

        if (signInButton != null)
            signInButton.onClick.AddListener(OnSignInButtonClicked);

        // Wire the view/hide password button and initialize its label
        if (viewPasswordButton != null)
            viewPasswordButton.onClick.AddListener(OnTogglePasswordVisibility);

        if (viewPasswordButtonText != null)
            viewPasswordButtonText.text = "View";

        // Ensure password input starts in Password content type (hidden)
        if (passwordInput != null)
            passwordInput.contentType = TMP_InputField.ContentType.Password;

        navButtonsPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (signInButton != null)
            signInButton.onClick.RemoveListener(OnSignInButtonClicked);

        if (viewPasswordButton != null)
            viewPasswordButton.onClick.RemoveListener(OnTogglePasswordVisibility);
    }

    // Wire this to the Sign In button (or it will be wired automatically in Start)
    public void OnSignInButtonClicked()
    {
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        // Validate mandatory fields
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Email and password are required.", true);
            return;
        }

        if (signInButton != null)
            signInButton.interactable = false;

        ShowFeedback("Signing in...", false);

        auth.SignInWithEmailAndPasswordAsync(email, password)
            .ContinueWithOnMainThread(task =>
            {
                if (signInButton != null)
                    signInButton.interactable = true;

                if (task.IsCanceled)
                {
                    ShowFeedback("Sign-in canceled.", true);
                    return;
                }

                if (task.IsFaulted)
                {
                    // Prefer the first inner exception and map it to a friendly message
                    string rawMessage = "Sign-in failed.";
                    Exception root = null;
                    if (task.Exception != null)
                    {
                        var inner = task.Exception.Flatten().InnerExceptions;
                        if (inner != null && inner.Count > 0)
                            root = inner[0];
                        else
                            root = task.Exception;
                    }

                    if (root != null)
                        rawMessage = MapFirebaseAuthError(root);

                    ShowFeedback(rawMessage, true);
                    return;
                }

                // Success
                FirebaseUser newUser = task.Result.User;
                ShowFeedback("Logged In", false);
                StartCoroutine(OpenAdminPanelAfterDelay(loggedInDisplayDuration));
            });
    }

    // Map common Firebase auth exception messages to user-friendly strings
    private string MapFirebaseAuthError(Exception ex)
    {
        if (ex == null)
            return "Sign-in failed.";

        string msg = ex.Message ?? string.Empty;
        string lower = msg.ToLowerInvariant();

        if (lower.Contains("password is invalid") || lower.Contains("the password is invalid") || lower.Contains("invalid password") || lower.Contains("wrong password"))
            return "Incorrect password. Please try again.";
        if (lower.Contains("no user record") || lower.Contains("no user") && lower.Contains("record") || lower.Contains("user not found") || lower.Contains("there is no user"))
            return "No account found for that email.";
        if (lower.Contains("badly formatted") || lower.Contains("invalid email") || lower.Contains("email address is badly"))
            return "Invalid email address format.";
        if (lower.Contains("network") || lower.Contains("timeout") || lower.Contains("unreachable host"))
            return "Network error. Check your internet connection and try again.";
        if (lower.Contains("email already in use"))
            return "That email is already registered.";
        if (lower.Contains("user disabled"))
            return "This user account has been disabled.";
        if (lower.Contains("too many requests") || lower.Contains("too many attempts"))
            return "Too many attempts. Please wait and try again later.";

        // Fall back to the raw message if nothing matched
        return msg;
    }

    private IEnumerator OpenAdminPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Hide feedback
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);

        // Activate admin panel if assigned
        if (adminPanel != null)
        {
            adminPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Login: adminPanel is not assigned. You may want to load an admin scene instead.");
        }
    }

    private void ShowFeedback(string message, bool isError)
    {
        if (feedbackText == null)
        {
            Debug.Log(isError ? "Error: " + message : message);
            return;
        }

        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;
        feedbackText.gameObject.SetActive(true);
    }

    // Toggle password visibility when the View/Hide button is clicked
    public void OnTogglePasswordVisibility()
    {
        if (passwordInput == null)
            return;

        bool currentlyHidden = passwordInput.contentType == TMP_InputField.ContentType.Password;

        // Toggle between Password (hidden) and Standard (visible)
        passwordInput.contentType = currentlyHidden ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;

        // Force the input field to apply the new content type and keep focus
        passwordInput.ForceLabelUpdate();
        passwordInput.ActivateInputField();
        passwordInput.caretPosition = passwordInput.text.Length;

        // Update button label if provided
        if (viewPasswordButtonText != null)
            viewPasswordButtonText.text = currentlyHidden ? "Hide" : "View";
    }

    //Back to home panel
    public void ShowHomePanel()
    {
        homePanel.SetActive(true);
        loginPanel.SetActive(false);
        navButtonsPanel.SetActive(true);
    }
}
