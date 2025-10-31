using UnityEngine;
using Firebase.Auth;

public class Logout : MonoBehaviour
{
    [Header("Navigation and UI")]
    public AppNavigation appNavigation;  
    public GameObject homePanel;         
    public GameObject navButtonsPanel;    
    public GameObject adminGroupPanelsParent; // Parent object that contains all admin related panels 

    /// <summary>
    /// Call from a Button OnClick to sign out the user and return to Home.
    /// Ensures Home and navButtonsPanel are active and adminGroupPanelsParent is deactivated.
    /// </summary>
    public void LogoutUser()
    {
        // Attempt Firebase sign-out 
        try
        {
            var auth = FirebaseAuth.DefaultInstance;
            if (auth != null && auth.CurrentUser != null)
            {
                auth.SignOut();
                Debug.Log("Logout: Signed out successfully.");
            }
            else
            {
                Debug.Log("Logout: No user signed in or FirebaseAuth not initialized.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Logout: SignOut failed: " + ex.Message);
        }

        // Deactivate admin group panels
        if (adminGroupPanelsParent != null)
            adminGroupPanelsParent.SetActive(false);

        // Navigate to home using 
        if (appNavigation != null)
        {
            appNavigation.ShowHomePanel();
        }
        else if (homePanel != null) 
        {
            homePanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Logout: No home target assigned. Assign AppNavigation or HomePanel in the Inspector.");
        }

        // Ensure nav buttonsmenu is visible
        if (navButtonsPanel != null)
            navButtonsPanel.SetActive(true);
    }
}
