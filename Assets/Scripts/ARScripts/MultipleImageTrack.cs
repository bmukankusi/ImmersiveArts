using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


/// <summary>
/// Manages the tracking of multiple AR reference images and dynamically instantiates associated prefabs.
/// </summary>
/// <remarks>This component listens for changes in the tracking state of AR reference images using the <see
/// cref="ARTrackedImageManager"/>. When a reference image is detected, it instantiates a corresponding prefab, applies
/// position, rotation, and scale offsets, and manages the prefab's visibility and behavior based on the tracking state.
/// Prefabs are expected to include a <see cref="VideoPlayer"/> component for playback control, though this is optional.
/// To use this component, attach it to a GameObject with an <see cref="ARTrackedImageManager"/> component. Configure
/// the <see cref="imagePrefabs"/> list in the Inspector to map reference image names to prefabs.</remarks>

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
        public float scaleMultiplier;         // adjust to fine-tune quad size
    }

    [Tooltip("Map reference image names to prefabs (quad with VideoPlayer).")]
    public List<NamedPrefab> imagePrefabs = new List<NamedPrefab>();

    ARTrackedImageManager _trackedImageManager;
    Dictionary<string, GameObject> _instantiated = new Dictionary<string, GameObject>();

    // Track last known tracking state so as to detect transitions 
    // and record an AR scan each time the user points the camera at the artwork.
    Dictionary<string, bool> _wasTracking = new Dictionary<string, bool>();

    // Gallery id used when recording scans
    const string GalleryId = "NP Art gallery";

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

    // Wrap UpdateImage in a safe method so we can catch and log unexpected errors without crashing other systems.
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
            Debug.LogError($"UpdateImage exception for {trackedImage.referenceImage.name}: {ex}", this);
        }
    }

    void UpdateImage(ARTrackedImage trackedImage)
    {
        // Sanity check
        if (trackedImage.referenceImage == null)
        {
            Debug.LogWarning("TrackedImage has no referenceImage.", this);
            return;
        }

        var name = trackedImage.referenceImage.name;

        // Find mapping
        var mapping = imagePrefabs.Find(x => x.imageName == name);
        if (mapping.prefab == null)
        {
            // No prefab mapped for this image 
            return;
        }

        // Instantiate if not already
        if (!_instantiated.ContainsKey(name))
        {
            var go = Instantiate(mapping.prefab, trackedImage.transform);
            // Apply inspector offsets and sizing immediately
            ApplyOffsetsAndScale(go.transform, trackedImage.size, mapping);

            // Ensure VideoPlayer is configured and paused until tracking confirmed
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

        // Activate / position depending on tracking state
        var instance = _instantiated[name];
        if (instance == null)
        {
            _instantiated.Remove(name);
            return;
        }

        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;
        bool wasTracking = false;
        _wasTracking.TryGetValue(name, out wasTracking);

        //  not-tracking - tracking (counts as one AR scan)
        if (isTracking && !wasTracking)
        {
            try
            {
                if (SaveDataToFirestore.Instance != null)
                {
                    // use the reference image name as artworkId
                    SaveDataToFirestore.Instance.RecordARScan(GalleryId, name);
                }
                else
                {
                    Debug.LogWarning("[MultipleImageTrack] SaveDataToFirestore.Instance not found. AR scan not recorded.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recording AR scan for '{name}': {ex}");
            }
        }

        // Update stored tracking state
        _wasTracking[name] = isTracking;

        if (isTracking)
        {
            instance.SetActive(true);
            // Parent to tracked image so it follows pose; keep local transform values
            instance.transform.SetParent(trackedImage.transform, false);

            // Update offsets/scale each update in case trackedImage.size or inspector changed
            ApplyOffsetsAndScale(instance.transform, trackedImage.size, mapping);

            var vp = instance.GetComponentInChildren<VideoPlayer>();
            if (vp != null && !vp.isPlaying)
                vp.Play();
        }
        else
        {
            // pause and hide when not tracking
            var vp = instance.GetComponentInChildren<VideoPlayer>();
            if (vp != null && vp.isPlaying)
                vp.Pause();

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
            Debug.LogError($"RemoveImage exception for {trackedImage.referenceImage.name}: {ex}", this);
        }
    }

    void RemoveImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage == null)
            return;

        var name = trackedImage.referenceImage.name;
        if (_instantiated.TryGetValue(name, out var instance))
        {
            if (instance != null)
            {
                var vp = instance.GetComponentInChildren<VideoPlayer>();
                if (vp != null)
                    vp.Stop();

                Destroy(instance);
            }

            _instantiated.Remove(name);
        }

        // Clear tracking state so a future redetection will be counted again
        if (_wasTracking.ContainsKey(name))
            _wasTracking.Remove(name);
    }

    // Helper: apply inspector controlled local transform and scale based on tracked image physical size
    void ApplyOffsetsAndScale(Transform t, Vector2 imageSize, NamedPrefab mapping)
    {
        if (t == null)
            return;

        // Quad is 1x1 (x = width, y = height). Z is 1 for quad thickness.
        var multiplier = mapping.scaleMultiplier == 0f ? 1f : mapping.scaleMultiplier;
        var scale = new Vector3(imageSize.x * multiplier,
                                imageSize.y * multiplier,
                                1f);

        t.localPosition = mapping.positionOffset;
        t.localEulerAngles = mapping.rotationOffset;
        t.localScale = scale;
    }
}
