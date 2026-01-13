using UnityEngine;
using UnityEngine.EventSystems;

public class StudentController : MonoBehaviour, IPointerClickHandler
{
    private StudentSpawnerController.StudentData studentData;
    private string studentKey;
    private Renderer studentRenderer;
    private Material currentMaterial;
    
    private Collider studentCollider;
    private bool isInitialized = false;

    void Awake()
    {
        studentRenderer = GetComponentInChildren<Renderer>();
        
        // Add collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    public void Initialize(StudentSpawnerController.StudentData data, Material material, string key)
    {
        studentData = data;
        studentKey = key;
        currentMaterial = material;
        isInitialized = true;
        
        // Apply material
        if (studentRenderer != null && material != null)
        {
            studentRenderer.material = material;
        }

        gameObject.name = $"Student_Seat{data.seat}";
    }
    
    public void UpdateStudent(StudentSpawnerController.StudentData data, Material material, string key)
    {
        studentData = data;
        studentKey = key;
        currentMaterial = material;
        
        // Update material
        if (studentRenderer != null && material != null)
        {
            studentRenderer.material = material;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsPointerOverUIElement())
        {
            return;
        }
        
        ShowStudentInfo();
    }

    void OnMouseDown()
    {
        if (IsPointerOverUIElement())
        {
            return;
        }
        
        ShowStudentInfo();
    }

    /// <summary>
    /// Checks if the pointer is currently over any UI element
    /// </summary>
    private bool IsPointerOverUIElement()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }

    void ShowStudentInfo()
    {
        if (!isInitialized || studentData == null || string.IsNullOrEmpty(studentKey)) return;

        Debug.Log($"Clicked student: {studentData.name} with key: {studentKey}");
        
        if (StudentInfoPanel.Instance != null)
        {
            StudentInfoPanel.Instance.ShowStudentInfo(studentData, studentKey);
        }
    }

    public StudentSpawnerController.StudentData GetStudentData()
    {
        return studentData;
    }
    
    public string GetStudentKey()
    {
        return studentKey;
    }
}