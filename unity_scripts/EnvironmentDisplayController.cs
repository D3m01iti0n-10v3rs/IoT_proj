using UnityEngine;
using TMPro;
using System;

public class EnvironmentDisplayController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text temperatureText;
    public TMP_Text humidityText;
    public TMP_Text lightText;
    public TMP_Text lastUpdateText;

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    private System.Diagnostics.Stopwatch updateIntervalStopwatch;
    private long lastUpdateIntervalMs = 0;
    private bool isFirstUpdate = true;
    private const string TEMPERATURE_PREFIX = "Nhiệt độ:  ";
    private const string HUMIDITY_PREFIX = "Độ ẩm: ";
    private const string LIGHT_PREFIX = "Ánh sáng: ";
    private const string NEVER_TEXT = "Never";
    private const string CURRENT_PATH = "current";

    private void Start()
    {
        if (firebaseManager == null)
            firebaseManager = FindObjectOfType<FirebaseDataManager>();

        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;

        UpdateDisplay(0, 0, 0, NEVER_TEXT);
    }

    void OnFirebaseReady(bool success)
    {
        if (success && firebaseManager != null)
        {
            firebaseManager.ListenForData(CURRENT_PATH);
            firebaseManager.GetData(CURRENT_PATH);
        }
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (path == CURRENT_PATH && !string.IsNullOrEmpty(jsonData))
        {
            if (updateIntervalStopwatch == null)
                updateIntervalStopwatch = new System.Diagnostics.Stopwatch();
            
            if (!isFirstUpdate)
            {
                updateIntervalStopwatch.Stop();
                lastUpdateIntervalMs = updateIntervalStopwatch.ElapsedMilliseconds;
                Debug.Log($"Environment data update interval: {lastUpdateIntervalMs} ms");
            }
            
            updateIntervalStopwatch.Restart();
            isFirstUpdate = false;
            
            ParseEnvironmentData(jsonData);
        }
    }

    void ParseEnvironmentData(string jsonData)
    {
        try
        {
            EnvironmentData data = JsonUtility.FromJson<EnvironmentData>(jsonData);
            if (data != null)
            {
                UpdateDisplay(data.temperature, data.humidity, data.light, data.lastUpdate);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Environment data error: {ex.Message}");
        }
    }

    void UpdateDisplay(float temperature, float humidity, float light, string lastUpdate)
    {
        if (temperatureText != null)
            temperatureText.text = TEMPERATURE_PREFIX + temperature + "°C";
        
        if (humidityText != null)
            humidityText.text = HUMIDITY_PREFIX + humidity + "%";

        if (lightText != null)
            lightText.text = LIGHT_PREFIX + light + "%";
        
        if (lastUpdateText != null)
        {
            lastUpdateText.text = lastUpdate != NEVER_TEXT ? lastUpdate : "Updated: Never";
        }
    }

    void OnDestroy()
    {
        FirebaseDataManager.OnFirebaseInitialized -= OnFirebaseReady;
        FirebaseDataManager.OnDataReceived -= OnDataReceived;
        
        if (firebaseManager != null)
            firebaseManager.StopListening(CURRENT_PATH);
    }

    [System.Serializable]
    public class EnvironmentData
    {
        public float temperature;
        public float humidity;
        public float light;
        public string lastUpdate;
        public int studentNumber;
    }
}