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
        public string imageName;              // exact name from your Reference Image Library
        public GameObject prefab;             // prefab with VideoPlayer (use a Quad)
        [Tooltip("Local position offset (in tracked-image local space).")]
        public Vector3 positionOffset;        // inspector-controlled local offset
        [Tooltip("Local Euler rotation offset (degrees, tracked-image local space).")]
        public Vector3 rotationOffset;        // inspector-controlled local rotation
        [Tooltip("Scale multiplier applied to the tracked image physical size (Quad is 1x1).")]
        public float scaleMultiplier;         // adjust to fine-tune quad size
    }

    [Tooltip("Map reference image names to prefabs (quad with VideoPlayer).")]
    public List<NamedPrefab> imagePrefabs = new List<NamedPrefab>();

    ARTrackedImageManager _trackedImageManager;
    Dictionary<string, GameObject> _instantiated = new Dictionary<string, GameObject>();

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
        // defensive: iterate safely
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
        // defensive null checks
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
            // No prefab mapped for this image — this is expected if you haven't set it in inspector
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

        if (trackedImage.trackingState == TrackingState.Tracking)
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
    }

    // Helper: apply inspector-controlled local transform and scale based on tracked image physical size.
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
