using UnityEngine;

public class BackToHomeFromLoginPage : MonoBehaviour
{

    /// <summary>
    /// Goes back to the home panel from the login panel.
    /// </summary>
    public GameObject homePanel;
    public GameObject bottomMenuPanel;
    public GameObject loginPanel;

    public void BackToHome()
    {
        homePanel.SetActive(true);
        bottomMenuPanel.SetActive(true);
        loginPanel.SetActive(false);
    }
}
