using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class EnrollmentController : MonoBehaviour
{
    [Header("UI References")]
    public Button startEnrollmentButton;
    public GameObject enrollmentPanel;
    public TextMeshProUGUI enrollmentStatusText;
    public TMP_InputField idInputField;
    public TMP_InputField nameInputField;
    public TMP_InputField seatInputField;
    public Button submitStudentInfoButton;
    public Button cancelEnrollmentButton;

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    private string newFingerprintKey = "";
    private int currentFingerID = 0;
    private bool isEnrolling = false;
    private Coroutine enrollmentCoroutine;
    private bool waitingForEnrollDone = false;
    private bool enrollmentFailed = false;
    private bool isSubscribed = false;
    private System.Diagnostics.Stopwatch enrollmentStopwatch;

    void Start()
    {
        if (startEnrollmentButton != null)
            startEnrollmentButton.onClick.AddListener(StartEnrollmentProcess);
        
        if (submitStudentInfoButton != null)
            submitStudentInfoButton.onClick.AddListener(SubmitStudentInfo);
        
        if (cancelEnrollmentButton != null)
            cancelEnrollmentButton.onClick.AddListener(CancelEnrollment);

        if (enrollmentPanel != null)
            enrollmentPanel.SetActive(false);
        
        FirebaseDataManager.OnDataReceived += OnDataReceived;
        isSubscribed = true;
    }

    void StartEnrollmentProcess()
    {
        if (isEnrolling) return;
        
        if (enrollmentStopwatch == null)
            enrollmentStopwatch = new System.Diagnostics.Stopwatch();
        enrollmentStopwatch.Restart();
        
        enrollmentCoroutine = StartCoroutine(EnrollmentWorkflow());
    }

    IEnumerator EnrollmentWorkflow()
    {
        isEnrolling = true;
        enrollmentFailed = false;
        currentFingerID = 0;
        
        if (startEnrollmentButton != null)
            startEnrollmentButton.interactable = false;

        firebaseManager.ListenForData("track/enrollDone");
        firebaseManager.ListenForData("track/fingerID");
        firebaseManager.ListenForData("track/enrollError");
        
        if (enrollmentPanel != null)
            enrollmentPanel.SetActive(true);
        
        if (enrollmentStatusText != null)
            enrollmentStatusText.text = "Đang đăng ký...";
        
        ClearInputFields();
        SetFormInteractable(false);

        firebaseManager.SendData("track/enrollFlag", true);
        firebaseManager.SendData("track/enrollDone", false);
        firebaseManager.SendData("track/enrollError", false);

        waitingForEnrollDone = true;

        // Wait for completion/error
        while (waitingForEnrollDone)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (enrollmentFailed)
        {
            yield return new WaitForSeconds(2f);
            ResetEnrollmentUI();
            yield break;
        }

        if (string.IsNullOrEmpty(newFingerprintKey) || currentFingerID == 0)
        {
            if (enrollmentStatusText != null)
                enrollmentStatusText.text = "Xảy ra lỗi. Vui lòng thử lại";
            yield return new WaitForSeconds(2f);
            ResetEnrollmentUI();
            yield break;
        }

        if (enrollmentStatusText != null)
            enrollmentStatusText.text = "Đăng ký thành công, nhập thông tin học sinh";
        
        SetFormInteractable(true);
        
        if (enrollmentStopwatch != null && enrollmentStopwatch.IsRunning)
        {
            enrollmentStopwatch.Stop();
            long enrollmentTimeMs = enrollmentStopwatch.ElapsedMilliseconds;
            Debug.Log($"Fingerprint enrollment time: {enrollmentTimeMs} ms");
        }
    }

    void SubmitStudentInfo()
    {
        if (string.IsNullOrEmpty(newFingerprintKey) || currentFingerID == 0)
        {
            if (enrollmentStatusText != null)
                enrollmentStatusText.text = "Xảy ra lỗi. Vui lòng thử lại";
            return;
        }

        if (string.IsNullOrEmpty(idInputField.text) || 
            string.IsNullOrEmpty(nameInputField.text) || 
            string.IsNullOrEmpty(seatInputField.text))
        {
            if (enrollmentStatusText != null)
                enrollmentStatusText.text = "Vui lòng nhập tất cả trường thông tin";
            return;
        }

        if (!int.TryParse(seatInputField.text, out int seatNumber) || seatNumber <= 0)
        {
            if (enrollmentStatusText != null)
                enrollmentStatusText.text = "Ghế phải là số dương";
            return;
        }

        StartCoroutine(SubmitStudentInfoCoroutine(seatNumber));
    }

    IEnumerator SubmitStudentInfoCoroutine(int seatNumber)
    {
        SetFormInteractable(false);
        if (enrollmentStatusText != null)
            enrollmentStatusText.text = "Đang lưu thông tin học sinh";

        Dictionary<string, object> studentData = new Dictionary<string, object>
        {
            { "attendance_flag", true },
            { "id", idInputField.text },
            { "name", nameInputField.text },
            { "seat", seatNumber },
            { "time_of_attendance", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        string studentPath = $"students/{newFingerprintKey}";
        
        firebaseManager.UpdateData(studentPath, studentData);

        yield return new WaitForSeconds(1f);

        if (enrollmentStatusText != null)
            enrollmentStatusText.text = "Đang cập nhật mã số vân tay";

        int nextFingerID = currentFingerID + 1;
        firebaseManager.SendData("track/fingerID", nextFingerID);

        yield return new WaitForSeconds(0.5f);

        firebaseManager.GetData("track/studentNumber");
        yield return new WaitForSeconds(0.5f); // Wait for response

        firebaseManager.SendData("track/enrollFlag", false);
        firebaseManager.SendData("track/enrollDone", false);

        Debug.Log($"Student {nameInputField.text} saved to {newFingerprintKey} and fingerID updated to {nextFingerID}");

        if (enrollmentStatusText != null)
            enrollmentStatusText.text = "Đăng ký thành công";
        
        yield return new WaitForSeconds(2f);

        ResetEnrollmentUI();
    }

    void CancelEnrollment()
    {
        if (isEnrolling)
        {
            StartCoroutine(CancelEnrollmentCoroutine());
        }
        else
        {
            ResetEnrollmentUI();
        }
    }

    IEnumerator CancelEnrollmentCoroutine()
    {
        firebaseManager.SendData("track/enrollFlag", false);
        firebaseManager.SendData("track/enrollDone", false);
        firebaseManager.SendData("track/enrollError", false);
        
        yield return new WaitForSeconds(0.5f);
        ResetEnrollmentUI();
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (!isEnrolling) return;

        if (path == "track/enrollDone" && waitingForEnrollDone)
        {
            if (!string.IsNullOrEmpty(jsonData) && bool.TryParse(jsonData, out bool enrollDone) && enrollDone)
            {
                waitingForEnrollDone = false;
                firebaseManager.GetData("track/fingerID");
            }
        }
        
        else if (path == "track/enrollError" && isEnrolling)  // Only check if enrolling
        {
            if (!string.IsNullOrEmpty(jsonData) && bool.TryParse(jsonData, out bool enrollError) && enrollError)
            {
                enrollmentFailed = true;
                waitingForEnrollDone = false;

                if (enrollmentStatusText != null)
                    enrollmentStatusText.text = "Xảy ra lỗi. Vui lòng thử lại";
                
                firebaseManager.SendData("track/enrollError", false);
                firebaseManager.SendData("track/enrollFlag", false);

            }
        }
        
        else if (path == "track/fingerID" && !enrollmentFailed && isEnrolling)
        {
            if (!string.IsNullOrEmpty(jsonData) && int.TryParse(jsonData, out int fingerID))
            {
                currentFingerID = fingerID;
                newFingerprintKey = $"fingerprint_data{fingerID:D2}";
            }
        }
        
        else if (path == "track/studentNumber" && !enrollmentFailed && isEnrolling)
        {
            if (!string.IsNullOrEmpty(jsonData) && int.TryParse(jsonData, out int studentNumber))
            {
                int nextStudentNumber = studentNumber + 1;
                firebaseManager.SendData("track/studentNumber", nextStudentNumber);
                Debug.Log($"Updated studentNumber from {studentNumber} to {nextStudentNumber}");
            }
        }
    }

    void SetFormInteractable(bool interactable)
    {
        if (idInputField != null)
            idInputField.interactable = interactable;
        if (nameInputField != null)
            nameInputField.interactable = interactable;
        if (seatInputField != null)
            seatInputField.interactable = interactable;
        if (submitStudentInfoButton != null)
            submitStudentInfoButton.interactable = interactable;
        if (cancelEnrollmentButton != null)
            cancelEnrollmentButton.interactable = interactable;
    }

    void ClearInputFields()
    {
        if (idInputField != null)
            idInputField.text = "";
        if (nameInputField != null)
            nameInputField.text = "";
        if (seatInputField != null)
            seatInputField.text = "";
    }

    void ResetEnrollmentUI()
    {
        if (enrollmentPanel != null)
            enrollmentPanel.SetActive(false);
        
        if (startEnrollmentButton != null)
            startEnrollmentButton.interactable = true;
        
        firebaseManager.StopListening("track/enrollDone");
        firebaseManager.StopListening("track/fingerID");
        firebaseManager.StopListening("track/enrollError");

        isEnrolling = false;
        waitingForEnrollDone = false;
        enrollmentFailed = false;
        newFingerprintKey = "";
        currentFingerID = 0;

        ClearInputFields();
        
        // Stop coroutine if running
        if (enrollmentCoroutine != null)
        {
            StopCoroutine(enrollmentCoroutine);
            enrollmentCoroutine = null;
        }
        
        Debug.Log("Enrollment UI reset");
    }

    void OnDestroy()
    {
        if (isSubscribed)
        {
            FirebaseDataManager.OnDataReceived -= OnDataReceived;
            isSubscribed = false;
        }
        
        if (isEnrolling)
        {
            firebaseManager.SendData("track/enrollFlag", false);
            firebaseManager.SendData("track/enrollDone", false);
            firebaseManager.SendData("track/enrollError", false);
        }

        if (enrollmentCoroutine != null)
        {
            StopCoroutine(enrollmentCoroutine);
        }
    }
}