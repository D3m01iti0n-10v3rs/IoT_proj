using UnityEngine;
using System.Collections;

public class GlobalMemoryMonitor : MonoBehaviour
{
    [Header("Memory Monitoring")]
    public bool enableMemoryLogging = false;
    public float memoryCheckInterval = 5f;
    
    private void Start()
    {
        if (enableMemoryLogging)
        {
            StartCoroutine(MemoryMonitorCoroutine());
        }
    }
    
    private IEnumerator MemoryMonitorCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(memoryCheckInterval);
            
            long totalMemory = System.GC.GetTotalMemory(false) / 1024 / 1024;
            long allocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
            long reservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024;
            
            Debug.Log($"Memory Usage - Total: {totalMemory}MB, Allocated: {allocatedMemory}MB, Reserved: {reservedMemory}MB");
            
            if (totalMemory > 1000) // If over 1GB
            {
                Debug.LogWarning("High memory usage detected, forcing GC...");
                System.GC.Collect();
            }
        }
    }
}