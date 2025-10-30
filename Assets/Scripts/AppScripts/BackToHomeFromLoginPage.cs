using UnityEngine;

public class BackToHomeFromLoginPage : MonoBehaviour
{
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
