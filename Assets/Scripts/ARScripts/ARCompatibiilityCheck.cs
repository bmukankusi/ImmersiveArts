using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARCompatibiilityCheck : MonoBehaviour
{
    [Header("UI (notification / confirmation)")]
    public GameObject compatibilityPanel;    
    public TMP_Text statusText;             
    public Button closeButton;              
    public Button proceedButton;            

    [Header("AR launch")]
    public string arSceneName = "ARScene";   
    public bool autoLoadOnSuccess = true;   

    // Optional timeout for long operations
    public float checkTimeoutSeconds = 30f;

    void Awake()
    {
        if (compatibilityPanel != null) compatibilityPanel.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (proceedButton != null)
        {
            proceedButton.onClick.RemoveAllListeners();
            proceedButton.onClick.AddListener(OnProceedButton);
            proceedButton.interactable = false;
        }
    }

    // Call this from your AR Start / AR Button onClick
    public void StartARCompatibilityCheck()
    {
        if (compatibilityPanel != null) compatibilityPanel.SetActive(true);
        if (statusText != null) statusText.text = "Preparing compatibility check...";
        if (proceedButton != null) proceedButton.interactable = false;

        StartCoroutine(CheckCompatibilityCoroutine());
    }

    IEnumerator CheckCompatibilityCoroutine()
    {
        float startTime = Time.realtimeSinceStartup;

        // Camera permission
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            if (statusText != null) statusText.text = "Requesting camera permission...";
            var request = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            yield return request;

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                if (statusText != null) statusText.text = "Camera permission denied. AR requires camera access.";
                yield break;
            }
        }

        // AR Foundation availability check
        if (statusText != null) statusText.text = "Checking AR support on this device...";

        // This call updates ARSession.state to indicate availability.
        yield return ARSession.CheckAvailability();

        // If CheckAvailability took too long, bail out
        if (Time.realtimeSinceStartup - startTime > checkTimeoutSeconds)
        {
            if (statusText != null) statusText.text = "Compatibility check timed out.";
            yield break;
        }

        ARSessionState state = ARSession.state;

        if (state == ARSessionState.Unsupported)
        {
            if (statusText != null) statusText.text = "AR is not supported on this device.";
            yield break;
        }

        if (state == ARSessionState.NeedsInstall)
        {
            if (statusText != null) statusText.text = "AR software needs to be installed. Installing...";
            yield return ARSession.Install();

            // Re-check state after install attempt
            if (Time.realtimeSinceStartup - startTime > checkTimeoutSeconds)
            {
                if (statusText != null) statusText.text = "Compatibility check timed out during install.";
                yield break;
            }

            state = ARSession.state;
            if (state == ARSessionState.Unsupported || state == ARSessionState.NeedsInstall)
            {
                if (statusText != null) statusText.text = "Failed to install required AR software.";
                yield break;
            }
        }

        // Finally check if available/running/ready
        if (state == ARSessionState.Ready || state == ARSessionState.SessionInitializing || state == ARSessionState.SessionTracking || state == ARSessionState.CheckingAvailability || state == ARSessionState.None)
        {
            // Consider this available (ARSession.state may vary by platform/version)
            if (statusText != null) statusText.text = "Device is compatible with AR.";

            if (proceedButton != null) proceedButton.interactable = true;

            if (autoLoadOnSuccess && !string.IsNullOrEmpty(arSceneName))
            {
                // Small delay to allow user to read the success message
                yield return new WaitForSeconds(0.35f);

                // Optionally start the ARSession then load scene
                // ARSession.Reset(); // optional: reset session state before loading AR scene

                // Load the AR scene
                SceneManager.LoadScene(arSceneName);
            }

            yield break;
        }

        if (statusText != null) statusText.text = $"AR not available (state: {state}).";
    }

    void OnProceedButton()
    {
        // Record device proceed to analytics (store under Galleries collection)
        if (SaveDataToFirestore.Instance != null)
        {
            SaveDataToFirestore.Instance.RecordProceed("NP Art gallery");
        }
        else
        {
            Debug.LogWarning("[ARCompatibiilityCheck] SaveDataToFirestore.Instance not found. Proceed not recorded.");
        }

        // Called when user presses the proceed button after a successful check
        if (!string.IsNullOrEmpty(arSceneName))
            SceneManager.LoadScene(arSceneName);
        ClosePanel();
    }

    public void ClosePanel()
    {
        if (compatibilityPanel != null) compatibilityPanel.SetActive(false);

        if (proceedButton != null) proceedButton.onClick.RemoveAllListeners();
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
    }

    void OnDestroy()
    {
        // Clean up listeners if object destroyed
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
        if (proceedButton != null) proceedButton.onClick.RemoveAllListeners();
    }
}
