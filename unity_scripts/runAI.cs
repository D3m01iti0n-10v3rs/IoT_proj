using UnityEngine;
using System.Diagnostics;
using System.IO;

public class RunPythonOnButton : MonoBehaviour
{
    public void RunPython()
    {
        string exePath = Path.Combine(Application.streamingAssetsPath,"ai_script.exe");

        UnityEngine.Debug.Log("Button pressed");
        UnityEngine.Debug.Log("EXE path: " + exePath);

        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("Executable not found");
            return;
        }

        Process.Start(exePath);
        UnityEngine.Debug.Log("Process.Start called");
    }
}
