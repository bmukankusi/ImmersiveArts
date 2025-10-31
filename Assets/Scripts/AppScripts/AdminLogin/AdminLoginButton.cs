using UnityEngine;
using UnityEngine.UI;

public class AdminLoginButton : MonoBehaviour
{
    public GameObject loginPanel;
    public GameObject bottomMenuPanel;
    private Button adminLoginButton;


    public void OpenLoginPanel()
    {
        loginPanel.SetActive(true);
        bottomMenuPanel.SetActive(false);
    }
}
