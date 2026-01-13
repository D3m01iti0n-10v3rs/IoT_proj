using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class DatabaseControlPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button openPanelButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closePanelButton;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button deleteFingerprintButton;
    [SerializeField] private Button deleteDatabaseButton;
    
    [Header("Firebase")]
    [SerializeField] private FirebaseDataManager firebaseManager;

    private System.Diagnostics.Stopwatch deleteFingerprintStopwatch;
    private System.Diagnostics.Stopwatch deleteDatabaseStopwatch;
    private bool waitingForFingerprintResponse = false;
    private bool waitingForDatabaseDeletion = false;
    
    // Cache paths
    private const string TRACK_PATH = "track";
    private const string STUDENTS_PATH = "students";
    private const string CURRENT_PATH = "current";

    // Cached data for Firebase operations
    private Dictionary<string, object> fingerprintUpdateData;
    private Dictionary<string, object> errorResetData;
    private Dictionary<string, object> successResetData;
    private Dictionary<string, object> resetStudentCountData;

    private void Awake()
    {
        fingerprintUpdateData = new Dictionary<string, object> { { "delFingerprintData", true } };
        errorResetData = new Dictionary<string, object> { { "delFingerError", false } };
        successResetData = new Dictionary<string, object> 
        { 
            { "delFingerprintData", false },
            { "delFingerDone", false }
        };
        resetStudentCountData = new Dictionary<string, object> { { "studentNumber", 0 } };
    }

    private void Start()
    {
        if (firebaseManager == null)
        {
            firebaseManager = FindObjectOfType<FirebaseDataManager>();
            if (firebaseManager == null)
            {
                Debug.LogError("FirebaseDataManager not found!");
                enabled = false;
                return;
            }
        }
        
        openPanelButton.onClick.AddListener(OpenPanel);
        closePanelButton.onClick.AddListener(ClosePanel);
        deleteFingerprintButton.onClick.AddListener(DeleteFingerprint);
        deleteDatabaseButton.onClick.AddListener(DeleteDatabase);
        
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (firebaseManager != null)
        {
            FirebaseDataManager.OnDataReceived += OnFirebaseDataReceived;
            FirebaseDataManager.OnError += OnFirebaseError;
            FirebaseDataManager.OnDataSaved += OnFirebaseDataSaved;
        }
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
        
        if (openPanelButton != null) openPanelButton.onClick.RemoveAllListeners();
        if (closePanelButton != null) closePanelButton.onClick.RemoveAllListeners();
        if (deleteFingerprintButton != null) deleteFingerprintButton.onClick.RemoveAllListeners();
        if (deleteDatabaseButton != null) deleteDatabaseButton.onClick.RemoveAllListeners();
    }

    private void Cleanup()
    {
        FirebaseDataManager.OnDataReceived -= OnFirebaseDataReceived;
        FirebaseDataManager.OnError -= OnFirebaseError;
        FirebaseDataManager.OnDataSaved -= OnFirebaseDataSaved;
        
        if (firebaseManager != null)
        {
            firebaseManager.StopListening(TRACK_PATH);
        }
        
        waitingForFingerprintResponse = false;
        waitingForDatabaseDeletion = false;
    }

    private void OpenPanel()
    {
        panel.SetActive(true);
        resultText.text = "Ready";
    }
    
    private void ClosePanel()
    {
        panel.SetActive(false);
        waitingForFingerprintResponse = false;
        waitingForDatabaseDeletion = false;
        
        if (firebaseManager != null)
        {
            firebaseManager.StopListening(TRACK_PATH);
        }
    }

    private void DeleteFingerprint()
    {
        if (!ValidateOperation("fingerprint")) return;

        if (deleteFingerprintStopwatch == null)
            deleteFingerprintStopwatch = new System.Diagnostics.Stopwatch();
        deleteFingerprintStopwatch.Restart();
        
        SetResultText("Starting fingerprint deletion...");
        waitingForFingerprintResponse = true;
        
        firebaseManager.UpdateData(TRACK_PATH, fingerprintUpdateData);
        firebaseManager.ListenForData(TRACK_PATH);
    }
    
    private void DeleteDatabase()
    {
        if (!ValidateOperation("database")) return;

        if (deleteDatabaseStopwatch == null)
            deleteDatabaseStopwatch = new System.Diagnostics.Stopwatch();
        deleteDatabaseStopwatch.Restart();
        
        SetResultText("Deleting all student data...");
        waitingForDatabaseDeletion = true;
        
        firebaseManager.DeleteData(STUDENTS_PATH);
        firebaseManager.UpdateData(CURRENT_PATH, resetStudentCountData);
    }
    
    private void OnFirebaseDataReceived(string path, string jsonData)
    {
        if (path != TRACK_PATH || !waitingForFingerprintResponse || string.IsNullOrEmpty(jsonData))
            return;
        
        TrackData trackData = ParseTrackData(jsonData);
        
        if (trackData == null)
        {
            SetResultText("Error: Failed to parse response");
            FinishFingerprintOperation();
            return;
        }
        
        if (trackData.delFingerError)
        {
            HandleFingerprintError();
        }

        else if (!trackData.delFingerError && trackData.delFingerDone)
        {
            if (deleteFingerprintStopwatch != null && deleteFingerprintStopwatch.IsRunning)
            {
                deleteFingerprintStopwatch.Stop();
                long timeMs = deleteFingerprintStopwatch.ElapsedMilliseconds;
                Debug.Log($"Fingerprint deletion time: {timeMs} ms");
            }

            HandleFingerprintSuccess();
        }
    }
    
    private void OnFirebaseDataSaved(string message)
    {
        if (message.StartsWith("Deleted: ") && message.Contains(STUDENTS_PATH) && waitingForDatabaseDeletion)
        {
            if (deleteDatabaseStopwatch != null && deleteDatabaseStopwatch.IsRunning)
            {
                deleteDatabaseStopwatch.Stop();
                long timeMs = deleteDatabaseStopwatch.ElapsedMilliseconds;
                Debug.Log($"Database deletion time: {timeMs} ms");
            }
            SetResultText("Success: All student data deleted");
            waitingForDatabaseDeletion = false;
        }

        else if (message == CURRENT_PATH && waitingForDatabaseDeletion)
        {

        }

        else if (message == TRACK_PATH && waitingForFingerprintResponse)
        {

        }
    }
    
    private void OnFirebaseError(string errorMessage)
    {
        SetResultText($"Error: {errorMessage}");
        
        if (waitingForFingerprintResponse)
        {
            FinishFingerprintOperation();
        }
        
        if (waitingForDatabaseDeletion)
        {
            waitingForDatabaseDeletion = false;
        }
    }
    
    private void HandleFingerprintError()
    {
        SetResultText("Error: Fingerprint deletion failed");
        
        firebaseManager.UpdateData(TRACK_PATH, errorResetData);
        
        FinishFingerprintOperation();
    }
    
    private void HandleFingerprintSuccess()
    {
        SetResultText("Success: Fingerprint deleted");
        
        firebaseManager.UpdateData(TRACK_PATH, successResetData);
        
        FinishFingerprintOperation();
    }
    
    private void FinishFingerprintOperation()
    {
        waitingForFingerprintResponse = false;
        
        if (firebaseManager != null)
        {
            firebaseManager.StopListening(TRACK_PATH);
        }
    }
    
    private bool ValidateOperation(string operationType)
    {
        if (!firebaseManager.IsFirebaseReady()) 
        {
            SetResultText("Firebase not ready");
            return false;
        }
        
        if (waitingForFingerprintResponse || waitingForDatabaseDeletion)
        {
            SetResultText("Another operation in progress");
            return false;
        }
        
        return true;
    }
    
    private TrackData ParseTrackData(string jsonData)
    {
        if (string.IsNullOrEmpty(jsonData))
            return null;
        
        TrackData trackData = null;
        
        try
        {
            trackData = JsonUtility.FromJson<TrackData>(jsonData);
        }
        catch
        {

        }
        
        if (trackData == null && jsonData.Length > 2)
        {
            try
            {
                string wrappedJson = $"{{\"track\":{jsonData}}}";
                TrackDataWrapper wrapper = JsonUtility.FromJson<TrackDataWrapper>(wrappedJson);
                trackData = wrapper?.track;
            }
            catch
            {

            }
        }
        
        return trackData;
    }
    
    private void SetResultText(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }
    }
    
    [System.Serializable]
    private class TrackDataWrapper
    {
        public TrackData track;
    }
    
    [System.Serializable]
    private class TrackData
    {
        public bool delFingerDone;
        public bool delFingerError;
        public bool delFingerprintData;
        public bool enrollDone;
        public bool enrollError;
        public bool enrollFlag;
        public int fingerID;
        public bool manualMode;
        public int studentNumber;
        public bool teacher_flag;
    }
}