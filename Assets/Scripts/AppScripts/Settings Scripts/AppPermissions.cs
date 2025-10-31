using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// - Assign Button for each permission in the inspector.
/// - Assign On / Off sprites
/// - Android: requests runtime permissions.
/// - iOS: camera uses Unity's WebCam authorization; storage uses app sandbox (no runtime request).
/// Clicking when permission is granted opens app settings so the user can revoke access.
/// </summary>
public class AppPermissions : MonoBehaviour
{
    [Header("Camera")]
    public Button cameraButton;

    [Header("Storage")]
    public Button storageButton;

    [Header("Sprites")]
    public Sprite onSprite;
    public Sprite offSprite;

    void Start()
    {
        if (cameraButton != null) cameraButton.onClick.AddListener(OnCameraClicked);
        if (storageButton != null) storageButton.onClick.AddListener(OnStorageClicked);

        UpdateUI();
    }

    void OnDestroy()
    {
        if (cameraButton != null) cameraButton.onClick.RemoveListener(OnCameraClicked);
        if (storageButton != null) storageButton.onClick.RemoveListener(OnStorageClicked);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) UpdateUI(); // refresh after returning from permission/settings
    }

    void UpdateUI()
    {
        SetButtonSprite(cameraButton, IsCameraGranted() ? onSprite : offSprite);
        SetButtonSprite(storageButton, IsStorageGranted() ? onSprite : offSprite);
    }

    Image GetButtonImage(Button btn)
    {
        if (btn == null) return null;
        if (btn.image != null) return btn.image;
        return btn.GetComponentInChildren<Image>();
    }

    void SetButtonSprite(Button btn, Sprite sprite)
    {
        var img = GetButtonImage(btn);
        if (img != null)
            img.sprite = sprite;
    }

    bool IsCameraGranted()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
        if (Application.isEditor) return true;
        return Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
    }

    bool IsStorageGranted()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Treat either read or write as granted
        return Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite) ||
               Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead);
#else
        // iOS and other platforms: app sandbox allows writing to persistentDataPath,
        // so no explicit runtime permission is required for downloaded content.
        return true;
#endif
    }

    void OnCameraClicked()
    {
        if (IsCameraGranted())
        {
            OpenAppSettings();
        }
        else
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            StartCoroutine(RequestAndroidPermissionAndRefresh(Permission.Camera));
#else
            StartCoroutine(RequestWebCamPermissionAndRefresh());
#endif
        }
    }

    void OnStorageClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsStorageGranted())
        {
            OpenAppSettings();
        }
        else
        {
            // Request write permission (will usually prompt for both read/write)
            StartCoroutine(RequestAndroidPermissionAndRefresh(Permission.ExternalStorageWrite));
        }
#else
        // iOS / Editor: no runtime storage permission required - just refresh UI
        UpdateUI();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator RequestAndroidPermissionAndRefresh(string permission)
    {
        bool before = Permission.HasUserAuthorizedPermission(permission);
        Permission.RequestUserPermission(permission);

        // brief wait for OS dialog; poll for change for up to 5 seconds
        float timeout = 5f;
        float t = 0f;
        while (t < timeout)
        {
            yield return null;
            t += Time.deltaTime;
            if (Permission.HasUserAuthorizedPermission(permission) != before) break;
        }

        yield return null;
        UpdateUI();
    }
#endif

    IEnumerator RequestWebCamPermissionAndRefresh()
    {
        // Works on iOS/Editor, requests webcam authorization
        var req = Application.RequestUserAuthorization(UserAuthorization.WebCam);
        yield return req;
        UpdateUI();
    }

    void OpenAppSettings()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var uriClass = new AndroidJavaClass("android.net.Uri");

            AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS");
            AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier);
            intent.Call<AndroidJavaObject>("setData", uri);
            currentActivity.Call("startActivity", intent);
        }
        catch { /* best-effort */ }
#elif UNITY_IOS && !UNITY_EDITOR
        Application.OpenURL("app-settings:");
#else
        Debug.Log("OpenAppSettings: open OS settings to change permissions.");
#endif
    }
}
