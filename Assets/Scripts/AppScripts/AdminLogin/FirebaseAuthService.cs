using UnityEngine;
using System.Collections;
using Firebase;
using Firebase.Auth;
using TMPro;


/// <summary>
///  Provides authentication services using Firebase, including user sign in, registration, and state management.
/// </summary>
/// <remarks>This service is implemented as a singleton and ensures that Firebase dependencies are initialized
/// before performing authentication operations. It manages the current user's authentication state and provides methods
/// for signing in and registering users.</remarks>

public class FirebaseAuthService : MonoBehaviour
{
    private static FirebaseAuthService _instance;

    public static FirebaseAuthService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<FirebaseAuthService>();
                if (_instance == null)
                {
                    // Create a new GameObject and attach the service
                    var go = new GameObject(nameof(FirebaseAuthService));
                    _instance = go.AddComponent<FirebaseAuthService>();
                    DontDestroyOnLoad(go);
                }
                else
                {
                    DontDestroyOnLoad(_instance.gameObject);
                }
            }
            return _instance;
        }
    }

    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;
    public FirebaseUser user;

    // expose last error for callers (LoginHandler) to read and show on local UI
    public string lastErrorMessage;

    bool isInitializing = false;

    // Public initializer 
    public void Initialize()
    {
        if (isInitializing || IsInitialized()) return;
        StartCoroutine(InitializeCoroutine());
    }

    private IEnumerator InitializeCoroutine()
    {
        isInitializing = true;

        var depTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => depTask.IsCompleted);

        dependencyStatus = depTask.Result;
        if (dependencyStatus == DependencyStatus.Available)
        {
            auth = FirebaseAuth.DefaultInstance;
            auth.StateChanged += AuthStateChanged;
            AuthStateChanged(this, null);
            Debug.Log("Firebase initialized.");
        }
        else
        {
            Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
        }

        isInitializing = false;
    }

    bool IsInitialized()
    {
        return auth != null && dependencyStatus == DependencyStatus.Available;
    }

    private IEnumerator EnsureInitializedThen(IEnumerator action)
    {
        if (!IsInitialized())
        {
            Initialize();
            yield return new WaitUntil(() => !isInitializing && IsInitialized());
        }

        yield return StartCoroutine(action);
    }

    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (auth == null) return;

        if (auth.CurrentUser != user)
        {
            bool signedIn = user != auth.CurrentUser && auth.CurrentUser != null;
            if (!signedIn && user != null)
            {
                Debug.Log("Signed out " + user.UserId);
            }
            user = auth.CurrentUser;
            if (signedIn)
            {
                Debug.Log("Signed in " + user.UserId);
            }
        }
    }

    // Public wrapper to start sign in/handles initialization
    public IEnumerator SignIn(string email, string password)
    {
        return EnsureInitializedThen(SignInAsync(email, password));
    }

    private IEnumerator SignInAsync(string email, string password)
    {
        // Clear previous error
        lastErrorMessage = null;

        var loginTask = auth.SignInWithEmailAndPasswordAsync(email, password);
        yield return new WaitUntil(() => loginTask.IsCompleted);

        if (loginTask.Exception != null)
        {
            Debug.LogError(loginTask.Exception);

            FirebaseException firebaseException = loginTask.Exception.GetBaseException() as FirebaseException;
            AuthError authError = (AuthError)firebaseException?.ErrorCode;

            string failedMessage = "Login failed. ";

            switch (authError)
            {
                case AuthError.InvalidEmail:
                    failedMessage += "Email is invalid.";
                    break;
                case AuthError.WrongPassword:
                    failedMessage += "Wrong password.";
                    break;
                case AuthError.MissingEmail:
                    failedMessage += "Email is missing.";
                    break;
                case AuthError.MissingPassword:
                    failedMessage += "Password is missing.";
                    break;
                case AuthError.UserNotFound:
                    failedMessage += "User not found.";
                    break;
                default:
                    failedMessage += "Please check your credentials.";
                    break;
            }

            lastErrorMessage = failedMessage;

            // Keep user null on failure to avoid treating anonymous/previous session as success
            user = null;

            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage(failedMessage);
            else
                Debug.LogError(failedMessage);
        }
        else
        {
            // success : clear last error msg and set user
            lastErrorMessage = null;
            user = loginTask.Result.User;

            string welcome = user != null ? $"Welcome {user.DisplayName ?? user.Email}" : "You are successfully logged in";
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage(welcome);
                UIManager.Instance.OpenDashboardPanel();
            }
            else
            {
                Debug.LogFormat("{0}You are successfully logged in", user?.DisplayName);
            }
        }
    }

    // Public wrapper to start registration
    public IEnumerator Register(string name, string email, string password, string confirmPassword)
    {
        return EnsureInitializedThen(RegisterAsync(name, email, password, confirmPassword));
    }

    private IEnumerator RegisterAsync(string name, string email, string password, string confirmPassword)
    {
        if (string.IsNullOrEmpty(name))
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowMessage("User Name is empty");
            else Debug.LogError("User Name is empty");
            yield break;
        }
        if (string.IsNullOrEmpty(email))
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowMessage("Email field is empty");
            else Debug.LogError("email field is empty");
            yield break;
        }
        if (password != confirmPassword)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowMessage("Password does not match");
            else Debug.LogError("Password does not match");
            yield break;
        }

        var registerTask = auth.CreateUserWithEmailAndPasswordAsync(email, password);
        yield return new WaitUntil(() => registerTask.IsCompleted);

        if (registerTask.Exception != null)
        {
            Debug.LogError(registerTask.Exception);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("Registration failed.");
            else
                Debug.LogError("Registration failed.");
        }
        else
        {
            user = registerTask.Result.User;
            UserProfile userProfile = new UserProfile { DisplayName = name };
            var updateProfileTask = user.UpdateUserProfileAsync(userProfile);
            yield return new WaitUntil(() => updateProfileTask.IsCompleted);

            if (updateProfileTask.Exception != null)
            {
                user.DeleteAsync();
                Debug.LogError(updateProfileTask.Exception);
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage("Profile update failed.");
                else
                    Debug.LogError("Profile update failed.");
            }
            else
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage($"Registration Successful. Welcome {user.DisplayName}");
                else
                    Debug.Log("Registration Successful Welcome " + user.DisplayName);

                if (UIManager.Instance != null) UIManager.Instance.OpenLoginPanel();
            }
        }
    }
}
