using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages button interactions and scene transitions in the application, including displaying informational panels and
/// handling navigation between scenes.
/// </summary>
/// <remarks>This class is designed to be used in Unity projects and provides functionality for managing scene
/// transitions, showing and hiding informational panels, and refreshing the AR scene. It assumes that the scene names
/// for the home and AR scenes are configured, and that an informational panel GameObject is assigned.</remarks>

public class ButtonsManager : MonoBehaviour
{
  
    public string homeSceneName = "App Scene";
    public string arSceneName = "AR Scene";
    public GameObject infoPanel;
    public float infoDisplayDuration = 3f;

    void Start()
    {
        var activeSceneName = SceneManager.GetActiveScene().name;
        if (activeSceneName == arSceneName)
        {
            if (infoPanel != null)
            {
                infoPanel.SetActive(true);
                StartCoroutine(HideInfoPanelAfterDelay(infoDisplayDuration));
            }
            else
            {
                Debug.LogWarning("ButtonsManager: infoPanel is not assigned but this is the AR scene.");
            }
        }
        else
        {
            // Ensure the panel is hidden in other scenes
            if (infoPanel != null)
                infoPanel.SetActive(false);
        }
    }

    private IEnumerator HideInfoPanelAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        CloseInfoPanel();
    }

    // Called by the back button to go back to App Scene
    public void GoToHomeScene()
    {
        if (string.IsNullOrEmpty(homeSceneName))
        {
            Debug.LogWarning("ButtonsManager: homeSceneName is empty. Cannot load home scene.");
            return;
        }
        SceneManager.LoadScene(homeSceneName);
    }

    // Called by a button to show the informational panel
    public void ShowInfoPanel()
    {
        if (infoPanel == null)
        {
            Debug.LogWarning("ButtonsManager: infoPanel is not assigned.");
            return;
        }
        infoPanel.SetActive(true);
    }

    // Called by the "Ok" button inside the informational panel to close it
    public void CloseInfoPanel()
    {
        if (infoPanel == null)
        {
            Debug.LogWarning("ButtonsManager: infoPanel is not assigned.");
            return;
        }
        infoPanel.SetActive(false);
    }

    // Called by the "Refresh" button to refresh the AR scene (reloads scene)
    public void RefreshARScene()
    {
        string sceneToLoad = string.IsNullOrEmpty(arSceneName) ? SceneManager.GetActiveScene().name : arSceneName;
        SceneManager.LoadScene(sceneToLoad);
    }
}
