using UnityEngine;
using System.Collections.Generic;

public class FirebaseDBTester : MonoBehaviour
{
    public FirebaseDataManager firebaseManager;

    [Header("Test Settings")]
    public bool runTestsAutomatically = true;

    void Start()
    {
        // Subscribe to events
        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;
        FirebaseDataManager.OnDataSaved += OnDataSaved;
        FirebaseDataManager.OnError += OnError;

        if (runTestsAutomatically)
        {
            Invoke("StartTests", 2f);
        }
    }

    void OnFirebaseReady(bool success)
    {
        if (success)
        {
            Debug.Log("✅ Firebase ready for classroom DB tests");
        }
        else
        {
            Debug.LogError("❌ Firebase init failed");
        }
    }

    void StartTests()
    {
        if (!firebaseManager.IsFirebaseReady())
        {
            Debug.LogWarning("Firebase not ready, retrying...");
            Invoke("StartTests", 1f);
            return;
        }

        Debug.Log("🏫 Starting Classroom Database Tests...");
        
        // Test sequence
        Invoke("TestGetCurrentData", 0.5f);
        Invoke("TestGetIOState", 2f);
        Invoke("TestGetStudents", 3.5f);
        Invoke("TestUpdateTemperature", 5f);
        Invoke("TestToggleLights", 7f);
        Invoke("TestUpdateStudent", 9f);
    }

    // TEST 1: Get current sensor data
    void TestGetCurrentData()
    {
        Debug.Log("🌡️ TEST 1: Getting current sensor data...");
        firebaseManager.GetData("current");
    }

    // TEST 2: Get IO state
    void TestGetIOState()
    {
        Debug.Log("💡 TEST 2: Getting IO state...");
        firebaseManager.GetData("ioState");
    }

    // TEST 3: Get students data
    void TestGetStudents()
    {
        Debug.Log("👨‍🎓 TEST 3: Getting students data...");
        firebaseManager.GetData("students");
    }

    // TEST 4: Update temperature
    void TestUpdateTemperature()
    {
        Debug.Log("🔥 TEST 4: Updating temperature...");
        
        var tempUpdate = new Dictionary<string, object>
        {
            { "temperature", Random.Range(25, 30) },
            { "humidity", Random.Range(60, 80) },
            { "lastUpdate", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
            { "studentNumber", Random.Range(0, 3) }
        };

        firebaseManager.UpdateData("current", tempUpdate);
    }

    // TEST 5: Toggle lights
    void TestToggleLights()
    {
        Debug.Log("💡 TEST 5: Toggling lights...");
        
        var lightUpdate = new Dictionary<string, object>
        {
            { "light1State", true },
            { "light2State", false },
            { "fan1State", true },
            { "doorState", false }
        };

        firebaseManager.UpdateData("ioState", lightUpdate);
    }

    // TEST 6: Update student attendance
    void TestUpdateStudent()
    {
        Debug.Log("👨‍🎓 TEST 6: Updating student attendance...");
        
        var studentUpdate = new Dictionary<string, object>
        {
            { 
                "attendance_flag", true 
            },
            { 
                "time_of_attendance", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") 
            },
            { 
                "seat", Random.Range(0, 5) 
            }
        };

        // Update fingerprint_data01
        firebaseManager.UpdateData("students/fingerprint_data01", studentUpdate);
    }

    // TEST 7: Real-time listener (manual trigger)
    public void StartRealTimeMonitoring()
    {
        Debug.Log("📡 Starting real-time monitoring...");
        firebaseManager.ListenForData("current");
        firebaseManager.ListenForData("ioState");
    }

    public void StopRealTimeMonitoring()
    {
        Debug.Log("🛑 Stopping real-time monitoring...");
        firebaseManager.StopListening("current");
        firebaseManager.StopListening("ioState");
    }

    // Manual test methods for UI buttons
    public void ManualTest_GetAllData()
    {
        firebaseManager.GetData("current");
        firebaseManager.GetData("ioState");
        firebaseManager.GetData("students");
    }

    public void ManualTest_ResetLights()
    {
        var resetIO = new Dictionary<string, object>
        {
            { "light1State", false },
            { "light2State", false },
            { "fan1State", false },
            { "fan2State", false },
            { "doorState", false }
        };
        firebaseManager.UpdateData("ioState", resetIO);
    }

    public void ManualTest_MarkStudentPresent(string studentKey = "fingerprint_data02")
    {
        var attendance = new Dictionary<string, object>
        {
            { "attendance_flag", true },
            { "time_of_attendance", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
        };
        firebaseManager.UpdateData($"students/{studentKey}", attendance);
    }

    // Event handlers
    void OnDataReceived(string path, string jsonData)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            Debug.Log($"📭 No data at path: {path}");
            return;
        }

        Debug.Log($"✅ DATA RECEIVED from {path}:");
        Debug.Log(jsonData);

        // You can parse the JSON here if needed
        // Example: ParseStudentData(jsonData);
    }

    void OnDataSaved(string path)
    {
        Debug.Log($"💾 Data saved successfully to: {path}");
    }

    void OnError(string error)
    {
        Debug.LogError($"❌ FIREBASE ERROR: {error}");
    }

    void Update()
    {
        // Manual test controls
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ManualTest_GetAllData();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ManualTest_ResetLights();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ManualTest_MarkStudentPresent();
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            StartRealTimeMonitoring();
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            StopRealTimeMonitoring();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        FirebaseDataManager.OnFirebaseInitialized -= OnFirebaseReady;
        FirebaseDataManager.OnDataReceived -= OnDataReceived;
        FirebaseDataManager.OnDataSaved -= OnDataSaved;
        FirebaseDataManager.OnError -= OnError;

        StopRealTimeMonitoring();
    }
}