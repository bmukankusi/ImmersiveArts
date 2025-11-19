using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Search Manager handles the search functionality within a Explore Panel.
/// </summary>
public class SearchMgr : MonoBehaviour
{
    public GameObject contentHolder;
    public TMP_InputField searchBar; // inspector reference to the input field
    public GameObject[] Element;
    public int totalElements;

    void Start()
    {
        if (contentHolder == null)
        {
            Debug.LogWarning("SearchMgr: contentHolder is not assigned.");
            totalElements = 0;
            Element = new GameObject[0];
            return;
        }

        RebuildElementsArray();
    }

    // Rebuild element list from contentHolder children
    void RebuildElementsArray()
    {
        totalElements = contentHolder.transform.childCount;
        Element = new GameObject[totalElements];

        for (int i = 0; i < totalElements; i++)
        {
            Element[i] = contentHolder.transform.GetChild(i).gameObject;
        }
    }

    // Search Function - shows panels that contain the search text in any nested TextMeshProUGUI
    public void Search()
    {
        if (searchBar == null)
        {
            Debug.LogWarning("SearchMgr: searchBar (TMP_InputField) is not assigned.");
            return;
        }

        if (contentHolder != null && (Element == null || Element.Length != contentHolder.transform.childCount))
        {
            RebuildElementsArray();
        }

        if (Element == null || Element.Length == 0)
            return;

        string searchText = (searchBar.text ?? string.Empty).Trim();

        // empty search -> show all
        if (searchText.Length == 0)
        {
            foreach (var ele in Element)
            {
                if (ele != null) ele.SetActive(true);
            }
            return;
        }

        foreach (GameObject ele in Element)
        {
            if (ele == null)
                continue;

            bool matched = false;

            // Get all TextMeshProUGUI components in this panel (including inactive children)
            var texts = ele.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t == null || string.IsNullOrEmpty(t.text))
                    continue;

                // case-insensitive substring match
                if (t.text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched = true;
                    break;
                }
            }

            ele.SetActive(matched);
        }
    }
}
