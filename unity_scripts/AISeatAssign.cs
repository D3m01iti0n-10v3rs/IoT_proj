using System;
using System.Collections.Generic;
using UnityEngine;

public class ClassSessionTracker : MonoBehaviour
{
    public FirebaseDataManager firebase;

    private bool classActive;
    private int lastStudentNumber;
    private int peakStudentCount;

    private string latestStudentsJson;

    private Dictionary<string, int> seatMap = new Dictionary<string, int>();
    private HashSet<string> attended = new HashSet<string>();

    void Start()
    {
        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;
    }

    void OnFirebaseReady(bool ready)
    {
        if (!ready) return;

        firebase.ListenForData("students");
        firebase.ListenForData("track/studentNumber");
    }

    void OnDataReceived(string path, string json)
    {
        if (path == "students" && !string.IsNullOrEmpty(json) && json != "null")
        {
            latestStudentsJson = json;
            ParseStudents(json);
        }
        else if (path == "track/studentNumber")
        {
            HandleStudentNumber(json);
        }
    }

    void HandleStudentNumber(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        int current = int.Parse(json);

        if (!classActive && lastStudentNumber == 0 && current > 0)
        {
            classActive = true;
            peakStudentCount = 0;
            attended.Clear();
        }

        if (classActive && current > peakStudentCount)
            peakStudentCount = current;

        if (classActive && lastStudentNumber > 0 && current == 0)
        {
            classActive = false;
            SaveSession();
        }

        lastStudentNumber = current;
    }

    void ParseStudents(string json)
    {
        string[] blocks = json.Split(
            new[] { "fingerprint_data" },
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (string block in blocks)
        {
            int idEnd = block.IndexOf('"');
            if (idEnd < 0) continue;

            string id = "fingerprint_data" + block.Substring(0, idEnd);

            int seatIdx = block.IndexOf("\"seat\"");
            if (seatIdx > 0)
            {
                int colon = block.IndexOf(':', seatIdx);
                int comma = block.IndexOf(',', colon);
                if (comma > colon)
                {
                    int seat = int.Parse(block.Substring(colon + 1, comma - colon - 1));
                    seatMap[id] = seat;
                }
            }

            if (classActive && block.Contains("\"attendance_flag\":true"))
            {
                attended.Add(id);
            }
        }
    }

    string GetNextWeekday(string today)
    {
        string[] days = { "MON", "TUE", "WED", "THU", "FRI", "SAT" };

        int index = Array.IndexOf(days, today);
        int next = (index + 1) % days.Length;

        return days[next];
    }

    void SaveSession()
    {
        Dictionary<string, object> sessionData = new Dictionary<string, object>();
        Dictionary<string, object> studentsData = new Dictionary<string, object>();

        foreach (var kvp in seatMap)
        {
            studentsData[kvp.Key] = new Dictionary<string, object>
            {
                { "student_id", kvp.Key },
                { "seat", kvp.Value },
                { "attended", attended.Contains(kvp.Key) }
            };
        }

        string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string weekday = DateTime.Now.ToString("ddd").ToUpper();

        sessionData["session_id"] = sessionId;
        sessionData["peak_student_count"] = peakStudentCount;
        sessionData["students"] = studentsData;
        sessionData["weekday"] = weekday;

        firebase.SendData($"sessions/{sessionId}", sessionData);

        string nextWeekday = GetNextWeekday(weekday);
        firebase.SendData("track/nextWeekday", nextWeekday);
    }

    void OnDestroy()
    {
        FirebaseDataManager.OnFirebaseInitialized -= OnFirebaseReady;
        FirebaseDataManager.OnDataReceived -= OnDataReceived;
    }
}
