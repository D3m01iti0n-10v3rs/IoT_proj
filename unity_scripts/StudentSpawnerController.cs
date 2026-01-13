using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StudentSpawnerController : MonoBehaviour
{
    [Header("Student Prefab")]
    public GameObject studentPrefab;
    public Material presentMaterial;
    public Material absentMaterial;

    [Header("Layout Settings")]
    public Vector3 startPosition = new Vector3(16f, 0f, -8f);
    public float seatSpacingX = 2f;
    public float tableGapX = 5f;
    public float rowSpacingZ = 2.5f;
    public int seatsPerTable = 4;
    public int tablesPerRow = 3;
    public int totalRows = 12;

    [Header("Rotation Settings")]
    [Tooltip("Rotation applied to all students (in degrees)")]
    public Vector3 studentRotation = new Vector3(0f, 180f, 0f);

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    [Header("Performance Settings")]
    public int maxStudentsToSpawn = 50;
    public bool enableStudentCulling = true;

    private Dictionary<string, StudentData> studentDataByKey = new Dictionary<string, StudentData>();
    private Dictionary<int, string> seatToStudentKey = new Dictionary<int, string>();
    private Dictionary<int, StudentController> spawnedStudents = new Dictionary<int, StudentController>();

    private Queue<string> pendingJsonData = new Queue<string>();
    private bool shouldProcessData = false;
    private bool isSubscribed = false;

    void Start()
    {
        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;
        isSubscribed = true;
    }

    void Update()
    {
        if (shouldProcessData && pendingJsonData.Count > 0)
        {
            string jsonData = pendingJsonData.Dequeue();
            ProcessStudentDataOnMainThread(jsonData);
            
            if (pendingJsonData.Count == 0)
            {
                shouldProcessData = false;
            }
        }
    }

    void OnFirebaseReady(bool success)
    {
        if (success)
        {
            firebaseManager.ListenForData("students");
            firebaseManager.GetData("students");
        }
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (path == "students" && !string.IsNullOrEmpty(jsonData))
        {
            if (pendingJsonData.Count < 2)
            {
                pendingJsonData.Enqueue(jsonData);
                shouldProcessData = true;
            }
        }
    }

    void ProcessStudentDataOnMainThread(string jsonData)
    {
        try
        {
            studentDataByKey.Clear();
            seatToStudentKey.Clear();

            string cleanJson = jsonData.Trim('{', '}');
            
            string[] studentEntries = cleanJson.Split(new[] { "}," }, System.StringSplitOptions.RemoveEmptyEntries);
            
            int processedCount = 0;
            
            foreach (string entry in studentEntries)
            {
                if (processedCount >= maxStudentsToSpawn) break;

                try
                {
                    string[] parts = entry.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        // Get fingerprint_data
                        string studentKey = parts[0].Trim().Trim('"', ' ', '\n', '\r', '\t');
                        
                        string studentJson = parts[1].Trim();
                        if (!studentJson.EndsWith("}")) studentJson += "}";

                        StudentData student = JsonUtility.FromJson<StudentData>(studentJson);
                        if (student != null && student.seat > 0)
                        {
                            studentDataByKey[studentKey] = student;
                            seatToStudentKey[student.seat] = studentKey;
                            processedCount++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Student entry error: {ex.Message}");
                }
            }

            SpawnAllStudents();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Student data error: {ex.Message}");
        }
    }

    void SpawnAllStudents()
    {
        var validSeats = seatToStudentKey.Keys.OrderBy(seat => seat).Take(maxStudentsToSpawn).ToList();
        var seatsToRemove = new HashSet<int>(spawnedStudents.Keys);
        
        foreach (int seat in validSeats)
        {
            seatsToRemove.Remove(seat);
            
            if (seatToStudentKey.ContainsKey(seat) && studentDataByKey.ContainsKey(seatToStudentKey[seat]))
            {
                string studentKey = seatToStudentKey[seat];
                StudentData student = studentDataByKey[studentKey];
                
                if (spawnedStudents.ContainsKey(seat))
                {
                    UpdateStudent(seat, student, studentKey);
                }
                else
                {
                    SpawnStudent(seat, student, studentKey);
                }
            }
        }
        
        // Remove students that no longer exist or exceed limit
        foreach (int seat in seatsToRemove)
        {
            if (spawnedStudents.ContainsKey(seat))
            {
                if (spawnedStudents[seat] != null && spawnedStudents[seat].gameObject != null)
                {
                    Destroy(spawnedStudents[seat].gameObject);
                }
                spawnedStudents.Remove(seat);
            }
        }
    }

    void SpawnStudent(int seat, StudentData student, string studentKey)
    {
        Vector3 position = CalculateSeatPosition(seat);
        Quaternion rotation = Quaternion.Euler(studentRotation);
        GameObject studentObj = Instantiate(studentPrefab, position, rotation, this.transform);
        
        StudentController studentController = studentObj.GetComponent<StudentController>();
        if (studentController == null)
        {
            studentController = studentObj.AddComponent<StudentController>();
        }

        Material materialToUse = student.attendance_flag ? presentMaterial : absentMaterial;
        ApplyMaterialToBodyMesh(studentObj, materialToUse);
        
        studentController.Initialize(student, materialToUse, studentKey);
        
        spawnedStudents[seat] = studentController;
    }

    void UpdateStudent(int seat, StudentData student, string studentKey)
    {
        if (spawnedStudents.ContainsKey(seat))
        {
            Material materialToUse = student.attendance_flag ? presentMaterial : absentMaterial;
            ApplyMaterialToBodyMesh(spawnedStudents[seat].gameObject, materialToUse);
            
            spawnedStudents[seat].UpdateStudent(student, materialToUse, studentKey);
        }
    }

    void ApplyMaterialToBodyMesh(GameObject studentObj, Material material)
    {
        Transform bodyMeshTransform = FindDeepChild(studentObj.transform, "HumanM_BodyMesh");
        if (bodyMeshTransform != null)
        {
            Renderer bodyMeshRenderer = bodyMeshTransform.GetComponent<Renderer>();
            if (bodyMeshRenderer != null)
            {
                bodyMeshRenderer.material = material;
            }
        }
        else
        {
            Renderer fallbackRenderer = studentObj.GetComponentInChildren<Renderer>();
            if (fallbackRenderer != null)
            {
                fallbackRenderer.material = material;
            }
        }
    }

    Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;
            
            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    Vector3 CalculateSeatPosition(int seatNumber)
    {
        int seatIndex = seatNumber - 1;
        int row = seatIndex / (tablesPerRow * seatsPerTable);
        int seatInRow = seatIndex % (tablesPerRow * seatsPerTable);
        int tableIndex = seatInRow / seatsPerTable;
        int seatInTable = seatInRow % seatsPerTable;
        
        float tableWidth = seatsPerTable * seatSpacingX;
        float x = startPosition.x - (seatInTable * seatSpacingX) - (tableIndex * (tableWidth + tableGapX));
        float z = startPosition.z - (row * rowSpacingZ);
        
        return new Vector3(x, startPosition.y, z);
    }

    void OnDestroy()
    {
        if (isSubscribed)
        {
            FirebaseDataManager.OnFirebaseInitialized -= OnFirebaseReady;
            FirebaseDataManager.OnDataReceived -= OnDataReceived;
            isSubscribed = false;
        }
        
        if (firebaseManager != null)
            firebaseManager.StopListening("students");

        studentDataByKey.Clear();
        seatToStudentKey.Clear();
        
        foreach (var student in spawnedStudents.Values)
        {
            if (student != null && student.gameObject != null)
                Destroy(student.gameObject);
        }
        spawnedStudents.Clear();
        
        pendingJsonData.Clear();
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