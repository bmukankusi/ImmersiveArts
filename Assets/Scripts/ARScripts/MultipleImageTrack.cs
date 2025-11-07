using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class MultipleImageTrack : MonoBehaviour
{
    [System.Serializable]
    public struct NamedPrefab
    {
        public string imageName;
        public GameObject prefab;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public float scaleMultiplier;
        public float minViewTime;    // Minimum seconds to count as a "view"
        public bool logScan;         // Whether to log a "scan" the first time the image is detected
    }

    public List<NamedPrefab> imagePrefabs = new List<NamedPrefab>();

    ARTrackedImageManager _trackedImageManager;
    Dictionary<string, GameObject> _instantiated = new Dictionary<string, GameObject>();
    Dictionary<string, bool> _wasTracking = new Dictionary<string, bool>();

    // Track whether we created the instance ourselves (so we can safely Destroy it).
    HashSet<string> _createdByUs = new HashSet<string>();

    const string GalleryId = "NP Art gallery";

    // Per-image runtime tracking state (view sessions & counts)
    class TrackingState
    {
        public bool isTracking;
        public float startTime;
        public int viewCount;
        public bool hasLoggedScan;
    }
    Dictionary<string, TrackingState> _tracking = new Dictionary<string, TrackingState>();

    void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        if (_trackedImageManager == null)
            Debug.LogWarning("ARTrackedImageManager not found on GameObject. Please add one.", this);
    }

    void OnEnable()
    {
        if (_trackedImageManager != null)
            _trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        else
            Debug.LogWarning("OnEnable: ARTrackedImageManager is null. Subscription skipped.", this);
    }

    void OnDisable()
    {
        if (_trackedImageManager != null)
            _trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var added in eventArgs.added)
            UpdateImageSafe(added);

        foreach (var updated in eventArgs.updated)
            UpdateImageSafe(updated);

        foreach (var removed in eventArgs.removed)
            RemoveImageSafe(removed);
    }

    void UpdateImageSafe(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
            return;

        try
        {
            UpdateImage(trackedImage);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"UpdateImage exception for {(trackedImage.referenceImage != null ? trackedImage.referenceImage.name : "null")}: {ex}", this);
        }
    }

    void UpdateImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage == null)
        {
            Debug.LogWarning("TrackedImage has no referenceImage.", this);
            return;
        }

        var name = trackedImage.referenceImage.name;
        var mapping = imagePrefabs.Find(x => x.imageName == name);
        if (mapping.prefab == null)
            return;

        bool hadWasTrackingEntry = _wasTracking.ContainsKey(name);

        // Instantiate or reuse existing instance
        if (!_instantiated.ContainsKey(name))
        {
            GameObject go = null;

            // If ARFoundation already created a prefab instance for this trackedImage (via trackedImagePrefab),
            // it will be part of the trackedImage GameObject hierarchy. Reuse that so we don't create duplicates.
            var existingVp = trackedImage.GetComponentInChildren<VideoPlayer>();
            if (existingVp != null)
            {
                go = existingVp.gameObject;
                // We did not create this instance
                _createdByUs.Remove(name);
            }
            else
            {
                // Create our own child under the tracked image
                go = Instantiate(mapping.prefab, trackedImage.transform);
                _createdByUs.Add(name);
            }

            ApplyOffsetsAndScale(go.transform, trackedImage.size, mapping);

            var vp = go.GetComponentInChildren<VideoPlayer>();
            if (vp != null)
            {
                vp.playOnAwake = false;
                vp.isLooping = true;
                vp.Pause();
            }
            else
            {
                Debug.LogWarning($"Prefab for '{name}' does not contain a VideoPlayer component.", go);
            }

            _instantiated[name] = go;
        }

        var instance = _instantiated[name];
        if (instance == null)
        {
            _instantiated.Remove(name);
            return;
        }

        bool isTracking = trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking;
        bool wasTracking = false;
        _wasTracking.TryGetValue(name, out wasTracking);

        // Ensure there is a tracking state object
        var state = GetOrCreateState(name);

        // If this is the first time we see this image (no previous _wasTracking entry), optionally log a "scan"
        if (!hadWasTrackingEntry && mapping.logScan && !state.hasLoggedScan)
        {
            state.hasLoggedScan = true;
            if (AnalyticsManager.Instance != null)
            {
                // use the reference image name as the stable artwork id (set this to match Firestore doc id / slug)
                AnalyticsManager.Instance.LogInteraction(name, name, 0f, "scan");
            }
        }

        _wasTracking[name] = isTracking;

        if (isTracking)
        {
            instance.SetActive(true);
            instance.transform.SetParent(trackedImage.transform, false);
            ApplyOffsetsAndScale(instance.transform, trackedImage.size, mapping);

            var vp = instance.GetComponentInChildren<VideoPlayer>();
            if (vp != null && !vp.isPlaying)
                vp.Play();

            // Start a view session if we weren't tracking previously
            if (!wasTracking)
                StartViewSession(name, mapping);
        }
        else
        {
            var vp = instance.GetComponentInChildren<VideoPlayer>();
            if (vp != null && vp.isPlaying)
                vp.Pause();

            // End view session when tracking is lost
            if (wasTracking)
                EndViewSession(name, mapping);

            instance.SetActive(false);
        }
    }

    void RemoveImageSafe(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
            return;

        try
        {
            RemoveImage(trackedImage);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RemoveImage exception for {(trackedImage.referenceImage != null ? trackedImage.referenceImage.name : "null")}: {ex}", this);
        }
    }

    void RemoveImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage == null)
            return;

        var name = trackedImage.referenceImage.name;

        // If it was tracking, end the session
        bool wasTracking = false;
        _wasTracking.TryGetValue(name, out wasTracking);
        var mapping = imagePrefabs.Find(x => x.imageName == name);
        if (wasTracking)
            EndViewSession(name, mapping);

        if (_instantiated.TryGetValue(name, out var instance))
        {
            if (instance != null)
            {
                var vp = instance.GetComponentInChildren<VideoPlayer>();
                if (vp != null)
                    vp.Stop();

                // Only destroy instances we created. If ARFoundation created the instance (trackedImagePrefab),
                // we should not Destroy it here; just deactivate it.
                if (_createdByUs.Contains(name))
                    Destroy(instance);
                else
                    instance.SetActive(false);
            }

            _instantiated.Remove(name);
        }

        if (_wasTracking.ContainsKey(name))
            _wasTracking.Remove(name);

        if (_createdByUs.Contains(name))
            _createdByUs.Remove(name);

        if (_tracking.ContainsKey(name))
            _tracking.Remove(name);
    }

    void ApplyOffsetsAndScale(Transform t, Vector2 imageSize, NamedPrefab mapping)
    {
        if (t == null)
            return;

        var multiplier = mapping.scaleMultiplier == 0f ? 1f : mapping.scaleMultiplier;
        var scale = new Vector3(imageSize.x * multiplier,
                                imageSize.y * multiplier,
                                1f);

        t.localPosition = mapping.positionOffset;
        t.localEulerAngles = mapping.rotationOffset;
        t.localScale = scale;
    }

    TrackingState GetOrCreateState(string name)
    {
        if (!_tracking.TryGetValue(name, out var state))
        {
            state = new TrackingState { isTracking = false, startTime = 0f, viewCount = 0, hasLoggedScan = false };
            _tracking[name] = state;
        }
        return state;
    }

    void StartViewSession(string name, NamedPrefab mapping)
    {
        var state = GetOrCreateState(name);
        if (state.isTracking) return;

        state.isTracking = true;
        state.startTime = Time.time;

        // Ensure instance visible
        if (_instantiated.TryGetValue(name, out var instance) && instance != null)
            instance.SetActive(true);

        // Start video if present
        if (_instantiated.TryGetValue(name, out var inst) && inst != null)
        {
            var vp = inst.GetComponentInChildren<VideoPlayer>();
            if (vp != null && !vp.isPlaying) vp.Play();
        }

        Debug.Log($"[MultipleImageTrack] Start view session: {name}");
    }

    void EndViewSession(string name, NamedPrefab mapping)
    {
        var state = GetOrCreateState(name);
        if (!state.isTracking) return;

        state.isTracking = false;
        float duration = Time.time - state.startTime;
        float minView = (mapping.prefab != null) ? mapping.minViewTime : 0f;
        if (minView <= 0f) minView = 3f; // default fallback

        // Stop video if present
        if (_instantiated.TryGetValue(name, out var instance) && instance != null)
        {
            var vp = instance.GetComponentInChildren<VideoPlayer>();
            if (vp != null && vp.isPlaying) vp.Pause();
        }

        if (duration >= minView)
        {
            state.viewCount++;
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogInteraction(name, name, duration, "view");
            }
            Debug.Log($"[MultipleImageTrack] Recorded view for {name}. duration={duration:F1}s totalViews={state.viewCount}");
        }
        else
        {
            Debug.Log($"[MultipleImageTrack] Ignored short view for {name}. duration={duration:F1}s (min {minView}s)");
        }
    }
}