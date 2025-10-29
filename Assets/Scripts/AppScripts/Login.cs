using System;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Database;
using TMPro;

public class Login : MonoBehaviour
{
    [Header("UI (assign in Inspector)")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button signInButton;
    public Button viewButton;
    public TMP_Text viewButtonText; // optional: text on the view/hide button
    public TMP_Text feedbackText;

    [Header("Panels")]
    public Transform panelsParent; // parent that contains gallery admin panels (child names should match galleryId)

    [Header("Behavior")]
    public float feedbackDisplaySeconds = 2f;

    FirebaseAuth auth;

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
            // Sign in with Firebase Auth
            await auth.SignInWithEmailAndPasswordAsync(email, password);

            var user = auth.CurrentUser;
            if (user == null)
            {
                StartCoroutine(ShowTemporaryFeedback("Sign-in failed.", true, feedbackDisplaySeconds));
                return;
            }

            StartCoroutine(ShowTemporaryFeedback("Signed in.", false, feedbackDisplaySeconds));

            // Admin panel / analytics loading is disabled until backend data exists.
            // To re-enable later, uncomment the following line:
            _ = LoadGalleryAndAnalyticsForUserAsync(user);
        }
        catch (Exception ex)
        {
            StartCoroutine(ShowTemporaryFeedback(MapFirebaseAuthError(ex), true, feedbackDisplaySeconds));
        }
    }

    async Task LoadGalleryAndAnalyticsForUserAsync(FirebaseUser user)
    {
        
        // Commented out admin panel / analytics loading while no stored data exists.
        try
        {
            string uid = user.UserId;

            // Expecting user's assigned gallery id stored at users/{uid}/galleryId
            var gallerySnap = await FirebaseDatabase.DefaultInstance.GetReference($"users/{uid}/galleryId").GetValueAsync();
            if (gallerySnap == null || !gallerySnap.Exists)
            {
                StartCoroutine(ShowTemporaryFeedback("No gallery assigned to this user.", true, feedbackDisplaySeconds));
                return;
            }

            string galleryId = gallerySnap.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(galleryId))
            {
                StartCoroutine(ShowTemporaryFeedback("Invalid gallery id.", true, feedbackDisplaySeconds));
                return;
            }

            // Fetch analytics stored under analytics/{galleryId}/...
            var analyticsRef = FirebaseDatabase.DefaultInstance.GetReference($"analytics/{galleryId}");
            var analyticsSnap = await analyticsRef.GetValueAsync();

            int totalDailyScans = 0;
            double averageUsers = 0.0;
            int monthlyInteractingUsers = 0;

            if (analyticsSnap != null && analyticsSnap.Exists)
            {
                // safe parsing for expected fields
                if (analyticsSnap.Child("totalDailyScans")?.Exists == true)
                    int.TryParse(analyticsSnap.Child("totalDailyScans").Value?.ToString(), out totalDailyScans);

                if (analyticsSnap.Child("averageUsers")?.Exists == true)
                    double.TryParse(analyticsSnap.Child("averageUsers").Value?.ToString(), out averageUsers);

                if (analyticsSnap.Child("monthlyInteractingUsers")?.Exists == true)
                    int.TryParse(analyticsSnap.Child("monthlyInteractingUsers").Value?.ToString(), out monthlyInteractingUsers);
            }

            // Persist analytics for other scripts to read (or use another IPC mechanism)
            PlayerPrefs.SetInt($"analytics_{galleryId}_totalDailyScans", totalDailyScans);
            PlayerPrefs.SetFloat($"analytics_{galleryId}_averageUsers", (float)averageUsers);
            PlayerPrefs.SetInt($"analytics_{galleryId}_monthlyInteractingUsers", monthlyInteractingUsers);
            PlayerPrefs.Save();

            // Activate the corresponding panel (child name must match galleryId)
            if (panelsParent != null)
            {
                var panel = panelsParent.Find(galleryId);
                if (panel != null)
                {
                    // hide others first
                    foreach (Transform t in panelsParent) t.gameObject.SetActive(false);
                    panel.gameObject.SetActive(true);
                }
                else
                {
                    StartCoroutine(ShowTemporaryFeedback("Gallery admin panel not found.", true, feedbackDisplaySeconds));
                }
            }
        }
        catch (Exception ex)
        {
            StartCoroutine(ShowTemporaryFeedback("Failed to load analytics: " + ex.Message, true, feedbackDisplaySeconds));
        }
        

        // Keep method valid while disabled:
        await Task.CompletedTask;
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

    // Minimal error mapping — extend as needed
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