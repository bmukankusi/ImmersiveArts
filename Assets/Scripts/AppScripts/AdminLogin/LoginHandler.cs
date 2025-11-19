using System;
using UnityEngine;
using TMPro;
using System.Collections;
using Firebase;


/// <summary>
/// Manages the login process for admin users using Firebase Authentication.
/// </summary>
/// <remarks> Uses FirebaseAuthService to authenticate users and provides UI feedback during the login process.</remarks>
public class LoginHandler : MonoBehaviour
{
    public TMP_InputField emailInputField;
    public TMP_InputField passwordInputField;

    [Header("UI")]
    public TMP_Text statusText;

    [Header("Admin")]
    public GameObject adminGroupParent;

    [Header("Optional: show logged-in info")]
    public TMP_InputField displayNameField;
    public TMP_InputField displayEmailField;

    void Start()
    {
        // Ensure the service exists or initialize it
        if (FirebaseAuthService.Instance == null)
        {
            Debug.Log("FirebaseAuthService not present in scene. Create a GameObject with FirebaseAuthService attached.");
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Authentication service missing";
            }
        }
        else
        {
            FirebaseAuthService.Instance.Initialize();
        }

        // hide status text initially
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
            statusText.text = string.Empty;
        }

        if (adminGroupParent != null)
            adminGroupParent.SetActive(false);
    }

    public void OnLoginButton()
    {
        if (emailInputField == null || passwordInputField == null)
        {
            Debug.LogError("Login input fields not set.");
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Internal error: input fields not configured";
            }
            return;
        }

        var email = emailInputField.text?.Trim() ?? string.Empty;
        var password = passwordInputField.text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Please enter email and password.";
            }
            return;
        }

        if (FirebaseAuthService.Instance == null)
        {
            Debug.LogError("FirebaseAuthService instance missing.");
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Authentication service not found";
            }
            return;
        }

        // Start the login flow that shows text feedback and guarantees at least 2s "Logging in..." display.
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "Logging in...";
        }

        StartCoroutine(HandleLogin(email, password));
    }

    private IEnumerator HandleLogin(string email, string password)
    {
        bool signInCompleted = false;

        IEnumerator SignInWrapper()
        {
            yield return StartCoroutine(FirebaseAuthService.Instance.SignIn(email, password));
            signInCompleted = true;
        }

        StartCoroutine(SignInWrapper());

        // Ensure "Logging in..." is visible for at least 2 seconds and wait for signin to finish
        float timer = 0f;
        while (!signInCompleted || timer < 2f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        var service = FirebaseAuthService.Instance;

        bool loginSuccess = service != null
                            && service.user != null
                            && !service.user.IsAnonymous
                            && !string.IsNullOrEmpty(service.user.Email)
                            && string.Equals(service.user.Email, email, StringComparison.OrdinalIgnoreCase);

        if (loginSuccess)
        {
            if (statusText != null)
            {
                statusText.text = service.user.DisplayName != null
                    ? $"Welcome {service.user.DisplayName}"
                    : $"Welcome {service.user.Email}";
            }

            // set placeholders / visible texts with the logged in user's info_admin settings panel
            string displayName = !string.IsNullOrEmpty(service.user.DisplayName) ? service.user.DisplayName : service.user.Email;
            if (displayNameField != null)
            {
                var ph = displayNameField.placeholder as TMP_Text;
                if (ph != null) ph.text = displayName;
                else displayNameField.text = displayName; 
            }

            if (displayEmailField != null)
            {
                var ph = displayEmailField.placeholder as TMP_Text;
                if (ph != null) ph.text = service.user.Email ?? string.Empty;
                else displayEmailField.text = service.user.Email ?? string.Empty;
            }

            if (adminGroupParent != null) adminGroupParent.SetActive(true);

            yield return new WaitForSeconds(1.5f);
            if (statusText != null) statusText.gameObject.SetActive(false);
        }
        else
        {
            string message = "Login failed. Please check your email and password.";
            if (service != null && !string.IsNullOrEmpty(service.lastErrorMessage))
            {
                message = service.lastErrorMessage;
            }
            else if (service != null && service.dependencyStatus != DependencyStatus.Available)
            {
                message = $"Firebase dependency error: {service.dependencyStatus}";
            }
            else if (service != null && service.user != null && service.user.IsAnonymous)
            {
                message = "Login failed: anonymous session detected. Please sign in with your registered account.";
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = message;
            }
            else
            {
                Debug.LogError(message);
            }
        }
    }
}
