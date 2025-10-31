using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine;

public class DleeteData : MonoBehaviour // DeleteData: misspelled class name to match file name
{
    FirebaseFirestore db;

    void Awake()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
                db = FirebaseFirestore.DefaultInstance;
            else
                Debug.LogError($"Firebase dependencies not available: {task.Result}");
        });
    }

    // Called via SendMessage from LoadData 
    // Signature must be: void DeleteArtwork(string id)
    public void DeleteArtwork(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("DeleteArtwork called with null/empty id.");
            return;
        }

        if (db == null)
        {
            db = FirebaseFirestore.DefaultInstance;
            if (db == null)
            {
                Debug.LogError("Firestore not initialized.");
                return;
            }
        }

        Debug.Log($"Deleting artwork {id}...");

        db.Collection("Artworks").Document(id).DeleteAsync().ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted)
            {
                Debug.LogError($"Failed to delete artwork {id}: {t.Exception}");
            }
            else
            {
                Debug.Log($"Artwork {id} deleted successfully.");
            }
        });
    }
}
