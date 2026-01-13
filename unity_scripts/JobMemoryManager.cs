using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;

public class JobMemoryManager : MonoBehaviour
{
    private static JobMemoryManager instance;
    
    [Header("Memory Settings")]
    public bool enableMemoryValidation = true;
    public int maxFrameAllocation = 5000; // Increased to 5MB threshold
    public float memoryCheckInterval = 2f; // Check every 2 seconds instead of every frame
    
    [Header("Debug Settings")]
    public bool logMemoryUsage = false;
    
    private long lastFrameMemory;
    private int highMemoryFrames;
    private float lastCheckTime;
    
    public static JobMemoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<JobMemoryManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("JobMemoryManager");
                    instance = obj.AddComponent<JobMemoryManager>();
                    DontDestroyOnLoad(obj);
                }
            }
            return instance;
        }
    }
    
    void Start()
    {
        if (enableMemoryValidation)
        {
            StartCoroutine(MemoryMonitorCoroutine());
        }
    }
    
    void Update()
    {
        // Reduced frequency of checks to avoid performance impact
        if (!enableMemoryValidation) return;
        
        if (Time.time - lastCheckTime >= memoryCheckInterval)
        {
            lastCheckTime = Time.time;
            CheckMemoryUsage();
        }
    }
    
    private IEnumerator MemoryMonitorCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(memoryCheckInterval);
            
            if (enableMemoryValidation)
            {
                CheckMemoryUsage();
            }
        }
    }
    
    private void CheckMemoryUsage()
    {
        long currentMemory = GetJobSystemMemory();
        
        if (logMemoryUsage)
        {
            Debug.Log($"Job System Memory: {currentMemory}KB");
        }
        
        if (currentMemory > maxFrameAllocation)
        {
            highMemoryFrames++;
            
            // Only trigger cleanup if high memory persists for multiple checks
            if (highMemoryFrames >= 3)
            {
                Debug.LogWarning($"Persistent high job system memory: {currentMemory}KB - Forcing cleanup");
                ClearJobSystemTempMemory();
                highMemoryFrames = 0;
            }
        }
        else
        {
            highMemoryFrames = 0; // Reset counter if memory is normal
        }
        
        lastFrameMemory = currentMemory;
    }
    
    private long GetJobSystemMemory()
    {
        try
        {
            // More accurate memory measurement
            return (UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() + 
                   UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()) / 1024;
        }
        catch
        {
            return 0;
        }
    }
    
    public void ClearJobSystemTempMemory()
    {
        try
        {
            // Complete any pending jobs
            JobHandle.ScheduleBatchedJobs();
            
            // Force garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            
            // Additional Unity-specific cleanup
            Resources.UnloadUnusedAssets();
            
            if (logMemoryUsage)
            {
                long afterMemory = GetJobSystemMemory();
                Debug.Log($"Memory cleanup completed. After: {afterMemory}KB");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Memory cleanup warning: {ex.Message}");
        }
    }
    
    // Call this when you know heavy operations are done
    public void RequestMemoryCleanup()
    {
        ClearJobSystemTempMemory();
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) // When app goes to background
        {
            ClearJobSystemTempMemory();
        }
    }
    
    void OnDestroy()
    {
        ClearJobSystemTempMemory();
    }
}