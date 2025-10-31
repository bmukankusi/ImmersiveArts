using System;
using System.Collections;
using System.Collections.Generic;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoadData : MonoBehaviour
{
    [Header("UI / Prefab")]
    public RectTransform contentParent;             
    public GameObject artworkPrefab;                
    public Sprite placeholderSprite;                

    [Header("Prefab child tags")]
    public string imageChildTag = "ArtworkImage";
    public string artworkNameTag = "ArtworkName";
    public string artistNameTag = "ArtistName";
    public string isActiveTag = "ArtworkIsActive";

    // Delete button and confirmation panel
    public string deleteButtonTag = "ArtworkDeleteButton";   
    public GameObject deleteConfirmPanel;                    
    public Button deleteConfirmYesButton;                    
    public Button deleteConfirmNoButton;                     

    public GameObject deleteHandlerTarget;    // target for delete message; if null, uses deleteConfirmPanel or this GameObject

    FirebaseFirestore db;

    // Track runtime-created sprites for cleanup
    private readonly List<Sprite> _runtimeSprites = new List<Sprite>();

    // Pending deletion info filled when delete button is pressed
    private string _pendingDeleteId;
    private GameObject _pendingDeleteItemGO;

    void OnEnable()
    {
        // Start loading artworks when the panel becomes active
        InitializeFirebaseAndLoad();

        // Hide confirm panel 
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    

    void InitializeFirebaseAndLoad()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(depTask =>
        {
            var depStatus = depTask.Result;
            if (depStatus != DependencyStatus.Available)
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {depStatus}");
                return;
            }

            db = FirebaseFirestore.DefaultInstance;
            LoadArtworks();
        });
    }

    void ClearContent()
    {
        if (contentParent == null) return;
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

        // Destroy any sprites created at runtime to avoid leaking GPU memory
        for (int i = _runtimeSprites.Count - 1; i >= 0; i--)
        {
            var s = _runtimeSprites[i];
            if (s != null)
                Destroy(s);
        }
        _runtimeSprites.Clear();
    }

    void LoadArtworks()
    {
        if (db == null)
        {
            Debug.LogError("Firestore not initialized.");
            return;
        }

        db.Collection("Artworks").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to get Artworks: " + task.Exception);
                return;
            }

            ClearContent();

            QuerySnapshot snapshot = task.Result;
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                try
                {
                    string name = doc.ContainsField("name") ? doc.GetValue<string>("name") : string.Empty;
                    string artist = doc.ContainsField("artist") ? doc.GetValue<string>("artist") : string.Empty;
                    bool isActive = doc.ContainsField("isActive") ? doc.GetValue<bool>("isActive") : false;
                    string imageUrl = doc.ContainsField("image") ? doc.GetValue<string>("image") : null;

                    if (artworkPrefab == null || contentParent == null)
                    {
                        Debug.LogWarning("artworkPrefab or contentParent is not set in inspector.");
                        continue;
                    }

                    GameObject go = Instantiate(artworkPrefab, contentParent);
                    go.name = $"Artwork_{doc.Id}";

                    // Find by tag 
                    Transform tName = FindChildWithTag(go.transform, artworkNameTag);
                    if (tName != null)
                    {
                        TMP_Text tmp = tName.GetComponent<TMP_Text>();
                        if (tmp != null) tmp.text = name;
                    }

                    Transform tArtist = FindChildWithTag(go.transform, artistNameTag);
                    if (tArtist != null)
                    {
                        TMP_Text tmp = tArtist.GetComponent<TMP_Text>();
                        if (tmp != null) tmp.text = artist;
                    }

                    Transform tActive = FindChildWithTag(go.transform, isActiveTag);
                    if (tActive != null)
                    {
                        TMP_Text tmp = tActive.GetComponent<TMP_Text>();
                        if (tmp != null) tmp.text = isActive ? "Active" : "Inactive";
                    }

                    // Image handling
                    Transform tImage = FindChildWithTag(go.transform, imageChildTag);
                    Image uiImage = null;
                    if (tImage != null)
                    {
                        uiImage = tImage.GetComponent<Image>();
                        if (uiImage != null && placeholderSprite != null)
                        {
                            uiImage.sprite = placeholderSprite;
                            uiImage.preserveAspect = true;
                        }
                    }

                    if (!string.IsNullOrEmpty(imageUrl) && uiImage != null)
                    {
                        StartCoroutine(DownloadImageRoutine(imageUrl, uiImage));
                    }

                    // Delete button handling
                    Transform tDeleteBtn = FindChildWithTag(go.transform, deleteButtonTag);
                    if (tDeleteBtn != null)
                    {
                        Button btn = tDeleteBtn.GetComponent<Button>();
                        if (btn != null)
                        {
                            // capture local vars for closure
                            string docIdLocal = doc.Id;
                            GameObject itemLocal = go;

                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => OpenDeleteConfirm(docIdLocal, itemLocal));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing artwork doc {doc.Id}: {ex}");
                }
            }
        });
    }

    // Open the confirmation panel and register confirm/cancel listeners
    void OpenDeleteConfirm(string docId, GameObject itemGO)
    {
        _pendingDeleteId = docId;
        _pendingDeleteItemGO = itemGO;

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(true);

        // Register listeners
        if (deleteConfirmYesButton != null)
        {
            deleteConfirmYesButton.onClick.RemoveAllListeners();
            deleteConfirmYesButton.onClick.AddListener(OnConfirmDelete);
        }

        if (deleteConfirmNoButton != null)
        {
            deleteConfirmNoButton.onClick.RemoveAllListeners();
            deleteConfirmNoButton.onClick.AddListener(CloseDeleteConfirm);
        }
    }

    void CloseDeleteConfirm()
    {
        _pendingDeleteId = null;
        _pendingDeleteItemGO = null;

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        if (deleteConfirmYesButton != null)
            deleteConfirmYesButton.onClick.RemoveAllListeners();

        if (deleteConfirmNoButton != null)
            deleteConfirmNoButton.onClick.RemoveAllListeners();
    }

    // Called when user clicks Yes in the confirmation panel
    void OnConfirmDelete()
    {
        if (string.IsNullOrEmpty(_pendingDeleteId))
        {
            Debug.LogWarning("No pending delete id.");
            CloseDeleteConfirm();
            return;
        }

        // Dispatch delete request:
        GameObject target = deleteHandlerTarget != null ? deleteHandlerTarget : (deleteConfirmPanel != null ? deleteConfirmPanel : this.gameObject);

        target.SendMessage("DeleteArtwork", _pendingDeleteId, SendMessageOptions.DontRequireReceiver);

        // Remove the item from UI
        if (_pendingDeleteItemGO != null)
        {
            Destroy(_pendingDeleteItemGO);
            _pendingDeleteItemGO = null;
        }

        // cleanup and hide panel
        _pendingDeleteId = null;
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);

        // remove listeners
        if (deleteConfirmYesButton != null) deleteConfirmYesButton.onClick.RemoveAllListeners();
        if (deleteConfirmNoButton != null) deleteConfirmNoButton.onClick.RemoveAllListeners();

        // LoadArtworks(); 
    }

    // Helper: searches the instantiated prefab and returns the first child whose tag equals requested tag
    Transform FindChildWithTag(Transform parent, string requestedTag)
    {
        if (parent == null || string.IsNullOrWhiteSpace(requestedTag)) return null;

        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject.tag == requestedTag)
                return t;
        }
        return null;
    }

    // Image-only download routine (returns a Sprite assigned to Image)
    IEnumerator DownloadImageRoutine(string url, Image uiImage)
    {
        if (string.IsNullOrEmpty(url) || uiImage == null) yield break;

        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (uwr.result != UnityWebRequest.Result.Success)
#else
            if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                Debug.LogWarning($"Image download failed: {uwr.error} - {url}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
            if (tex == null)
            {
                Debug.LogWarning("Downloaded texture is null: " + url);
                yield break;
            }

            // Create a Sprite and assign to the Image component
            Sprite spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            uiImage.sprite = spr;
            uiImage.preserveAspect = true;

            // Track runtime sprite for cleanup
            _runtimeSprites.Add(spr);
        }
    }
}
