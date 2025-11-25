using UnityEngine;
using System.Collections;
using Firebase;
using Firebase.Auth;
using TMPro;

public class FirebaseAuthService : MonoBehaviour
{
    // Backing field for lazy singleton
    private static FirebaseAuthService _instance;

    // Lazy singleton property — will find or create the service if needed.
    public static FirebaseAuthService Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an existing instance in the scene
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
                    // Ensure the found instance persists across scenes
                    DontDestroyOnLoad(_instance.gameObject);
                }
            }
            return _instance;
        }
    }

    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;
    public FirebaseUser user;

    // New: expose last error for callers (LoginHandler) to read and show on local UI
    public string lastErrorMessage;

    bool isInitializing = false;

    // Public initializer you can call from UI or a scene controller.
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

            // show diagnostic info on device/UI
            ShowInitializationDiagnostics();
        }
        else
        {
            Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);

            // still show diagnostic info so you can see why init failed on device
            ShowInitializationDiagnostics();
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

    // Small diagnostic routine: prints Firebase init status to Debug.Log and UI (if UIManager available).
    private void ShowInitializationDiagnostics(float displaySeconds = 5f)
    {
        string uid = auth?.CurrentUser?.UserId ?? "null";
        string authSet = (auth != null).ToString();
        string msg = $"Firebase diag: dependency={dependencyStatus}, authSet={authSet}, currentUserUid={uid}";

        Debug.Log(msg);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage(msg, displaySeconds);
        }
    }

    // Public wrapper to start sign-in (handles initialization).
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

            // Record the last error so callers (LoginHandler) can display it in their UI
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
            // success -> clear lastErrorMessage and set user
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

    // Public wrapper to start registration (handles initialization).
    public IEnumerator Register(string name, string email, string password, string confirmPassword)
    {
        return EnsureInitializedThen(RegisterAsync(name, email, password, confirmPassword));
    }

    private IEnumerator RegisterAsync(string name, string email, string password, string confirmPassword)
    {
        // Existing registration logic unchanged but you may want to set lastErrorMessage on failures similarly.
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
            // set lastErrorMessage here if desired
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
