using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using TMPro;

/// <summary>
/// - Assign TMP fields in inspector to display today's users/scans, monthly averages and a per-month average list.
/// - Call Refresh() from a UI Button or enable refreshOnStart.
/// - Reads documents at: Galleries/{galleryId}/daily/{yyyy-MM-dd}
///   with fields: "date" (yyyy-MM-dd), "uniqueDevices" (long), "scans" (long)
/// </summary>
public class Analytics : MonoBehaviour
{
    [Header("Gallery")]
    [Tooltip("Collection document name under Galleries. Example: 'NP Art gallery'")]
    public string galleryId = "NP Art gallery";

    [Tooltip("Automatically refresh analytics on Start")]
    public bool refreshOnStart = true;

    [Header("TextMeshPro UI")]
    [Tooltip("Today's unique users")]
    public TextMeshProUGUI dailyUsersTMP;

    [Tooltip("Today's AR scans")]
    public TextMeshProUGUI dailyScansTMP;

    [Tooltip("Average daily unique users (last N months)")]
    public TextMeshProUGUI monthlyAvgUsersTMP;

    [Tooltip("Average daily AR scans (last N months)")]
    public TextMeshProUGUI monthlyAvgScansTMP;

    [Tooltip("Multiline per-month average list (from Oct 2025 to last usage)")]
    public TextMeshProUGUI monthlyAveragesListTMP;

    [Header("Options")]
    [Tooltip("How many months (including current) to average for the 'overall' values")]
    public int monthsToAverage = 6;

    FirebaseFirestore db;
    bool firebaseReady = false;

    void Awake()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                firebaseReady = true;
                Debug.Log("[Analytics] Firebase initialized.");
            }
            else
            {
                firebaseReady = false;
                Debug.LogError($"[Analytics] Firebase dependency error: {status}");
            }
        });
    }

    void Start()
    {
        if (refreshOnStart)
            Refresh();
    }

    // Public: call from UI button or other scripts
    public void Refresh()
    {
        SetAllUiLoading();

        if (!firebaseReady)
        {
            Debug.LogWarning("[Analytics] Firebase not ready. Skipping refresh.");
            SetAllUiUnavailable();
            return;
        }

        LoadTodayAndMonthlyOverview(galleryId, (day, months) =>
        {
            UpdateDailyUi(day);
            UpdateMonthlyUi(months);

            // Also load the per-month averages from Oct 2025 up to last usage and render multiline list
            LoadMonthlyAveragesFromOct2025(galleryId, (ok, list) =>
            {
                if (ok && list != null)
                    RenderMonthlyAveragesList(list);
                else
                    SetTmpro(monthlyAveragesListTMP, "No monthly data (from Oct 2025).");
            });
        });
    }

    #region DTOs

    class DailyAnalytics
    {
        public string DateString;
        public DateTime DateUtc;
        public long UniqueDevices;
        public long Scans;
    }

    // Change MonthlySummary from private to public
    public class MonthlySummary
    {
        public string MonthLabel;
        public int Year;
        public int Month;
        public int DaysInMonth;
        public int DaysConsidered;
        public long TotalUniqueDevices;
        public long TotalScans;
        public double AverageDailyUniqueDevices => DaysConsidered > 0 ? (double)TotalUniqueDevices / DaysConsidered : 0;
        public double AverageDailyScans => DaysConsidered > 0 ? (double)TotalScans / DaysConsidered : 0;
    }

    class MonthlyAveragesResult
    {
        public List<MonthlySummary> Months = new List<MonthlySummary>();
        public double OverallAverageDailyUniqueDevices;
        public double OverallAverageDailyScans;
    }

    #endregion

    #region Firestore loaders

    void LoadRange(string galleryId, DateTime startUtc, DateTime endUtc, Action<bool, List<DailyAnalytics>> callback)
    {
        if (db == null) { callback?.Invoke(false, null); return; }
        if (string.IsNullOrEmpty(galleryId)) galleryId = "NP Art gallery";

        var startStr = startUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endStr = endUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var query = db.Collection("Galleries").Document(galleryId).Collection("daily")
                      .WhereGreaterThanOrEqualTo("date", startStr)
                      .WhereLessThanOrEqualTo("date", endStr);

        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[Analytics] LoadRange failed: {task.Exception}");
                callback?.Invoke(false, null);
                return;
            }

            var snapshot = task.Result;
            var map = new Dictionary<string, DailyAnalytics>(StringComparer.Ordinal);
            foreach (var doc in snapshot.Documents)
            {
                try
                {
                    var dict = doc.ToDictionary();
                    var dateStr = dict.ContainsKey("date") ? dict["date"]?.ToString() : doc.Id;
                    if (string.IsNullOrEmpty(dateStr)) continue;

                    long uniqueDevices = 0;
                    long scans = 0;
                    if (dict.TryGetValue("uniqueDevices", out var ud)) uniqueDevices = Convert.ToInt64(ud);
                    if (dict.TryGetValue("scans", out var sc)) scans = Convert.ToInt64(sc);

                    map[dateStr] = new DailyAnalytics
                    {
                        DateString = dateStr,
                        DateUtc = DateTime.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                        UniqueDevices = uniqueDevices,
                        Scans = scans
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Analytics] Skipping doc {doc.Id}: {ex}");
                }
            }

            var results = new List<DailyAnalytics>();
            for (var d = startUtc.Date; d <= endUtc.Date; d = d.AddDays(1))
            {
                var ds = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (map.TryGetValue(ds, out var rec))
                    results.Add(rec);
                else
                    results.Add(new DailyAnalytics { DateString = ds, DateUtc = d, UniqueDevices = 0, Scans = 0 });
            }

            callback?.Invoke(true, results);
        });
    }

    void LoadDaily(string galleryId, DateTime dateUtc, Action<bool, DailyAnalytics> callback)
    {
        var start = dateUtc.Date;
        LoadRange(galleryId, start, start, (ok, list) =>
        {
            if (!ok || list == null || list.Count == 0) callback?.Invoke(false, null);
            else callback?.Invoke(true, list[0]);
        });
    }

    void LoadLastNMonthsAverages(string galleryId, int monthsCount, Action<bool, MonthlyAveragesResult> callback)
    {
        if (monthsCount <= 0) monthsCount = 6;
        var nowUtc = DateTime.UtcNow.Date;
        var firstOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startMonth = firstOfThisMonth.AddMonths(-(monthsCount - 1));
        var startDate = startMonth;
        var endDate = firstOfThisMonth.AddMonths(1).AddDays(-1);

        LoadRange(galleryId, startDate, endDate, (ok, dailyList) =>
        {
            if (!ok || dailyList == null) { callback?.Invoke(false, null); return; }

            var result = new MonthlyAveragesResult();
            var groups = new Dictionary<string, List<DailyAnalytics>>(StringComparer.Ordinal);
            foreach (var d in dailyList)
            {
                var key = d.DateUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                if (!groups.TryGetValue(key, out var list)) { list = new List<DailyAnalytics>(); groups[key] = list; }
                list.Add(d);
            }

            double totalAvgUsersSum = 0;
            double totalAvgScansSum = 0;
            int countedMonths = 0;

            for (var m = startMonth; m <= firstOfThisMonth; m = m.AddMonths(1))
            {
                var key = m.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
                long totalUnique = 0;
                long totalScans = 0;

                if (groups.TryGetValue(key, out var list))
                {
                    foreach (var d in list)
                    {
                        totalUnique += d.UniqueDevices;
                        totalScans += d.Scans;
                    }
                }

                var summary = new MonthlySummary
                {
                    MonthLabel = key,
                    Year = m.Year,
                    Month = m.Month,
                    DaysInMonth = daysInMonth,
                    DaysConsidered = daysInMonth,
                    TotalUniqueDevices = totalUnique,
                    TotalScans = totalScans
                };
                result.Months.Add(summary);

                totalAvgUsersSum += summary.AverageDailyUniqueDevices;
                totalAvgScansSum += summary.AverageDailyScans;
                countedMonths++;
            }

            result.OverallAverageDailyUniqueDevices = countedMonths > 0 ? totalAvgUsersSum / countedMonths : 0;
            result.OverallAverageDailyScans = countedMonths > 0 ? totalAvgScansSum / countedMonths : 0;

            callback?.Invoke(true, result);
        });
    }

    // NEW: Load monthly averages starting from Oct 2025 up to the last recorded day (last time AR was used).
    // For each month:
    // - totalUnique = sum(uniqueDevices for days within that month)
    // - daysConsidered = full month days except for the last month where daysConsidered = lastUsedDayOfMonth
    // - average = totalUnique / daysConsidered
    public void LoadMonthlyAveragesFromOct2025(string galleryId, Action<bool, List<MonthlySummary>> callback)
    {
        if (db == null) { callback?.Invoke(false, null); return; }
        if (string.IsNullOrEmpty(galleryId)) galleryId = "NP Art gallery";

        var startDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var startStr = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Query all daily docs from Oct 1, 2025 upwards
        var query = db.Collection("Galleries").Document(galleryId).Collection("daily")
                      .WhereGreaterThanOrEqualTo("date", startStr);

        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[Analytics] LoadMonthlyAveragesFromOct2025 failed: {task.Exception}");
                callback?.Invoke(false, null);
                return;
            }

            var snapshot = task.Result;
            if (snapshot.Count == 0)
            {
                callback?.Invoke(false, null);
                return;
            }

            // Build a map of dateStr -> uniqueDevices and find the latest date present
            var map = new Dictionary<string, long>(StringComparer.Ordinal);
            DateTime latestDate = DateTime.MinValue;
            foreach (var doc in snapshot.Documents)
            {
                try
                {
                    var dict = doc.ToDictionary();
                    var dateStr = dict.ContainsKey("date") ? dict["date"]?.ToString() : doc.Id;
                    if (string.IsNullOrEmpty(dateStr)) continue;

                    var parsed = DateTime.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    if (parsed > latestDate) latestDate = parsed;

                    long uniqueDevices = 0;
                    if (dict.TryGetValue("uniqueDevices", out var ud)) uniqueDevices = Convert.ToInt64(ud);

                    map[dateStr] = uniqueDevices;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Analytics] Skipping doc {doc.Id} while building monthly averages: {ex}");
                }
            }

            if (latestDate == DateTime.MinValue)
            {
                callback?.Invoke(false, null);
                return;
            }

            // Build monthly summaries from Oct 2025 to the month of latestDate inclusive
            var summaries = new List<MonthlySummary>();
            var monthCursor = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var endMonth = new DateTime(latestDate.Year, latestDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            for (var m = monthCursor; m <= endMonth; m = m.AddMonths(1))
            {
                var keyMonth = m.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
                int daysConsidered = daysInMonth;
                // If this is the last month, only count days up to latestDate.Day
                if (m.Year == latestDate.Year && m.Month == latestDate.Month)
                    daysConsidered = latestDate.Day;

                long totalUnique = 0;
                long totalScans = 0;

                // Sum across days in that month; missing days count as 0
                for (int day = 1; day <= daysConsidered; day++)
                {
                    var dateStr = new DateTime(m.Year, m.Month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (map.TryGetValue(dateStr, out var ud))
                        totalUnique += ud;

                    // scans not required here for the user's request, but attempt to read if present
                    // (we didn't store scans map in map; if needed, a separate scan map can be populated above)
                }

                summaries.Add(new MonthlySummary
                {
                    MonthLabel = keyMonth,
                    Year = m.Year,
                    Month = m.Month,
                    DaysInMonth = daysInMonth,
                    DaysConsidered = daysConsidered,
                    TotalUniqueDevices = totalUnique,
                    TotalScans = totalScans
                });
            }

            callback?.Invoke(true, summaries);
        });
    }

    void LoadTodayAndMonthlyOverview(string galleryId, Action<DailyAnalytics, MonthlyAveragesResult> onComplete)
    {
        var today = DateTime.UtcNow.Date;
        LoadDaily(galleryId, today, (okDay, dayData) =>
        {
            LoadLastNMonthsAverages(galleryId, monthsToAverage, (okMonths, monthsData) =>
            {
                onComplete?.Invoke(dayData, monthsData);
            });
        });
    }

    #endregion

    #region UI helpers

    void SetTmpro(TextMeshProUGUI tmp, string s)
    {
        if (tmp != null) tmp.text = s;
    }

    void SetAllUiLoading()
    {
        SetTmpro(dailyUsersTMP, "Loading...");
        SetTmpro(dailyScansTMP, "Loading...");
        SetTmpro(monthlyAvgUsersTMP, "Loading...");
        SetTmpro(monthlyAvgScansTMP, "Loading...");
        SetTmpro(monthlyAveragesListTMP, "Loading...");
    }

    void SetAllUiUnavailable()
    {
        SetTmpro(dailyUsersTMP, "—");
        SetTmpro(dailyScansTMP, "—");
        SetTmpro(monthlyAvgUsersTMP, "—");
        SetTmpro(monthlyAvgScansTMP, "—");
        SetTmpro(monthlyAveragesListTMP, "—");
    }

    void UpdateDailyUi(DailyAnalytics day)
    {
        if (day == null)
        {
            SetAllUiUnavailable();
            return;
        }

        SetTmpro(dailyUsersTMP, day.UniqueDevices.ToString());
        SetTmpro(dailyScansTMP, day.Scans.ToString());
    }

    void UpdateMonthlyUi(MonthlyAveragesResult months)
    {
        if (months == null)
        {
            SetTmpro(monthlyAvgUsersTMP, "—");
            SetTmpro(monthlyAvgScansTMP, "—");
            return;
        }

        SetTmpro(monthlyAvgUsersTMP, months.OverallAverageDailyUniqueDevices.ToString("F1", CultureInfo.InvariantCulture));
        SetTmpro(monthlyAvgScansTMP, months.OverallAverageDailyScans.ToString("F1", CultureInfo.InvariantCulture));
    }

    void RenderMonthlyAveragesList(List<MonthlySummary> list)
    {
        if (monthlyAveragesListTMP == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Month — Avg users/day (total users)");

        foreach (var s in list)
        {
            sb.AppendFormat("{0}: {1} ({2})",
                s.MonthLabel,
                s.AverageDailyUniqueDevices.ToString("F1", CultureInfo.InvariantCulture),
                s.TotalUniqueDevices);
            sb.AppendLine();
        }

        SetTmpro(monthlyAveragesListTMP, sb.ToString());
    }

    #endregion
}
