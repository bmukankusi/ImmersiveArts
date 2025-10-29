using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Database;
using TMPro;

public class AdminSettings : MonoBehaviour
{
    [Header("UI References (assign in Inspector)")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button saveButton;
    public TMP_Text feedbackText;

    [Header("Behavior")]
    public float feedbackDisplaySeconds = 2f;
    public int changeCooldownDays = 30;

    FirebaseAuth auth;
    DatabaseReference dbRoot;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        dbRoot = FirebaseDatabase.DefaultInstance.RootReference;
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);
    }

    // Called when user clicks save. Async to await Firebase calls.
    public async void OnSaveClicked()
    {
        var user = auth.CurrentUser;
        if (user == null)
        {
            StartCoroutine(ShowTemporaryFeedback("Not signed in.", true, feedbackDisplaySeconds));
            return;
        }

        // Keep emailInput for UI but ignore changes to email; only allow password changes.
        string newEmail = emailInput != null ? emailInput.text.Trim() : string.Empty;
        string newPassword = passwordInput != null ? passwordInput.text : string.Empty;

        // Only consider password changes now
        if (string.IsNullOrEmpty(newPassword))
        {
            StartCoroutine(ShowTemporaryFeedback("No changes to save. Only password changes are allowed.", true, feedbackDisplaySeconds));
            return;
        }

        string uid = user.UserId;
        try
        {
            // Read last change timestamp (stored as unix seconds) to enforce cooldown
            long lastChangeSeconds = 0;
            var snap = await FirebaseDatabase.DefaultInstance.GetReference($"users/{uid}/lastCredentialChangeSeconds").GetValueAsync();
            if (snap.Exists && long.TryParse(snap.Value?.ToString(), out var parsed))
                lastChangeSeconds = parsed;

            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var cooldownSeconds = (long)changeCooldownDays * 24 * 3600;
            if (lastChangeSeconds != 0 && nowSeconds - lastChangeSeconds < cooldownSeconds)
            {
                var remaining = TimeSpan.FromSeconds(cooldownSeconds - (nowSeconds - lastChangeSeconds));
                string friendly = $"You can change credentials again in {remaining.Days}d {remaining.Hours}h.";
                StartCoroutine(ShowTemporaryFeedback(friendly, true, feedbackDisplaySeconds));
                return;
            }

            bool anyChanged = false;

            // Only update password (email changes are not permitted here)
            if (!string.IsNullOrEmpty(newPassword))
            {
                await user.UpdatePasswordAsync(newPassword);
                anyChanged = true;
            }

            if (anyChanged)
            {
                // Save timestamp of change (client-side unix seconds). Adjust if you prefer server timestamp.
                await FirebaseDatabase.DefaultInstance.GetReference($"users/{uid}/lastCredentialChangeSeconds").SetValueAsync(nowSeconds);

                StartCoroutine(ShowTemporaryFeedback("Password saved.", false, feedbackDisplaySeconds));
                // Clear password input
                if (passwordInput != null) passwordInput.text = string.Empty;
            }
            else
            {
                StartCoroutine(ShowTemporaryFeedback("No changes applied.", true, feedbackDisplaySeconds));
            }
        }
        catch (Exception ex)
        {
            // Map common Firebase messages to friendly text when possible
            StartCoroutine(ShowTemporaryFeedback(MapFirebaseAuthError(ex), true, feedbackDisplaySeconds));
        }
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
        if (lower.Contains("requires-recent-login") || lower.Contains("recent login"))
            return "Please re-authenticate (sign in again) before changing sensitive information.";
        if (lower.Contains("network"))
            return "Network error. Check your connection and try again.";
        return msg;
    }
}
