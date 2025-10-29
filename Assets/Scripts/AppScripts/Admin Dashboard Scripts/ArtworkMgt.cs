using UnityEngine;
using UnityEngine.UI;

public class ArtworkMgt : MonoBehaviour
{
    [Header("Panels")]
    public GameObject viewArtworkPanel;
    public GameObject confirmDeleteArtworkPanel;


    //View artwork
    public void ShowViewArtworkPanel()
    {
        viewArtworkPanel?.SetActive(true);
        confirmDeleteArtworkPanel?.SetActive(false);
    }

    //Delete artwork panel
    public void ShowConfirmDeleteArtworkPanel()
    {
        viewArtworkPanel?.SetActive(false);
        confirmDeleteArtworkPanel?.SetActive(true);
    }

    // Close delete confirmation panel
    public void CloseConfirmDeleteArtworkPanel()
    {
        confirmDeleteArtworkPanel?.SetActive(false);
    }

    // Close view artwork panel
    public void CloseViewArtworkPanel()
    {
        viewArtworkPanel?.SetActive(false);
    }
}
