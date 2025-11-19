using UnityEngine;


/// <summary>
/// Opens the admin login panel when the admin button is clicked.
/// </summary>

public class AdminButton : MonoBehaviour
{
    //panels
    public GameObject homePanel;
    public GameObject loginPanel;
    public GameObject bottomNavPanel;



    //Open log in panel
    public void OpenLoginPanel()
    {
        loginPanel.SetActive(true);
        homePanel.SetActive(false);
        bottomNavPanel.SetActive(false);
    }
}
