using UnityEngine;
using Firebase.Auth;

public class AdminLogout : MonoBehaviour
{
    [Header("Navigation")]
    public AppNavigation appNavigation;   
    public GameObject homePanel;          

    [Header("Admin UI")]
    public GameObject adminRootPanel;     
    public GameObject navButtonsPanel;    

    /// <summary>
    /// Call from a Button OnClick to sign out the admin and return to the home panel.
    /// Also enables `navButtonsPanel` 
    /// </summary>
    public void Logout()
    {
        // Attempt Firebase sign-out
        try
        {
            var auth = FirebaseAuth.DefaultInstance;
            if (auth.CurrentUser != null)
            {
                auth.SignOut();
                Debug.Log("AdminLogout: Signed out successfully.");
            }
            else
            {
                Debug.Log("AdminLogout: No user signed in.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("AdminLogout: SignOut failed: " + ex.Message);
        }

        // Hide admin UI 
        if (adminRootPanel != null)
            adminRootPanel.SetActive(false);

        // Navigate to home using AppNavigation
        if (appNavigation != null)
        {
            appNavigation.ShowHomePanel();

            if (navButtonsPanel != null)
                navButtonsPanel.SetActive(true);

            return;
        }

        // directly activate homePanel
        if (homePanel != null)
        {
            homePanel.SetActive(true);

            if (navButtonsPanel != null)
                navButtonsPanel.SetActive(true);

            return;
        }

    }
}
