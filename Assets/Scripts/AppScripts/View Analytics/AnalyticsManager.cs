using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using Firebase.Auth;

/// <summary>
///  Responsible for managing analytics data collection and retrieval using Firebase Firestore.
/// </summary>
/// <remarks> This singleton class initializes Firebase Firestore, logs user device information, user interactions with artworks, and provides methods to retrieve aggregated analytics data for dashboard display.</remarks>

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
        public object timestamp; 
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
        public object timestamp;
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
        db = null;

        var auth = FirebaseAuth.DefaultInstance;
        if (auth != null && auth.CurrentUser != null)
        {
            userId = auth.CurrentUser.UserId;
            db = FirebaseFirestore.DefaultInstance;
            SendDeviceInfo();
            return;
        }

        // Sign in anonymously so we can track devices without requiring user login
        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("Anonymous sign-in failed; falling back to device id: " + task.Exception?.Flatten()?.Message);
                userId = SystemInfo.deviceUniqueIdentifier;
            }
            else
            {
                userId = FirebaseAuth.DefaultInstance.CurrentUser?.UserId ?? SystemInfo.deviceUniqueIdentifier;
                Debug.Log($"AnalyticsManager: Signed in anonymously as {userId}");
            }

            db = FirebaseFirestore.DefaultInstance;
            SendDeviceInfo();
        });
    }

    // record views / interactions
    public void LogInteraction(string artworkId, string artworkName, float duration, string interactionType = "view")
    {
        if (db == null)
        {
            Debug.LogWarning("AnalyticsManager: Firestore not initialized - cannot log interaction.");
            return;
        }

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
            { "timestamp", FieldValue.ServerTimestamp }
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

    public void LogInteraction(string artworkName, float duration, string interactionType = "view")
    {
        LogInteraction(null, artworkName, duration, interactionType);
    }

    // New methods for retrieving analytics data 
    public async Task<AnalyticsData> GetDashboardData()
    {
        var analyticsData = new AnalyticsData();

        try
        {
            if (db == null)
                db = FirebaseFirestore.DefaultInstance;

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
        // Query using Firestore Timestamp for robust filtering
        DateTime utcToday = DateTime.UtcNow.Date;
        var ts = Timestamp.FromDateTime(utcToday);

        QuerySnapshot snapshot = await db.Collection("analytics/users/devices")
            .WhereGreaterThanOrEqualTo("timestamp", ts)
            .GetSnapshotAsync();

        var uniqueUsers = new HashSet<string>();
        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            if (document.ContainsField("user_id"))
            {
                var uid = document.GetValue<string>("user_id");
                if (!string.IsNullOrEmpty(uid)) uniqueUsers.Add(uid);
            }
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
        DateTime startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        DateTime endDate = DateTime.UtcNow;
        var tsStart = Timestamp.FromDateTime(startDate);
        var tsEnd = Timestamp.FromDateTime(endDate);

        QuerySnapshot usersSnapshot = await db.Collection("analytics/users/devices")
            .WhereGreaterThanOrEqualTo("timestamp", tsStart)
            .WhereLessThanOrEqualTo("timestamp", tsEnd)
            .GetSnapshotAsync();

        var monthlyUsers = new HashSet<string>();
        foreach (DocumentSnapshot document in usersSnapshot.Documents)
        {
            if (document.ContainsField("user_id"))
            {
                var uid = document.GetValue<string>("user_id");
                if (!string.IsNullOrEmpty(uid)) monthlyUsers.Add(uid);
            }
        }

        QuerySnapshot scansSnapshot = await db.Collection("analytics/interactions/artworks")
            .WhereGreaterThanOrEqualTo("timestamp", tsStart)
            .WhereLessThanOrEqualTo("timestamp", tsEnd)
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
            string key = null;
            if (document.ContainsField("artwork_id"))
                key = document.GetValue<string>("artwork_id");
            else if (document.ContainsField("artwork_name"))
                key = document.GetValue<string>("artwork_name");

            if (!string.IsNullOrEmpty(key) && popularity.ContainsKey(key))
                popularity[key]++;
        }
        return popularity;
    }

    private async Task<Dictionary<string, int>> GetDailyTrends()
    {
        Dictionary<string, int> trends = new Dictionary<string, int>();
        DateTime lastWeek = DateTime.UtcNow.AddDays(-7);
        var ts = Timestamp.FromDateTime(lastWeek);

        QuerySnapshot snapshot = await db.Collection("analytics/interactions/artworks")
            .WhereGreaterThanOrEqualTo("timestamp", ts)
            .GetSnapshotAsync();

        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            try
            {
                if (!document.ContainsField("timestamp")) continue;

                var raw = document.GetValue<object>("timestamp");
                DateTime dt;
                if (raw is Timestamp t)
                    dt = t.ToDateTime();
                else
                    dt = DateTime.Parse(raw.ToString());

                string date = dt.ToString("yyyy-MM-dd");
                if (trends.ContainsKey(date)) trends[date]++;
                else trends[date] = 1;
            }
            catch
            {
                // ignore malformed timestamp entries
            }
        }
        return trends;
    }

    void SendDeviceInfo()
    {
        if (db == null) return;

        var device = new Dictionary<string, object>
        {
            { "device_model", SystemInfo.deviceModel },
            { "operating_system", SystemInfo.operatingSystem },
            { "device_type", SystemInfo.deviceType.ToString() },
            { "ar_support_status", "unknown" },
            { "app_version", Application.version },
            { "timestamp", FieldValue.ServerTimestamp },
            { "session_id", sessionId },
            { "user_id", userId }
        };

        db.Collection("analytics/users/devices").AddAsync(device).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted) Debug.LogWarning("SendDeviceInfo failed: " + t.Exception);
        });
    }

    public bool IsInitialized => db != null;

    /// <summary>
    /// Waits up to timeoutMs for Firebase/Firestore initialization to complete.
    /// Returns true when initialized, false on timeout.
    /// </summary>
    public async Task<bool> WaitForInitializationAsync(int timeoutMs = 5000)
    {
        const int poll = 200;
        int waited = 0;
        while (db == null && waited < timeoutMs)
        {
            await Task.Delay(poll);
            waited += poll;
        }
        return db != null;
    }
}