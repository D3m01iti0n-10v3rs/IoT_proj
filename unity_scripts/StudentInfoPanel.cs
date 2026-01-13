using UnityEngine;
using TMPro;
using System.Collections;

public class StudentInfoPanel : MonoBehaviour
{
    public static StudentInfoPanel Instance;
    
    [Header("UI References")]
    public GameObject panel;
    public TMP_Text studentNameText;
    public TMP_Text studentIdText;
    public TMP_Text seatNumberText;
    public TMP_Text statusText;
    public TMP_Text lastUpdateText;
    public UnityEngine.UI.Button closeButton;

    [Header("Firebase Configuration")]
    public string studentDataPath = "students";

    private FirebaseDataManager firebaseManager;
    private StudentSpawnerController.StudentData currentStudentData;
    private string currentStudentKey = "";
    private bool isListening = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);
            
        HidePanel();
    }

    void Start()
    {
        firebaseManager = FindObjectOfType<FirebaseDataManager>();
        if (firebaseManager == null)
        {
            Debug.LogError("FirebaseDataManager not found!");
        }
    }

    public void ShowStudentInfo(StudentSpawnerController.StudentData studentData, string studentKey)
    {
        if (studentData == null || string.IsNullOrEmpty(studentKey))
        {
            Debug.LogWarning("ShowStudentInfo called with null data or empty key");
            return;
        }
        
        Debug.Log($"Showing info for student: {studentKey}");

        StopListeningToCurrentStudent();

        currentStudentData = studentData;
        currentStudentKey = studentKey;
        
        UpdatePanelUI();
        
        if (panel != null) 
        {
            panel.SetActive(true);
        }
        
        StartListeningToStudent();
    }

    private void StartListeningToStudent()
    {
        if (firebaseManager == null)
        {
            firebaseManager = FindObjectOfType<FirebaseDataManager>();
            if (firebaseManager == null) return;
        }

        if (!firebaseManager.IsFirebaseReady())
        {
            StartCoroutine(RetryStartListening());
            return;
        }

        if (string.IsNullOrEmpty(currentStudentKey))
        {
            Debug.LogWarning("Cannot start listening: currentStudentKey is empty");
            return;
        }

        string studentPath = $"{studentDataPath}/{currentStudentKey}";
        
        FirebaseDataManager.OnDataReceived -= HandleDataReceived;
        
        FirebaseDataManager.OnDataReceived += HandleDataReceived;
        
        firebaseManager.ListenForData(studentPath);
        
        isListening = true;
    }

    private IEnumerator RetryStartListening()
    {
        yield return new WaitForSeconds(1f);
        StartListeningToStudent();
    }

    private void HandleDataReceived(string path, string jsonData)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(currentStudentKey))
            return;
        
        // Check if data is for current student
        string expectedPath = $"{studentDataPath}/{currentStudentKey}";
        
        if (!path.Contains(currentStudentKey))
        {
            return;
        }

        if (string.IsNullOrEmpty(jsonData))
        {
            return;
        }

        try
        {
            currentStudentData = JsonUtility.FromJson<StudentSpawnerController.StudentData>(jsonData);
            UpdatePanelUI();
        }
        catch (System.Exception)
        {

        }
    }

    private void StopListeningToCurrentStudent()
    {
        if (!isListening || string.IsNullOrEmpty(currentStudentKey))
            return;
            
        if (firebaseManager != null)
        {
            string studentPath = $"{studentDataPath}/{currentStudentKey}";
            firebaseManager.StopListening(studentPath);
        }
        
        FirebaseDataManager.OnDataReceived -= HandleDataReceived;
        
        isListening = false;
    }

    public void ShowPanel()
    {
        if (panel != null) 
        {
            panel.SetActive(true);
            if (currentStudentData != null) 
                UpdatePanelUI();
        }
    }

    public void HidePanel()
    {
        StopListeningToCurrentStudent();
        
        if (panel != null) 
        {
            panel.SetActive(false);
        }
    }

    private void UpdatePanelUI()
    {
        if (currentStudentData == null) return;
        
        if (studentNameText != null) 
            studentNameText.text = $"Tên: {currentStudentData.name}";
        if (studentIdText != null) 
            studentIdText.text = $"MSSV: {currentStudentData.id}";
        if (seatNumberText != null) 
            seatNumberText.text = $"Chỗ ngồi: {currentStudentData.seat}";
        if (statusText != null) 
            statusText.text = currentStudentData.attendance_flag ? "Trạng thái: Có mặt" : "Trạng thái: Vắng";
        if (lastUpdateText != null) 
            lastUpdateText.text = $"Cập nhật: {currentStudentData.time_of_attendance}";
    }

    void OnDestroy()
    {
        StopListeningToCurrentStudent();
        FirebaseDataManager.OnDataReceived -= HandleDataReceived;
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}