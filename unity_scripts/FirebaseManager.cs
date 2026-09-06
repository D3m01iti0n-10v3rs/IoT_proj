using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using System.Threading.Tasks;

public class FirebaseDataManager : MonoBehaviour
{
    [Header("Firebase Configuration")]
    public string databaseURL = "";

    private DatabaseReference databaseReference;
    private FirebaseApp firebaseApp;
    private bool isInitialized = false;

    // Events for connection status and data updates
    public static event Action<bool> OnFirebaseInitialized;
    public static event Action<string, string> OnDataReceived;
    public static event Action<string> OnDataSaved;
    public static event Action<string> OnError;

    // Use HashSet for faster lookups
    private HashSet<string> activeListeners = new HashSet<string>();
    private Dictionary<string, DatabaseReference> listenerReferences = new Dictionary<string, DatabaseReference>();

    // Use ConcurrentQueue for thread safety without lock
    private System.Collections.Concurrent.ConcurrentQueue<Action> mainThreadQueue = new System.Collections.Concurrent.ConcurrentQueue<Action>();

    private const string NOT_INITIALIZED_ERROR = "Firebase not initialized";
    private const string INITIALIZATION_SUCCESS = "Firebase initialized successfully";

    void Start()
    {
        InitializeFirebase();
    }

    void Update()
    {
        int processed = 0;
        while (mainThreadQueue.TryDequeue(out Action action) && processed < 10)
        {
            action?.Invoke();
            processed++;
        }
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                firebaseApp = FirebaseApp.DefaultInstance;
                databaseReference = FirebaseDatabase.GetInstance(databaseURL).RootReference;
                isInitialized = true;

                Debug.Log(INITIALIZATION_SUCCESS);
                QueueOnMainThread(() => OnFirebaseInitialized?.Invoke(true));
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                QueueOnMainThread(() => 
                {
                    OnFirebaseInitialized?.Invoke(false);
                    OnError?.Invoke($"Firebase dependencies failed: {dependencyStatus}");
                });
            }
        });
    }

    public async void SendData(string path, object data)
    {
        if (!isInitialized)
        {
            QueueOnMainThread(() => OnError?.Invoke(NOT_INITIALIZED_ERROR));
            return;
        }

        try
        {
            await databaseReference.Child(path).SetValueAsync(data);
            QueueOnMainThread(() => OnDataSaved?.Invoke(path));
        }
        catch (Exception ex)
        {
            QueueOnMainThread(() => OnError?.Invoke($"Send failed: {ex.Message}"));
        }
    }

    public async void GetData(string path)
    {
        if (!isInitialized)
        {
            QueueOnMainThread(() => OnError?.Invoke(NOT_INITIALIZED_ERROR));
            return;
        }

        try
        {
            DataSnapshot snapshot = await databaseReference.Child(path).GetValueAsync();
            
            if (snapshot.Exists)
            {
                string jsonData = snapshot.GetRawJsonValue();
                QueueOnMainThread(() => OnDataReceived?.Invoke(path, jsonData));
            }
            else
            {
                QueueOnMainThread(() => OnDataReceived?.Invoke(path, null));
            }
        }
        catch (Exception ex)
        {
            QueueOnMainThread(() => OnError?.Invoke($"Get failed: {ex.Message}"));
        }
    }

    public void ListenForData(string path)
    {
        if (!isInitialized)
        {
            QueueOnMainThread(() => OnError?.Invoke(NOT_INITIALIZED_ERROR));
            return;
        }

        try
        {
            // Check if already listening
            if (activeListeners.Contains(path)) return;

            DatabaseReference listenerRef = databaseReference.Child(path);
            listenerRef.ValueChanged += HandleDataChanged;
            activeListeners.Add(path);
            listenerReferences[path] = listenerRef;
        }
        catch (Exception ex)
        {
            QueueOnMainThread(() => OnError?.Invoke($"Listener failed: {ex.Message}"));
        }
    }

    public void StopListening(string path)
    {
        if (!isInitialized) return;

        try
        {
            if (activeListeners.Contains(path) && listenerReferences.ContainsKey(path))
            {
                listenerReferences[path].ValueChanged -= HandleDataChanged;
                activeListeners.Remove(path);
                listenerReferences.Remove(path);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error stopping listener: {ex.Message}");
        }
    }

    public void StopAllListening()
    {
        foreach (string path in activeListeners)
        {
            if (listenerReferences.ContainsKey(path))
            {
                listenerReferences[path].ValueChanged -= HandleDataChanged;
            }
        }
        activeListeners.Clear();
        listenerReferences.Clear();
    }

    private void HandleDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            QueueOnMainThread(() => OnError?.Invoke($"Data change error: {args.DatabaseError.Message}"));
            return;
        }

        if (args.Snapshot.Exists)
        {
            string jsonData = args.Snapshot.GetRawJsonValue();
            string path = GetPathFromSnapshot(args.Snapshot);
            QueueOnMainThread(() => OnDataReceived?.Invoke(path, jsonData));
        }
        else
        {
            string path = GetPathFromSnapshot(args.Snapshot);
            QueueOnMainThread(() => OnDataReceived?.Invoke(path, null));
        }
    }

    private string GetPathFromSnapshot(DataSnapshot snapshot)
    {
        System.Text.StringBuilder pathBuilder = new System.Text.StringBuilder();
        DatabaseReference currentRef = snapshot.Reference;
        
        while (currentRef != null && currentRef != databaseReference)
        {
            string currentKey = currentRef.Key;
            if (!string.IsNullOrEmpty(currentKey))
            {
                if (pathBuilder.Length > 0)
                    pathBuilder.Insert(0, '/');
                pathBuilder.Insert(0, currentKey);
            }
            currentRef = currentRef.Parent;
        }
        
        return pathBuilder.ToString();
    }

    private void QueueOnMainThread(Action action)
    {
        mainThreadQueue.Enqueue(action);
    }

    public async void UpdateData(string path, Dictionary<string, object> updates)
    {
        if (!isInitialized)
        {
            QueueOnMainThread(() => OnError?.Invoke(NOT_INITIALIZED_ERROR));
            return;
        }

        try
        {
            await databaseReference.Child(path).UpdateChildrenAsync(updates);
            QueueOnMainThread(() => OnDataSaved?.Invoke(path));
        }
        catch (Exception ex)
        {
            QueueOnMainThread(() => OnError?.Invoke($"Update failed: {ex.Message}"));
        }
    }

    public async void DeleteData(string path)
    {
        if (!isInitialized)
        {
            QueueOnMainThread(() => OnError?.Invoke(NOT_INITIALIZED_ERROR));
            return;
        }

        try
        {
            await databaseReference.Child(path).RemoveValueAsync();
            QueueOnMainThread(() => OnDataSaved?.Invoke($"Deleted: {path}"));
        }
        catch (Exception ex)
        {
            QueueOnMainThread(() => OnError?.Invoke($"Delete failed: {ex.Message}"));
        }
    }

    public bool IsFirebaseReady()
    {
        return isInitialized;
    }

    void OnDestroy()
    {
        StopAllListening();
        
        while (mainThreadQueue.TryDequeue(out _)) { }

        if (firebaseApp != null)
        {
            firebaseApp.Dispose();
        }
    }

    void OnApplicationQuit()
    {
        OnDestroy();
    }
}
