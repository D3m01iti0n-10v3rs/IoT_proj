using UnityEngine;

public class FanManager : MonoBehaviour
{
    [Header("Fan Objects")]
    public GameObject[] fanObjects;

    [Header("Visual Feedback")]
    public Material fanOnMaterial;
    public Material fanOffMaterial;

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    private SimpleFanSpin[] fanSpinners;
    private Renderer[] fanBaseRenderers;
    private bool[] lastFanStates;
    private bool isSubscribed = false;
    private bool componentsCached = false;

    void Start()
    {
        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;
        isSubscribed = true;
        
        InitializeArrays();
        CacheFanComponents();
        InitializeAllFans();
    }

    void InitializeArrays()
    {
        if (fanObjects != null)
        {
            fanSpinners = new SimpleFanSpin[fanObjects.Length];
            fanBaseRenderers = new Renderer[fanObjects.Length];
            lastFanStates = new bool[fanObjects.Length];
        }
        else
        {
            Debug.LogError("FanManager: fanObjects array is not assigned!");
        }
    }

    void CacheFanComponents()
    {
        if (fanObjects == null) return;

        for (int i = 0; i < fanObjects.Length; i++)
        {
            if (fanObjects[i] != null)
            {
                Transform dynamicPart = fanObjects[i].transform.Find("dynamic");
                if (dynamicPart != null)
                {
                    fanSpinners[i] = dynamicPart.GetComponent<SimpleFanSpin>();
                    if (fanSpinners[i] == null)
                    {
                        fanSpinners[i] = dynamicPart.gameObject.AddComponent<SimpleFanSpin>();
                    }
                }

                Transform staticPart = fanObjects[i].transform.Find("static");
                if (staticPart != null)
                {
                    foreach (Transform child in staticPart)
                    {
                        if (child.name.StartsWith("base"))
                        {
                            fanBaseRenderers[i] = child.GetComponent<Renderer>();
                            break;
                        }
                    }
                }
            }
        }
        componentsCached = true;
    }

    void OnFirebaseReady(bool success)
    {
        if (success)
        {
            firebaseManager.ListenForData("ioState");
        }
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (path == "ioState" && !string.IsNullOrEmpty(jsonData))
        {
            ParseFanData(jsonData);
        }
    }

    void ParseFanData(string jsonData)
    {
        try
        {
            IOStateData data = JsonUtility.FromJson<IOStateData>(jsonData);
            
            if (data != null)
            {
                if (fanObjects.Length > 0 && fanObjects[0] != null)
                {
                    SetFanState(0, data.fan1State);
                }

                if (fanObjects.Length > 1 && fanObjects[1] != null)
                {
                    SetFanState(1, data.fan2State);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FanManager error: {ex.Message}");
        }
    }

    void SetFanState(int fanIndex, bool isOn)
    {
        if (!componentsCached || fanSpinners == null || fanBaseRenderers == null || lastFanStates == null)
        {
            Debug.LogWarning("FanManager: Components not cached yet, skipping fan state update");
            return;
        }

        if (fanIndex < 0 || fanIndex >= lastFanStates.Length)
        {
            Debug.LogError($"FanManager: Invalid fan index {fanIndex}");
            return;
        }

        // Only update if state changed
        if (lastFanStates[fanIndex] == isOn) return;
        lastFanStates[fanIndex] = isOn;

        if (fanSpinners[fanIndex] != null)
        {
            if (isOn)
                fanSpinners[fanIndex].StartSpinning();
            else
                fanSpinners[fanIndex].StopSpinning();
        }
        else
        {
            Debug.LogWarning($"FanManager: No spinner found for fan {fanIndex}");
        }

        if (fanBaseRenderers[fanIndex] != null)
        {
            if (fanOnMaterial != null && fanOffMaterial != null)
            {
                fanBaseRenderers[fanIndex].material = isOn ? fanOnMaterial : fanOffMaterial;
            }
            else
            {
                if (fanBaseRenderers[fanIndex].material != null)
                {
                    fanBaseRenderers[fanIndex].material.color = isOn ? Color.green : Color.red;
                }
            }
        }
        else
        {
            Debug.LogWarning($"FanManager: No base renderer found for fan {fanIndex}");
        }
    }

    void InitializeAllFans()
    {
        if (!componentsCached) return;

        for (int i = 0; i < fanObjects.Length; i++)
        {
            if (fanObjects[i] != null)
            {
                SetFanState(i, false);
            }
        }
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
            firebaseManager.StopListening("ioState");
    }

    [System.Serializable]
    public class IOStateData
    {
        public bool doorState;
        public bool fan1State;
        public bool fan2State;
        public bool light1State;
        public bool light2State;
    }
}