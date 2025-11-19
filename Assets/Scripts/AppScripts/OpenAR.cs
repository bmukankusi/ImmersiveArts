using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class OpenAR : MonoBehaviour
{
    private const string AR_SCENE_NAME = "AR Scene";

    /// <summary>
    /// Initiates the AR scene load after ensuring the AnalyticsManager is ready.
    /// This logs the start of a user's session with the AR environment.
    /// </summary>
    public async void OpenARScene()
    {
        // 1. Ensure Analytics Manager is initialized before proceeding
        if (AnalyticsManager.Instance != null)
        {
            Debug.Log("Waiting for Analytics Manager to initialize...");
            // Wait up to 5 seconds for Firebase/Auth to be ready
            bool isReady = await AnalyticsManager.Instance.WaitForInitializationAsync(5000);

            if (isReady)
            {
                Debug.Log("Analytics Manager ready. Logging scene transition.");

                // 2. Log a custom interaction event: The user has entered the AR Scene.
                // We use a duration of 0 since it's an immediate event.
                AnalyticsManager.Instance.LogInteraction(
                    artworkId: null,
                    artworkName: AR_SCENE_NAME,
                    duration: 0f,
                    interactionType: "scene_start"
                );
            }
            else
            {
                Debug.LogWarning("Analytics Manager failed to initialize. Proceeding without logging scene start.");
            }
        }

        // 3. Load the AR scene
        SceneManager.LoadScene(AR_SCENE_NAME);
    }
}