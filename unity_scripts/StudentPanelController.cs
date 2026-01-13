using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class StudentPanelController : MonoBehaviour
{
    [Header("Student Panel References")]
    public Button showStudentPanelButton;
    public GameObject studentPanel;
    public Button closeStudentPanelButton;

    [Header("Table References")]
    public Transform tableContent;
    public GameObject studentRowPrefab;

    [Header("UI Text")]
    public TMP_Text showStudentPanelButtonText;
    public TMP_Text closeStudentPanelButtonText;

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    private List<StudentData> studentList = new List<StudentData>();
    private Dictionary<int, GameObject> studentRows = new Dictionary<int, GameObject>();
    private bool isStudentPanelVisible = false;
    private bool isListeningToRealtime = false;
    private bool isSubscribed = false;

    void Start()
    {
        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;
        isSubscribed = true;

        if (showStudentPanelButton != null) showStudentPanelButton.onClick.AddListener(ToggleStudentPanel);
        if (closeStudentPanelButton != null) closeStudentPanelButton.onClick.AddListener(HideStudentPanel);

        UpdateButtonTexts();

        if (studentPanel != null)
        {
            studentPanel.SetActive(false);
            isStudentPanelVisible = false;
        }
    }

    void OnFirebaseReady(bool success)
    {
        if (success)
        {
            SetupRealtimeListener();
            firebaseManager.GetData("students");
        }
    }

    void SetupRealtimeListener()
    {
        if (firebaseManager != null && !isListeningToRealtime)
        {
            firebaseManager.ListenForData("students");
            isListeningToRealtime = true;
        }
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (path == "students" && !string.IsNullOrEmpty(jsonData))
        {
            ParseStudentData(jsonData);
            if (isStudentPanelVisible)
            {
                UpdateStudentTable();
            }
        }
        else if (path.Contains("students/"))
        {
            firebaseManager.GetData("students");
        }
    }

    void ParseStudentData(string jsonData)
    {
        try
        {
            studentList.Clear();
            
            string cleanJson = jsonData.Trim('{', '}');
            string[] studentEntries = cleanJson.Split(new[] { "}," }, System.StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string entry in studentEntries)
            {
                try
                {
                    string[] parts = entry.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        string studentJson = parts[1].Trim();
                        if (!studentJson.EndsWith("}")) studentJson += "}";
                        
                        StudentData student = JsonUtility.FromJson<StudentData>(studentJson);
                        if (student != null)
                        {
                            studentList.Add(student);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Student entry parse error: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Student data parse error: {ex.Message}");
        }
    }

    void UpdateStudentTable()
    {
        var sortedStudents = studentList.OrderBy(s => s.seat).ToList();
        var seatsToRemove = new HashSet<int>(studentRows.Keys);
        
        foreach (var student in sortedStudents)
        {
            seatsToRemove.Remove(student.seat);
            
            if (studentRows.ContainsKey(student.seat))
            {
                // FIXED: Update existing row with ALL fields including time_of_attendance
                UpdateStudentRow(studentRows[student.seat], student);
            }
            else
            {
                GameObject row = Instantiate(studentRowPrefab, tableContent);
                SetupStudentRow(row, student);
                studentRows[student.seat] = row;
            }
        }
        
        foreach (int seat in seatsToRemove)
        {
            if (studentRows.ContainsKey(seat))
            {
                Destroy(studentRows[seat]);
                studentRows.Remove(seat);
            }
        }
    }

    void SetupStudentRow(GameObject row, StudentData student)
    {
        TMP_Text seatText = row.transform.Find("SeatText")?.GetComponent<TMP_Text>();
        Image attendanceDot = row.transform.Find("AttendanceDot")?.GetComponent<Image>();
        TMP_Text nameText = row.transform.Find("NameText")?.GetComponent<TMP_Text>();
        TMP_Text idText = row.transform.Find("IDText")?.GetComponent<TMP_Text>();
        TMP_Text timeText = row.transform.Find("TimeText")?.GetComponent<TMP_Text>();

        if (seatText != null) seatText.text = student.seat.ToString();
        if (attendanceDot != null) attendanceDot.color = student.attendance_flag ? Color.green : Color.red;
        if (nameText != null) nameText.text = "| " + student.name;
        if (idText != null) idText.text = "| " + student.id.ToString();
        if (timeText != null) timeText.text = "| " + student.time_of_attendance;
    }

    void UpdateStudentRow(GameObject row, StudentData student)
    {
        TMP_Text seatText = row.transform.Find("SeatText")?.GetComponent<TMP_Text>();
        Image attendanceDot = row.transform.Find("AttendanceDot")?.GetComponent<Image>();
        TMP_Text nameText = row.transform.Find("NameText")?.GetComponent<TMP_Text>();
        TMP_Text idText = row.transform.Find("IDText")?.GetComponent<TMP_Text>();
        TMP_Text timeText = row.transform.Find("TimeText")?.GetComponent<TMP_Text>();

        if (seatText != null) seatText.text = student.seat.ToString();
        if (attendanceDot != null) attendanceDot.color = student.attendance_flag ? Color.green : Color.red;
        if (nameText != null) nameText.text = "| " + student.name;
        if (idText != null) idText.text = "| " + student.id.ToString();
        if (timeText != null) timeText.text = "| " + student.time_of_attendance;
    }

    void ToggleStudentPanel()
    {
        if (studentPanel != null)
        {
            isStudentPanelVisible = !isStudentPanelVisible;
            studentPanel.SetActive(isStudentPanelVisible);
            
            if (isStudentPanelVisible)
            {
                firebaseManager.GetData("students");
                UpdateStudentTable();
            }
        }
    }

    void HideStudentPanel()
    {
        if (studentPanel != null)
        {
            studentPanel.SetActive(false);
            isStudentPanelVisible = false;
        }
    }

    void UpdateButtonTexts()
    {
        if (showStudentPanelButtonText != null) showStudentPanelButtonText.text = "Danh sách học sinh";
        if (closeStudentPanelButtonText != null) closeStudentPanelButtonText.text = "Đóng";
    }

    void OnDestroy()
    {
        if (isSubscribed)
        {
            FirebaseDataManager.OnFirebaseInitialized -= OnFirebaseReady;
            FirebaseDataManager.OnDataReceived -= OnDataReceived;
            isSubscribed = false;
        }
        
        if (firebaseManager != null && isListeningToRealtime)
        {
            firebaseManager.StopListening("students");
        }

        foreach (var row in studentRows.Values)
        {
            if (row != null) Destroy(row);
        }
        studentRows.Clear();
    }

    [System.Serializable]
    public class StudentData
    {
        public bool attendance_flag;
        public string id;
        public string name;
        public int seat;
        public string time_of_attendance;
    }
}