using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;
using System;


/// <summary>
///  This controller manages the admin dashboard UI, fetching and displaying analytics data.
/// </summary>
/// <remarks> It interacts with the AnalyticsManager to retrieve data and updates the UI elements accordingly.
/// It also provides manual and automatic refresh capabilities.</remarks>

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

        // Do not auto load here/ loading happens when panel becomes active 
    }

    void OnEnable()
    {
        // Load data when dashboard is opened
        if (!isRefreshing)
        {
            _ = LoadDashboardData();
        }

        // Start auto refresh
        InvokeRepeating(nameof(AutoRefresh), 120f, 120f);
    }

    void OnDisable()
    {
        // Stop auto refresh when dashboard is closed
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
                AnalyticsManager.Instance = found;
                Debug.Log("AdminDashboardController: Found AnalyticsManager in scene and assigned Instance.");
            }
            else
            {
                // Create a GameObject with AnalyticsManager so analytics init runs
                var go = new GameObject("AnalyticsManager");
                var am = go.AddComponent<AnalyticsManager>();
                // Awake should have set Instance, but ensure fallback
                if (AnalyticsManager.Instance == null) AnalyticsManager.Instance = am;
                Debug.Log("AdminDashboardController: Created AnalyticsManager GameObject.");
            }
        }

        // Wait for AnalyticsManager to initialize Firestore 
        bool ready = true;
        try
        {
            ready = await AnalyticsManager.Instance.WaitForInitializationAsync(5000);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Error while waiting for Analytics initialization: " + ex.Message);
            ready = false;
        }

        if (!ready)
        {
            Debug.LogError("AnalyticsManager not ready. Cannot load dashboard data.");
            UpdateUIWithError();
            isRefreshing = false;
            refreshButton.interactable = true;
            return;
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
                analyticsData.artworkPopularity["blueBird"].ToString("N0") : "0";
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