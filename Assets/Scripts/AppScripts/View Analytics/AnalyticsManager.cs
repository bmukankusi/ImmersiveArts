using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Linq;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance;

    [System.Serializable]
    public class UserDeviceData
    {
        public string device_model;
        public string operating_system;
        public string device_type;
        public string ar_support_status;
        public string app_version;
        public string timestamp;
        public string session_id;
        public string user_id;
    }

    [System.Serializable]
    public class InteractionData
    {
        public string artwork_id;
        public string artwork_name;
        public string gallery_id;
        public string gallery_name;
        public string interaction_type;
        public float interaction_duration;
        public string timestamp;
        public string session_id;
        public string user_id;
    }

    [System.Serializable]
    public class AnalyticsData
    {
        public int todayUsers;
        public int totalScans;
        public int monthlyAvgUsers;
        public int monthlyAvgScans;
        public Dictionary<string, int> artworkPopularity;
        public Dictionary<string, int> dailyTrends;
    }

    private FirebaseFirestore db;
    private string sessionId;
    private string userId;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAnalytics();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeAnalytics()
    {
        sessionId = Guid.NewGuid().ToString();
        userId = SystemInfo.deviceUniqueIdentifier;
        db = FirebaseFirestore.DefaultInstance;
        SendDeviceInfo();
    }

    // ... (Previous methods: SendDeviceInfo, CheckARSupport, LogArtworkInteraction, etc.)

    // PUBLIC: called by ARSceneController to record views / interactions
    public void LogInteraction(string artworkId, string artworkName, float duration, string interactionType = "view")
    {
        if (db == null)
        {
            Debug.LogWarning("AnalyticsManager: Firestore not initialized - cannot log interaction.");
            return;
        }

        // Use a dictionary so we can set Firestore server timestamp reliably
        var doc = new Dictionary<string, object>
        {
            { "artwork_id", artworkId },
            { "artwork_name", artworkName },
            { "gallery_id", null },
            { "gallery_name", null },
            { "interaction_type", interactionType },
            { "interaction_duration", duration },
            { "session_id", sessionId },
            { "user_id", userId },
            { "timestamp", FieldValue.ServerTimestamp } // server-side timestamp
        };

        db.Collection("analytics/interactions/artworks")
          .AddAsync(doc)
          .ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
                Debug.LogError($"AnalyticsManager: Failed to log interaction for {artworkName}: {task.Exception}");
            else
                Debug.Log($"AnalyticsManager: Logged interaction for {artworkName} ({interactionType}) duration={duration:F1}s id={artworkId}");
        });
    }

    // Keep existing signature for compatibility with other callers
    public void LogInteraction(string artworkName, float duration, string interactionType = "view")
    {
        // Forward to the new overload when artwork id is not supplied
        LogInteraction(null, artworkName, duration, interactionType);
    }

    // New methods for retrieving analytics data
    public async Task<AnalyticsData> GetDashboardData()
    {
        var analyticsData = new AnalyticsData();

        try
        {
            var todayUsersTask = GetTodayUsers();
            var totalScansTask = GetTotalScans();
            var monthlyAvgTask = GetMonthlyAverages();
            var artworkPopularityTask = GetArtworkPopularity();
            var dailyTrendsTask = GetDailyTrends();

            await Task.WhenAll(todayUsersTask, totalScansTask, monthlyAvgTask, artworkPopularityTask, dailyTrendsTask);

            analyticsData.todayUsers = todayUsersTask.Result;
            analyticsData.totalScans = totalScansTask.Result;
            analyticsData.monthlyAvgUsers = monthlyAvgTask.Result.avgUsers;
            analyticsData.monthlyAvgScans = monthlyAvgTask.Result.avgScans;
            analyticsData.artworkPopularity = artworkPopularityTask.Result;
            analyticsData.dailyTrends = dailyTrendsTask.Result;

            return analyticsData;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting dashboard data: {e.Message}");
            return analyticsData;
        }
    }

    private async Task<int> GetTodayUsers()
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        QuerySnapshot snapshot = await db.Collection("analytics/users/devices")
            .WhereGreaterThanOrEqualTo("timestamp", today)
            .GetSnapshotAsync();

        var uniqueUsers = new HashSet<string>();
        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            UserDeviceData data = document.ConvertTo<UserDeviceData>();
            uniqueUsers.Add(data.user_id);
        }
        return uniqueUsers.Count;
    }

    private async Task<int> GetTotalScans()
    {
        QuerySnapshot snapshot = await db.Collection("analytics/interactions/artworks")
            .WhereEqualTo("interaction_type", "scan")
            .GetSnapshotAsync();
        return snapshot.Count;
    }

    private async Task<(int avgUsers, int avgScans)> GetMonthlyAverages()
    {
        DateTime startDate = new DateTime(2025, 11, 1);
        DateTime endDate = DateTime.UtcNow;

        // Get user count for the month
        QuerySnapshot usersSnapshot = await db.Collection("analytics/users/devices")
            .WhereGreaterThanOrEqualTo("timestamp", startDate.ToString("yyyy-MM-dd"))
            .WhereLessThanOrEqualTo("timestamp", endDate.ToString("yyyy-MM-dd"))
            .GetSnapshotAsync();

        var monthlyUsers = new HashSet<string>();
        foreach (DocumentSnapshot document in usersSnapshot.Documents)
        {
            UserDeviceData data = document.ConvertTo<UserDeviceData>();
            monthlyUsers.Add(data.user_id);
        }

        // Get scan count for the month
        QuerySnapshot scansSnapshot = await db.Collection("analytics/interactions/artworks")
            .WhereGreaterThanOrEqualTo("timestamp", startDate.ToString("yyyy-MM-dd"))
            .WhereLessThanOrEqualTo("timestamp", endDate.ToString("yyyy-MM-dd"))
            .GetSnapshotAsync();

        int daysInMonth = (endDate - startDate).Days + 1;
        int avgUsers = daysInMonth > 0 ? monthlyUsers.Count / daysInMonth : 0;
        int avgScans = daysInMonth > 0 ? scansSnapshot.Count / daysInMonth : 0;

        return (avgUsers, avgScans);
    }

    private async Task<Dictionary<string, int>> GetArtworkPopularity()
    {
        Dictionary<string, int> popularity = new Dictionary<string, int>
        {
            { "bottles", 0 },
            { "blueBird", 0 },
            { "womenEmpowered", 0 },
            { "zebras", 0 }
        };

        QuerySnapshot snapshot = await db.Collection("analytics/interactions/artworks").GetSnapshotAsync();
        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            InteractionData data = document.ConvertTo<InteractionData>();
            if (popularity.ContainsKey(data.artwork_name))
            {
                popularity[data.artwork_name]++;
            }
        }
        return popularity;
    }

    private async Task<Dictionary<string, int>> GetDailyTrends()
    {
        Dictionary<string, int> trends = new Dictionary<string, int>();
        DateTime lastWeek = DateTime.UtcNow.AddDays(-7);

        QuerySnapshot snapshot = await db.Collection("analytics/interactions/artworks")
            .WhereGreaterThanOrEqualTo("timestamp", lastWeek.ToString("yyyy-MM-dd"))
            .GetSnapshotAsync();

        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            InteractionData data = document.ConvertTo<InteractionData>();
            string date = data.timestamp.Split('T')[0];
            if (trends.ContainsKey(date))
            {
                trends[date]++;
            }
            else
            {
                trends[date] = 1;
            }
        }
        return trends;
    }

    // Placeholder: ensure SendDeviceInfo exists (called in InitializeAnalytics)
    void SendDeviceInfo()
    {
        // Basic device info send (non-blocking)
        if (db == null) return;

        var device = new UserDeviceData
        {
            device_model = SystemInfo.deviceModel,
            operating_system = SystemInfo.operatingSystem,
            device_type = SystemInfo.deviceType.ToString(),
            ar_support_status = "unknown",
            app_version = Application.version,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            session_id = sessionId,
            user_id = userId
        };

        db.Collection("analytics/users/devices").AddAsync(device).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted) Debug.LogWarning("SendDeviceInfo failed: " + t.Exception);
        });
    }
}