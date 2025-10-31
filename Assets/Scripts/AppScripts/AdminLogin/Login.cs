using System;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using TMPro;

public class Login : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button signInButton;
    public Button viewButton;
    public TMP_Text viewButtonText;
    public TMP_Text feedbackText;

    [Header("Panels")]
    public Transform adminPanel;
    public Transform panelsParent;
    public GameObject topMenuPanel;
    public GameObject loginPanel;
    public GameObject bottmNavPanel;

    [Header("Behavior")]
    public float feedbackDisplaySeconds = 2f;

    FirebaseAuth auth;

    // Notifies other systems 
    public static event Action<FirebaseUser> SignedIn;

    /// <summary>
    /// Initializes the authentication instance and sets up event listeners for UI interactions.
    /// </summary>
    /// <remarks>This method configures the Firebase authentication instance and attaches event handlers  to
    /// the sign-in and password visibility buttons. It also ensures  that the feedback text UI
    /// element is hidden initially</remarks>

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;

        if (signInButton != null) signInButton.onClick.AddListener(OnSignInClicked);
        if (viewButton != null) viewButton.onClick.AddListener(TogglePasswordVisibility);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (signInButton != null) signInButton.onClick.RemoveListener(OnSignInClicked);
        if (viewButton != null) viewButton.onClick.RemoveListener(TogglePasswordVisibility);
    }

    public async void OnSignInClicked()
    {
        var email = emailInput != null ? emailInput.text.Trim() : string.Empty;
        var password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            StartCoroutine(ShowTemporaryFeedback("Email and password are required.", true, feedbackDisplaySeconds));
            return;
        }

        try
        {
            // Sign in with Firebase authentication
            await auth.SignInWithEmailAndPasswordAsync(email, password);

            var user = auth.CurrentUser;
            if (user == null)
            {
                StartCoroutine(ShowTemporaryFeedback("Sign-in failed.", true, feedbackDisplaySeconds));
                return;
            }

            StartCoroutine(ShowTemporaryFeedback("Signed in.", false, feedbackDisplaySeconds));

            try
            {
                SignedIn?.Invoke(user);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SignedIn event handler threw: {ex}");
            }

            // Activate panels
            
            StartCoroutine(ActivatePanelsSequence());
        }
        catch (Exception ex)
        {
            StartCoroutine(ShowTemporaryFeedback(MapFirebaseAuthError(ex), true, feedbackDisplaySeconds));
        }
    }

    IEnumerator ActivatePanelsSequence()
    {
        if (adminPanel == null)
        {
            Debug.LogWarning("Admin panel not assigned. Assign the adminPanel in the Login inspector to open it after sign-in.");
            yield break;
        }

        if (panelsParent != null)
            panelsParent.gameObject.SetActive(true);

        yield return null;

        // Hide all children under the parent
        if (panelsParent != null)
        {
            foreach (Transform t in panelsParent)
                t.gameObject.SetActive(false);
        }

        // 2) Enable the top menu panel
        if (topMenuPanel != null)
            topMenuPanel.SetActive(true);

        // Small wait to let UI settle 
        yield return null;

        // Enable the requested admin panel
        adminPanel.gameObject.SetActive(true);

        // Wait for end of frame then hide login UI
        yield return new WaitForEndOfFrame();

        // Hide login UI , otherwise disable this component's GameObject
        if (loginPanel != null)
            loginPanel.SetActive(false);
        else
            gameObject.SetActive(false);

        // Hide bottom navigation panel 
        if (bottmNavPanel != null)
            bottmNavPanel.SetActive(false);
    }

    // Toggle between showing and hiding password text
    void TogglePasswordVisibility()
    {
        if (passwordInput == null) return;

        var current = passwordInput.contentType;
        if (current == TMP_InputField.ContentType.Password || current == TMP_InputField.ContentType.Pin)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Standard;
            if (viewButtonText != null) viewButtonText.text = "Hide";
        }
        else
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            if (viewButtonText != null) viewButtonText.text = "View";
        }

        // Refresh the visual state
        passwordInput.ForceLabelUpdate();
        passwordInput.ActivateInputField();
    }

    private System.Collections.IEnumerator ShowTemporaryFeedback(string message, bool isError, float duration)
    {
        if (feedbackText == null)
        {
            Debug.Log(isError ? "Error: " + message : message);
            yield break;
        }

        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;
        feedbackText.gameObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        feedbackText.gameObject.SetActive(false);
    }

    // error mapping 
    private string MapFirebaseAuthError(Exception ex)
    {
        if (ex == null) return "An unknown error occurred.";
        var msg = ex.Message ?? ex.ToString();
        var lower = msg.ToLowerInvariant();
        if (lower.Contains("invalid email") || lower.Contains("badly formatted") || lower.Contains("email address is badly"))
            return "Invalid email address format.";
        if (lower.Contains("password is invalid") || lower.Contains("wrong password") || lower.Contains("invalid password"))
            return "Incorrect password.";
        if (lower.Contains("user not found") || lower.Contains("no user record"))
            return "User not found.";
        if (lower.Contains("network"))
            return "Network error. Check your connection and try again.";
        return msg;
    }
}