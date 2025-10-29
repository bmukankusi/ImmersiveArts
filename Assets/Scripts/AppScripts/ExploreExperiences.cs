using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Database;
using TMPro;

public class ExploreExperiences : MonoBehaviour
{
    [Header("UI (assign in Inspector)")]
    public TMP_InputField searchInput;
    public Button searchButton;
    public Transform suggestionsContainer; // container for suggestion items (vertical layout)
    public GameObject suggestionItemPrefab; // prefab with a Button and a TMP_Text component
    public TMP_Text feedbackText; // "No results" or temporary messages

    [Header("Gallery Panels")]
    public Transform panelsParent; // parent that contains gallery panels (one per gallery)

    [Header("Behavior")]
    public int maxSuggestions = 6;

    // Internal model for galleries loaded from Firebase
    class Gallery
    {
        public string id;
        public string name;
        public Dictionary<string, object> data;
    }

    List<Gallery> galleries = new List<Gallery>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // preserve original comment block
        // Start is called once before the first execution of Update after the MonoBehaviour is created

        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchInputChanged);
        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchClicked);
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);

        // Begin loading galleries from Firebase (fire-and-forget)
        _ = LoadGalleriesAsync();
    }

    // Update is called once per frame
    void Update()
    {
        // preserve original comment block
        // Update is called once per frame
    }

    async Task LoadGalleriesAsync()
    {
        try
        {
            var snap = await FirebaseDatabase.DefaultInstance.GetReference("galleries").GetValueAsync();
            galleries.Clear();
            if (snap != null && snap.Exists)
            {
                foreach (var child in snap.Children)
                {
                    var g = new Gallery
                    {
                        id = child.Key,
                        name = child.Child("name")?.Value?.ToString() ?? child.Key,
                        data = new Dictionary<string, object>()
                    };

                    // copy other fields if needed
                    foreach (var field in child.Children)
                    {
                        g.data[field.Key] = field.Value;
                    }

                    galleries.Add(g);
                }
            }
        }
        catch (Exception ex)
        {
            ShowFeedback("Failed to load galleries: " + ex.Message, true);
        }
    }

    void OnSearchInputChanged(string text)
    {
        UpdateSuggestions(text);
    }

    void UpdateSuggestions(string text)
    {
        ClearSuggestions();

        if (string.IsNullOrWhiteSpace(text))
            return;

        var query = text.Trim();
        var matches = galleries
            .Where(g => g.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(maxSuggestions)
            .ToList();

        if (matches.Count == 0)
        {
            CreateNoResultSuggestion();
            return;
        }

        foreach (var g in matches)
        {
            var item = CreateSuggestionItem(g.name);
            var btn = item.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    searchInput.SetTextWithoutNotify(g.name);
                    ClearSuggestions();
                    OnSearchClicked();
                });
            }
        }
    }

    void OnSearchClicked()
    {
        var query = searchInput != null ? searchInput.text?.Trim() : string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            ShowFeedback("Type a gallery name to search.", true);
            return;
        }

        var matches = galleries
            .Where(g => g.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        if (matches.Count == 0)
        {
            ShowFeedback("No results found.", true);
            HideAllGalleryPanels();
            return;
        }

        ShowFeedback(string.Empty, false);
        ShowMatchingPanels(matches);
    }

    void ShowMatchingPanels(List<Gallery> matches)
    {
        // Hide all first
        HideAllGalleryPanels();

        // Try to show panels whose GameObject.name matches gallery id or gallery name (case-insensitive)
        foreach (var g in matches)
        {
            // try by id
            var byId = panelsParent?.Find(g.id);
            if (byId != null)
            {
                byId.gameObject.SetActive(true);
                continue;
            }

            // try by name (search children)
            foreach (Transform child in panelsParent)
            {
                if (string.Equals(child.name, g.name, StringComparison.OrdinalIgnoreCase) ||
                    child.name.IndexOf(g.name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.gameObject.SetActive(true);
                }
            }
        }
    }

    void HideAllGalleryPanels()
    {
        if (panelsParent == null) return;
        foreach (Transform child in panelsParent)
            child.gameObject.SetActive(false);
    }

    GameObject CreateSuggestionItem(string text)
    {
        if (suggestionItemPrefab == null || suggestionsContainer == null)
            return null;

        var go = Instantiate(suggestionItemPrefab, suggestionsContainer);
        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = text;
        return go;
    }

    void CreateNoResultSuggestion()
    {
        var item = CreateSuggestionItem("No results");
        if (item == null) return;
        var btn = item.GetComponentInChildren<Button>();
        if (btn != null)
            btn.interactable = false;
    }

    void ClearSuggestions()
    {
        if (suggestionsContainer == null) return;
        for (int i = suggestionsContainer.childCount - 1; i >= 0; i--)
            Destroy(suggestionsContainer.GetChild(i).gameObject);
    }

    void ShowFeedback(string message, bool isError)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.green;
        feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    void OnDestroy()
    {
        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(OnSearchInputChanged);
        if (searchButton != null)
            searchButton.onClick.RemoveListener(OnSearchClicked);
    }
}
