using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LoadsUIController : MonoBehaviour
{
    [Header("Main Control Button")]
    public Button showControlsButton;

    [Header("Control Panel")]
    public GameObject controlPanel;

    [Header("UI Toggles")]
    public Toggle light1Toggle;
    public Toggle light2Toggle;
    public Toggle fan1Toggle;
    public Toggle fan2Toggle;
    
    [Header("Auto Mode Toggle")]
    public Toggle autoModeToggle;

    [Header("UI Buttons")]
    public Button allOnButton;
    public Button allOffButton;
    public Button closeButton;

    [Header("Status Display")]
    public TMP_Text statusText;
    public float statusDisplayTime = 3f;

    [Header("Firebase")]
    public FirebaseDataManager firebaseManager;

    private System.Diagnostics.Stopwatch loadToggleStopwatch;
    private string lastToggledLoad = "";
    private bool isUpdatingFromFirebase = false;
    private float statusTimer = 0f;
    private bool isPanelVisible = false;
    private bool isSubscribed = false;

    void Start()
    {
        // Subscribe to Firebase events
        FirebaseDataManager.OnFirebaseInitialized += OnFirebaseReady;
        FirebaseDataManager.OnDataReceived += OnDataReceived;
        FirebaseDataManager.OnDataSaved += OnDataSaved;
        FirebaseDataManager.OnError += OnError;
        isSubscribed = true;

        // Setup toggle listeners
        if (light1Toggle != null) light1Toggle.onValueChanged.AddListener(OnLight1Toggled);
        if (light2Toggle != null) light2Toggle.onValueChanged.AddListener(OnLight2Toggled);
        if (fan1Toggle != null) fan1Toggle.onValueChanged.AddListener(OnFan1Toggled);
        if (fan2Toggle != null) fan2Toggle.onValueChanged.AddListener(OnFan2Toggled);
        
        // Setup auto mode toggle listener
        if (autoModeToggle != null) 
        {
            autoModeToggle.onValueChanged.AddListener(OnAutoModeToggled);
            UpdateToggleColor(autoModeToggle, autoModeToggle.isOn, Color.green, Color.red);
        }

        // Setup button listeners
        if (allOnButton != null) allOnButton.onClick.AddListener(TurnAllOn);
        if (allOffButton != null) allOffButton.onClick.AddListener(TurnAllOff);
        if (closeButton != null) closeButton.onClick.AddListener(HideControlPanel);
        if (showControlsButton != null) showControlsButton.onClick.AddListener(ToggleControlPanel);

        // Hide control panel by default
        if (controlPanel != null)
        {
            controlPanel.SetActive(false);
            isPanelVisible = false;
        }

        UpdateStatus("Cửa sổ điều khiển sẵn sàng", Color.white);
    }

    void Update()
    {
        if (statusText != null && statusText.gameObject.activeInHierarchy && statusTimer > 0)
        {
            statusTimer -= Time.deltaTime;
            if (statusTimer <= 0)
            {
                statusText.gameObject.SetActive(false);
            }
        }
    }

    void OnFirebaseReady(bool success)
    {
        if (success)
        {
            UpdateStatus("Đã kết nối với DB", Color.green);
            firebaseManager.ListenForData("ioState");
            firebaseManager.GetData("ioState");
            
            // Add track data listening for auto mode
            firebaseManager.ListenForData("track");
            firebaseManager.GetData("track");
        }
        else
        {
            UpdateStatus("Kết nối DB thất bại", Color.red);
        }
    }

    void OnDataReceived(string path, string jsonData)
    {
        if (string.IsNullOrEmpty(jsonData)) return;

        try
        {
            isUpdatingFromFirebase = true;

            if (path == "ioState")
            {
                var ioData = JsonUtility.FromJson<IOStateData>(jsonData);
                
                if (light1Toggle != null && light1Toggle.isOn != ioData.light1State) 
                    light1Toggle.isOn = ioData.light1State;
                if (light2Toggle != null && light2Toggle.isOn != ioData.light2State) 
                    light2Toggle.isOn = ioData.light2State;
                if (fan1Toggle != null && fan1Toggle.isOn != ioData.fan1State) 
                    fan1Toggle.isOn = ioData.fan1State;
                if (fan2Toggle != null && fan2Toggle.isOn != ioData.fan2State) 
                    fan2Toggle.isOn = ioData.fan2State;

                UpdateToggleColors();

                if (isPanelVisible)
                {
                    UpdateStatus("Trạng thái đã được cập nhật", Color.green);
                }
            }
            else if (path == "track")
            {
                var trackData = JsonUtility.FromJson<TrackData>(jsonData);
                if (autoModeToggle != null && autoModeToggle.isOn != trackData.autoMode)
                {
                    autoModeToggle.isOn = trackData.autoMode;
                    UpdateToggleColor(autoModeToggle, trackData.autoMode, Color.green, Color.red);
                }
            }

            isUpdatingFromFirebase = false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Data error: {ex.Message}");
            isUpdatingFromFirebase = false;
        }
    }

    void OnDataSaved(string path)
    {
        if (path == "ioState" && isPanelVisible)
        {
            UpdateStatus("Dữ liệu đã được cập nhật trên DB", Color.green);
            
            // Measure and print Unity-to-Firebase time
            if (loadToggleStopwatch != null && loadToggleStopwatch.IsRunning)
            {
                loadToggleStopwatch.Stop();
                long unityToFirebaseTime = loadToggleStopwatch.ElapsedMilliseconds;
                Debug.Log($"{lastToggledLoad} toggle: Unity→Firebase = {unityToFirebaseTime}ms");
            }
        }
        else if (path == "track" && isPanelVisible)
        {
            UpdateStatus("Chế độ auto đã được cập nhật", Color.green);
        }
    }

    void OnError(string error)
    {
        if (isPanelVisible)
        {
            UpdateStatus($"Lỗi: {error}", Color.red);
        }
    }

    void OnLight1Toggled(bool isOn)
    {
        if (!isUpdatingFromFirebase && firebaseManager.IsFirebaseReady())
        {
            // Start timing
            if (loadToggleStopwatch == null)
                loadToggleStopwatch = new System.Diagnostics.Stopwatch();
            loadToggleStopwatch.Restart();
            lastToggledLoad = "Light1";
            
            var update = new Dictionary<string, object> { { "light1State", isOn } };
            firebaseManager.UpdateData("ioState", update);
            UpdateToggleColor(light1Toggle, isOn, Color.green, Color.red);
        }
    }

    void OnLight2Toggled(bool isOn)
    {
        if (!isUpdatingFromFirebase && firebaseManager.IsFirebaseReady())
        {
            var update = new Dictionary<string, object> { { "light2State", isOn } };
            firebaseManager.UpdateData("ioState", update);
            UpdateToggleColor(light2Toggle, isOn, Color.green, Color.red);
        }
    }

    void OnFan1Toggled(bool isOn)
    {
        if (!isUpdatingFromFirebase && firebaseManager.IsFirebaseReady())
        {
            var update = new Dictionary<string, object> { { "fan1State", isOn } };
            firebaseManager.UpdateData("ioState", update);
            UpdateToggleColor(fan1Toggle, isOn, Color.green, Color.red);
        }
    }

    void OnFan2Toggled(bool isOn)
    {
        if (!isUpdatingFromFirebase && firebaseManager.IsFirebaseReady())
        {
            var update = new Dictionary<string, object> { { "fan2State", isOn } };
            firebaseManager.UpdateData("ioState", update);
            UpdateToggleColor(fan2Toggle, isOn, Color.green, Color.red);
        }
    }
    
    void OnAutoModeToggled(bool isOn)
    {
        if (!isUpdatingFromFirebase && firebaseManager.IsFirebaseReady())
        {
            var update = new Dictionary<string, object> { { "autoMode", isOn } };
            firebaseManager.UpdateData("track", update);
            UpdateToggleColor(autoModeToggle, isOn, Color.green, Color.red);
            UpdateStatus(isOn ? "Auto mode ON" : "Auto mode OFF", Color.yellow);
        }
    }

    void ToggleControlPanel()
    {
        if (controlPanel != null)
        {
            isPanelVisible = !isPanelVisible;
            controlPanel.SetActive(isPanelVisible);
            
            if (isPanelVisible)
            {
                firebaseManager.GetData("ioState");
                firebaseManager.GetData("track"); // Also get latest track data
            }
        }
    }

    void HideControlPanel()
    {
        if (controlPanel != null)
        {
            controlPanel.SetActive(false);
            isPanelVisible = false;
        }
    }

    void TurnAllOn()
    {
        if (firebaseManager.IsFirebaseReady())
        {
            var update = new Dictionary<string, object>
            {
                { "light1State", true },
                { "light2State", true },
                { "fan1State", true },
                { "fan2State", true }
            };
            firebaseManager.UpdateData("ioState", update);
            UpdateStatus("Đã bật tất cả các tải", Color.green);
        }
    }

    void TurnAllOff()
    {
        if (firebaseManager.IsFirebaseReady())
        {
            var update = new Dictionary<string, object>
            {
                { "light1State", false },
                { "light2State", false },
                { "fan1State", false },
                { "fan2State", false }
            };
            firebaseManager.UpdateData("ioState", update);
            UpdateStatus("Đã tắt tất cả các tải", Color.green);
        }
    }

    void UpdateToggleColors()
    {
        UpdateToggleColor(light1Toggle, light1Toggle.isOn, Color.green, Color.red);
        UpdateToggleColor(light2Toggle, light2Toggle.isOn, Color.green, Color.red);
        UpdateToggleColor(fan1Toggle, fan1Toggle.isOn, Color.green, Color.red);
        UpdateToggleColor(fan2Toggle, fan2Toggle.isOn, Color.green, Color.red);
    }

    void UpdateToggleColor(Toggle toggle, bool isOn, Color onColor, Color offColor)
    {
        if (toggle != null)
        {
            var colors = toggle.colors;
            colors.normalColor = isOn ? onColor : offColor;
            colors.highlightedColor = isOn ? onColor : offColor;
            colors.pressedColor = isOn ? onColor * 0.8f : offColor * 0.8f;
            colors.selectedColor = isOn ? onColor : offColor;
            toggle.colors = colors;
        }
    }

    void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
            statusText.gameObject.SetActive(true);
            statusTimer = statusDisplayTime;
        }
    }

    void OnDestroy()
    {
        // OPTIMIZED: Comprehensive cleanup
        if (isSubscribed)
        {
            FirebaseDataManager.OnFirebaseInitialized -= OnFirebaseReady;
            FirebaseDataManager.OnDataReceived -= OnDataReceived;
            FirebaseDataManager.OnDataSaved -= OnDataSaved;
            FirebaseDataManager.OnError -= OnError;
            isSubscribed = false;
        }

        // Remove toggle listeners
        if (light1Toggle != null) light1Toggle.onValueChanged.RemoveListener(OnLight1Toggled);
        if (light2Toggle != null) light2Toggle.onValueChanged.RemoveListener(OnLight2Toggled);
        if (fan1Toggle != null) fan1Toggle.onValueChanged.RemoveListener(OnFan1Toggled);
        if (fan2Toggle != null) fan2Toggle.onValueChanged.RemoveListener(OnFan2Toggled);
        
        // Remove auto mode toggle listener
        if (autoModeToggle != null) autoModeToggle.onValueChanged.RemoveListener(OnAutoModeToggled);

        // Remove button listeners
        if (allOnButton != null) allOnButton.onClick.RemoveListener(TurnAllOn);
        if (allOffButton != null) allOffButton.onClick.RemoveListener(TurnAllOff);
        if (closeButton != null) closeButton.onClick.RemoveListener(HideControlPanel);
        if (showControlsButton != null) showControlsButton.onClick.RemoveListener(ToggleControlPanel);

        // Stop Firebase listening
        if (firebaseManager != null)
        {
            firebaseManager.StopListening("ioState");
            firebaseManager.StopListening("track");
        }
    }

    [System.Serializable]
    private class IOStateData
    {
        public bool doorState;
        public bool fan1State;
        public bool fan2State;
        public bool light1State;
        public bool light2State;
    }
    
    [System.Serializable]
    private class TrackData
    {
        public bool autoMode;
    }
}