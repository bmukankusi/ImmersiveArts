using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonsManager : MonoBehaviour
{
  
    public string homeSceneName = "App Scene";
    public string arSceneName = "AR Scene";
    public GameObject infoPanel;
    public float infoDisplayDuration = 3f; // Duration to show info panel on AR scene load

    void Start()
    {
        // If this is the configured AR scene, show the info panel for the configured duration.
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

    // Called by the "back" button to go back to App Scene
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
