using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Minimal Firestore recorder:
/// - RecordProceed(galleryId) : records a unique device visit (once per UTC day) under Galleries/{galleryId}/daily/{yyyy-MM-dd}
/// - RecordARScan(galleryId, artworkId) : increments daily scan counter and per-artwork counter under Artworks subcollection
/// </summary>
public class SaveDataToFirestore : MonoBehaviour
{
    public static SaveDataToFirestore Instance { get; private set; }

    FirebaseFirestore db;
    bool firebaseReady = false;

    // Default gallery id 
    [Tooltip("Default gallery id used when no gallery id is provided")]
    public string defaultGalleryId = "NP Art gallery";

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeFirebase();
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                firebaseReady = true;
                Debug.Log("[SaveDataToFirestore] Firebase initialized.");
            }
            else
            {
                firebaseReady = false;
                Debug.LogError($"[SaveDataToFirestore] Firebase dependency error: {status}");
            }
        });
    }

    // Record that the device proceeded into the AR experience (unique device counted once per UTC day)
    public void RecordProceed(string galleryId = null)
    {
        if (string.IsNullOrEmpty(galleryId)) galleryId = defaultGalleryId;
        if (!firebaseReady)
        {
            Debug.LogWarning("[SaveDataToFirestore] Firebase not ready. Skipping RecordProceed.");
            return;
        }

        string deviceId = GetDeviceId();
        string date = UtcDateString();
        var docRef = db.Collection("Galleries").Document(galleryId).Collection("daily").Document(date);

        db.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef).ConfigureAwait(false);
            if (snapshot.Exists)
            {
                // check devices map
                var dict = snapshot.ToDictionary();
                bool devicePresent = false;
                if (dict.TryGetValue("devices", out var devicesObj) && devicesObj is Dictionary<string, object> devicesMap)
                {
                    devicePresent = devicesMap.ContainsKey(deviceId);
                }

                if (!devicePresent)
                {
                    var updates = new Dictionary<string, object>
                    {
                        { $"devices.{deviceId}", true },
                        { "uniqueDevices", FieldValue.Increment(1L) }
                    };
                    transaction.Update(docRef, updates);
                }
            }
            else
            {
                var initial = new Dictionary<string, object>
                {
                    { "date", date },
                    { "uniqueDevices", 1L },
                    { "scans", 0L },
                    { "devices", new Dictionary<string, object> { { deviceId, true } } }
                };
                transaction.Set(docRef, initial);
            }
        }).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted) Debug.LogError($"[SaveDataToFirestore] RecordProceed failed: {t.Exception}");
            else Debug.Log("[SaveDataToFirestore] RecordProceed successful.");
        });
    }

    // Record an AR scan (increments daily scans and optionally per-artwork counter).
    // Uses collection names: Galleries/{galleryId}/daily/{date}/Artworks/{artworkId}
    public void RecordARScan(string galleryId = null, string artworkId = null)
    {
        if (string.IsNullOrEmpty(galleryId)) galleryId = defaultGalleryId;
        if (!firebaseReady)
        {
            Debug.LogWarning("[SaveDataToFirestore] Firebase not ready. Skipping RecordARScan.");
            return;
        }

        string date = UtcDateString();
        var docRef = db.Collection("Galleries").Document(galleryId).Collection("daily").Document(date);

        db.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef).ConfigureAwait(false);
            if (snapshot.Exists)
            {
                transaction.Update(docRef, new Dictionary<string, object>
                {
                    { "scans", FieldValue.Increment(1L) }
                });
            }
            else
            {
                var initial = new Dictionary<string, object>
                {
                    { "date", date },
                    { "uniqueDevices", 0L },
                    { "scans", 1L },
                    { "devices", new Dictionary<string, object>() }
                };
                transaction.Set(docRef, initial);
            }
        }).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted) Debug.LogError($"[SaveDataToFirestore] RecordARScan (daily) failed: {t.Exception}");
            else Debug.Log("[SaveDataToFirestore] RecordARScan (daily) updated.");
        });

        // Per artwork increment 
        if (!string.IsNullOrEmpty(artworkId))
        {
            var artworkDoc = db.Collection("Galleries").Document(galleryId)
                               .Collection("daily").Document(date)
                               .Collection("Artworks").Document(artworkId);

            artworkDoc.UpdateAsync(new Dictionary<string, object>
            {
                { "scans", FieldValue.Increment(1L) },
                { "lastSeenUtc", Timestamp.GetCurrentTimestamp() }
            }).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                {
                    // If update fails create it
                    var fallback = new Dictionary<string, object>
                    {
                        { "scans", 1L },
                        { "lastSeenUtc", Timestamp.GetCurrentTimestamp() }
                    };
                    artworkDoc.SetAsync(fallback).ContinueWithOnMainThread(setTask =>
                    {
                        if (setTask.IsFaulted) Debug.LogError($"[SaveDataToFirestore] Failed to set artwork doc: {setTask.Exception}");
                    });
                }
            });
        }
    }

    DocumentReference GetDailyDocRef(string galleryId, string dateString)
    {
        return db.Collection("Galleries").Document(galleryId).Collection("daily").Document(dateString);
    }

    static string UtcDateString() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    string GetDeviceId()
    {
        var id = SystemInfo.deviceUniqueIdentifier;
        if (!string.IsNullOrEmpty(id)) return id;
        var fallback = $"{SystemInfo.deviceModel}_{SystemInfo.deviceType}_{SystemInfo.operatingSystem}";
        return fallback.GetHashCode().ToString("X");
    }
}
