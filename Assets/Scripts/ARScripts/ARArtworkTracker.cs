using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

/// <summary>
/// Monitors ARTrackedImageManager events to log interaction analytics for detected artworks.
/// This script should be attached to the GameObject that has the ARTrackedImageManager component.
/// </summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public class ARArtworkTracker : MonoBehaviour
{
    // The component we will subscribe to for tracking events
    private ARTrackedImageManager trackedImageManager;

    // Key: The name of the reference image (e.g., "MonaLisa")
    // Value: The Time.time when tracking started for that specific image
    private Dictionary<string, float> activeInteractionTimers = new Dictionary<string, float>();

    /// <summary>
    /// Initialize the manager and subscribe to AR Foundation events.
    /// </summary>
    void Awake()
    {
        // Get the ARTrackedImageManager component from this GameObject
        trackedImageManager = GetComponent<ARTrackedImageManager>();

        if (trackedImageManager == null)
        {
            Debug.LogError("ARArtworkTracker requires an ARTrackedImageManager component on the same GameObject.");
            return;
        }

        // Subscribe to the event that fires when tracked images are added, updated, or removed
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;

        Debug.Log("ARArtworkTracker initialized and subscribed to AR Foundation events.");
    }

    /// <summary>
    /// Unsubscribe from events to prevent memory leaks when the object is destroyed.
    /// </summary>
    void OnDestroy()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
        // Ensure any remaining timers are cleared
        activeInteractionTimers.Clear();
    }

    /// <summary>
    /// The event handler for ARTrackedImageManager's changes.
    /// </summary>
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // 1. Handle newly detected images (Tracking Started)
        foreach (var newImage in eventArgs.added)
        {
            HandleImageAppeared(newImage);
        }

        // 2. Handle images that are no longer visible or removed (Tracking Stopped)
        foreach (var removedImage in eventArgs.removed)
        {
            HandleImageDisappeared(removedImage);
        }
    }

    /// <summary>
    /// Logs a 'scan' interaction when a new image target is found.
    /// </summary>
    private void HandleImageAppeared(ARTrackedImage image)
    {
        string artworkName = GetArtworkName(image);

        if (string.IsNullOrEmpty(artworkName)) return;

        if (activeInteractionTimers.ContainsKey(artworkName))
        {
            // Already tracking, ignore.
            return;
        }

        // 1. Record the start time of the interaction.
        activeInteractionTimers[artworkName] = Time.time;

        // 2. Log the 'scan' or 'target_found' event (zero duration).
        // It uses the AnalyticsManager singleton instance.
        if (AnalyticsManager.Instance != null && AnalyticsManager.Instance.IsInitialized)
        {
            AnalyticsManager.Instance.LogInteraction(
                artworkId: artworkName,
                artworkName: artworkName,
                duration: 0f,
                interactionType: "scan"
            );
            Debug.Log($"[Analytics] Image Tracked & Scan Logged: {artworkName}");
        }
        else
        {
            Debug.LogWarning($"Analytics not initialized. Started tracking {artworkName} but could not log scan.");
        }
    }

    /// <summary>
    /// Logs a 'view' interaction with duration when an image target tracking is lost.
    /// </summary>
    private void HandleImageDisappeared(ARTrackedImage image)
    {
        string artworkName = GetArtworkName(image);

        if (string.IsNullOrEmpty(artworkName)) return;

        if (activeInteractionTimers.TryGetValue(artworkName, out float startTime))
        {
            // 1. Calculate the duration.
            float duration = Time.time - startTime;

            // 2. Log the 'view' event with the duration.
            // It uses the AnalyticsManager singleton instance.
            if (AnalyticsManager.Instance != null && AnalyticsManager.Instance.IsInitialized)
            {
                AnalyticsManager.Instance.LogInteraction(
                    artworkId: artworkName,
                    artworkName: artworkName,
                    duration: duration,
                    interactionType: "view"
                );
                Debug.Log($"[Analytics] Tracking Lost & View Logged: {artworkName} ({duration:F2}s)");
            }
            else
            {
                Debug.LogWarning($"Analytics not initialized. Tracking lost for {artworkName} after {duration:F2}s. No log created.");
            }

            // 3. Remove the timer.
            activeInteractionTimers.Remove(artworkName);
        }
    }

    /// <summary>
    /// Utility to safely get the name of the tracked image.
    /// </summary>
    private string GetArtworkName(ARTrackedImage image)
    {
        // Use the name of the reference image from the Library
        if (image.referenceImage != null)
        {
            return image.referenceImage.name;
        }
        // Fallback (should be avoided by ensuring referenceImage is available)
        return image.gameObject.name;
    }
}