using UnityEngine;

public class LightController : MonoBehaviour
{
    [Header("Physical Light Objects")]
    public GameObject physicalLight1;
    public GameObject physicalLight2;

    [Header("Light Indicator Cubes")]
    public GameObject light1Indicator;
    public GameObject light2Indicator;

    [Header("Light Materials")]
    public Material greenMaterial;
    public Material redMaterial;

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    // Cache components and state
    private Renderer light1Renderer;
    private Renderer light2Renderer;
    private bool lastLight1State = false;
    private bool lastLight2State = false;
    private bool isSubscribed = false;

    void Start()
    {
        // Cache renderer components
        if (light1Indicator != null) light1Renderer = light1Indicator.GetComponent<Renderer>();
        if (light2Indicator != null) light2Renderer = light2Indicator.GetComponent<Renderer>();

        SetIndicatorColor(light1Renderer, false);
        SetIndicatorColor(light2Renderer, false);

        if (physicalLight1 != null) physicalLight1.SetActive(false);
        if (physicalLight2 != null) physicalLight2.SetActive(false);

        FirebaseDataManager.OnDataReceived += OnDataReceived;
        isSubscribed = true;
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (path != "ioState" || string.IsNullOrEmpty(jsonData)) return;

        try
        {
            var ioData = JsonUtility.FromJson<IOStateData>(jsonData);
            
            if (ioData.light1State != lastLight1State)
            {
                SetIndicatorColor(light1Renderer, ioData.light1State);
                SetPhysicalLight(physicalLight1, ioData.light1State);
                lastLight1State = ioData.light1State;
            }

            if (ioData.light2State != lastLight2State)
            {
                SetIndicatorColor(light2Renderer, ioData.light2State);
                SetPhysicalLight(physicalLight2, ioData.light2State);
                lastLight2State = ioData.light2State;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Light data error: {ex.Message}");
        }
    }

    void SetIndicatorColor(Renderer renderer, bool isOn)
    {
        if (renderer != null)
        {
            renderer.material = isOn ? greenMaterial : redMaterial;
        }
    }

    void SetPhysicalLight(GameObject lightObject, bool isOn)
    {
        if (lightObject != null)
        {
            lightObject.SetActive(isOn);
        }
    }

    void OnDestroy()
    {
        if (isSubscribed)
        {
            FirebaseDataManager.OnDataReceived -= OnDataReceived;
            isSubscribed = false;
        }
    }

    [System.Serializable]
    private class IOStateData
    {
        public bool light1State;
        public bool light2State;
    }
}