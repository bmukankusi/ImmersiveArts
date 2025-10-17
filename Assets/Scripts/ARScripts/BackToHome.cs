using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BackToHome : MonoBehaviour
{
 
    // Name of the home scene
    public string homeSceneName = "App Scene";

    // Go to home scene by clicking a button

    public void GoToHomeScene()
    {
        // Load the home scene
        SceneManager.LoadScene(homeSceneName);
    }
}
