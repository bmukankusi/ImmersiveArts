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
    }

    public List<NamedPrefab> imagePrefabs = new List<NamedPrefab>();

    ARTrackedImageManager _trackedImageManager;
    Dictionary<string, GameObject> _instantiated = new Dictionary<string, GameObject>();
    Dictionary<string, bool> _wasTracking = new Dictionary<string, bool>();

    // Track whether we created the instance ourselves (so we can safely Destroy it).
    HashSet<string> _createdByUs = new HashSet<string>();

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

        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;
        bool wasTracking = false;
        _wasTracking.TryGetValue(name, out wasTracking);

        if (isTracking && !wasTracking)
        {
            try
            {
                if (SaveDataToFirestore.Instance != null)
                    SaveDataToFirestore.Instance.RecordARScan(GalleryId, name);
                else
                    Debug.LogWarning("[MultipleImageTrack] SaveDataToFirestore.Instance not found. AR scan not recorded.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error recording AR scan for '{name}': {ex}");
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
        }
        else
        {
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
            Debug.LogError($"RemoveImage exception for {(trackedImage.referenceImage != null ? trackedImage.referenceImage.name : "null")}: {ex}", this);
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
}
