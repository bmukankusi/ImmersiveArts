using System;
using System.Collections;
using System.Collections.Generic;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using Firebase.Auth;
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

    public GameObject deleteHandlerTarget; // target for delete message; if null, uses deleteConfirmPanel or this GameObject

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

            var auth = FirebaseAuth.DefaultInstance;
            // If not signed in, sign in anonymously so Firestore rules that require auth succeed.
            if (auth.CurrentUser == null)
            {
                auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(signInTask =>
                {
                    if (signInTask.IsFaulted)
                    {
                        Debug.LogError("Anonymous sign-in failed: " + signInTask.Exception?.Flatten()?.Message);
                        // Still attempt to initialize Firestore (will fail if rules require auth).
                    }
                    else
                    {
                        Debug.Log("Signed in anonymously: " + auth.CurrentUser.UserId);
                    }

                    db = FirebaseFirestore.DefaultInstance;
                    LoadArtworks();
                });
            }
            else
            {
                db = FirebaseFirestore.DefaultInstance;
                LoadArtworks();
            }
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

        var auth = FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser == null)
            Debug.LogWarning("Not signed in (auth.CurrentUser is null). Reads may be denied by rules.");

        db.Collection("Artworks").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to get Artworks: " + task.Exception?.Flatten()?.ToString());
                return;
            }

            ClearContent();

            QuerySnapshot snapshot = task.Result;
            if (snapshot == null || snapshot.Count == 0)
            {
                Debug.Log("No artwork documents found in 'Artworks' collection.");
                return;
            }

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

                    // NEW: debug/log image info so you can see why images don't load
                    Debug.Log($"Artwork {doc.Id} -> name='{name}' imageUrl='{imageUrl ?? "null"}' uiImageAssigned={(uiImage != null)}");

                    if (!string.IsNullOrEmpty(imageUrl) && uiImage != null)
                    {
                        if (imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            StartCoroutine(DownloadImageRoutine(imageUrl, uiImage));
                        }
                        else
                        {
                            Debug.LogWarning($"Artwork {doc.Id} has invalid image URL: {imageUrl}");
                        }
                    }
                    else if (!string.IsNullOrEmpty(imageUrl) && uiImage == null)
                    {
                        Debug.LogWarning($"Artwork {doc.Id} has image URL but prefab has no Image child with tag '{imageChildTag}'.");
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
        if (string.IsNullOrEmpty(url) || uiImage == null)
        {
            Debug.LogWarning("DownloadImageRoutine: empty url or missing Image component.");
            yield break;
        }

        Debug.Log($"DownloadImageRoutine: starting download for url={url}");

        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
#if UNITY_2020_1_OR_NEWER
            uwr.timeout = 15;
#endif
            yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = uwr.result != UnityWebRequest.Result.Success;
#else
            bool failed = uwr.isNetworkError || uwr.isHttpError;
#endif
            long code = uwr.responseCode;
            if (failed)
            {
                Debug.LogWarning($"Image download failed: {uwr.error} - HTTP {code} - {url}");
                yield break;
            }

            string contentType = uwr.GetResponseHeader("Content-Type") ?? "";
            Debug.Log($"DownloadImageRoutine: HTTP {code} Content-Type={contentType} for {url}");

            Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
            if (tex == null)
            {
                Debug.LogWarning("Downloaded texture is null: " + url);
                yield break;
            }

            Sprite spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            uiImage.sprite = spr;
            uiImage.preserveAspect = true;
            uiImage.enabled = true;              // ensure Image is visible
            uiImage.type = Image.Type.Simple;    // ensure correct image type
            _runtimeSprites.Add(spr);

            Debug.Log($"Image downloaded and assigned: {url} (HTTP {code}) size={tex.width}x{tex.height}");
        }
    }
}
