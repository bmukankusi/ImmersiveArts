using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;

public class AdminDashboardController : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text todayUsersText;
    public TMP_Text totalScansText;
    public TMP_Text monthlyAvgUsersText;
    public TMP_Text monthlyAvgScansText;

    [Header("Artwork Stats")]
    public TMP_Text bottlesScansText;
    public TMP_Text bluebirdScansText;
    public TMP_Text womenScansText;
    public TMP_Text zebrasScansText;

    [Header("Buttons")]
    public Button refreshButton;

    private bool isRefreshing = false;

    void Start()
    {
        // Set up button listener
        refreshButton.onClick.AddListener(OnRefreshClicked);

        // Do not auto-load here; loading happens when panel becomes active (OnEnable)
    }

    void OnEnable()
    {
        // Load data when dashboard is opened
        if (!isRefreshing)
        {
            _ = LoadDashboardData();
        }

        // Start auto-refresh
        InvokeRepeating(nameof(AutoRefresh), 120f, 120f);
    }

    void OnDisable()
    {
        // Stop auto-refresh when dashboard is closed
        CancelInvoke(nameof(AutoRefresh));
    }

    async void OnRefreshClicked()
    {
        if (isRefreshing) return;

        await LoadDashboardData();
    }

    public async Task LoadDashboardData()
    {
        if (isRefreshing) return;

        isRefreshing = true;
        refreshButton.interactable = false;

        // Ensure AnalyticsManager exists in the scene
        if (AnalyticsManager.Instance == null)
        {
            var found = FindObjectOfType<AnalyticsManager>();
            if (found != null)
            {
                // assign the instance so other code can use it
                AnalyticsManager.Instance = found;
                Debug.Log("AdminDashboardController: Found AnalyticsManager in scene and assigned Instance.");
            }
            else
            {
                Debug.LogError("AnalyticsManager not found!");
                UpdateUIWithError();

                // Reset state and return
                isRefreshing = false;
                refreshButton.interactable = true;
                return;
            }
        }

        try
        {
            var analyticsData = await AnalyticsManager.Instance.GetDashboardData();
            UpdateUI(analyticsData);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading dashboard data: {e.Message}");
            UpdateUIWithError();
        }
        finally
        {
            isRefreshing = false;
            refreshButton.interactable = true;
        }
    }

    private void UpdateUI(AnalyticsManager.AnalyticsData analyticsData)
    {
        // Update main stats
        todayUsersText.text = analyticsData.todayUsers.ToString("N0");
        totalScansText.text = analyticsData.totalScans.ToString("N0");
        monthlyAvgUsersText.text = analyticsData.monthlyAvgUsers.ToString("N0");
        monthlyAvgScansText.text = analyticsData.monthlyAvgScans.ToString("N0");

        // Update artwork popularity
        if (analyticsData.artworkPopularity != null)
        {
            bottlesScansText.text = analyticsData.artworkPopularity.ContainsKey("bottles") ?
                analyticsData.artworkPopularity["bottles"].ToString("N0") : "0";
            bluebirdScansText.text = analyticsData.artworkPopularity.ContainsKey("bluebird") ?
                analyticsData.artworkPopularity["bluebird"].ToString("N0") : "0";
            womenScansText.text = analyticsData.artworkPopularity.ContainsKey("women") ?
                analyticsData.artworkPopularity["women"].ToString("N0") : "0";
            zebrasScansText.text = analyticsData.artworkPopularity.ContainsKey("zebras") ?
                analyticsData.artworkPopularity["zebras"].ToString("N0") : "0";
        }
    }

    private void UpdateUIWithError()
    {
        todayUsersText.text = "Error";
        totalScansText.text = "Error";
        monthlyAvgUsersText.text = "Error";
        monthlyAvgScansText.text = "Error";

        bottlesScansText.text = "Error";
        bluebirdScansText.text = "Error";
        womenScansText.text = "Error";
        zebrasScansText.text = "Error";
    }

    async void AutoRefresh()
    {
        if (gameObject.activeInHierarchy && !isRefreshing)
        {
            await LoadDashboardData();
        }
    }
}