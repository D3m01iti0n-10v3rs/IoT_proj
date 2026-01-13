using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject menuPanel;  // The entire menu panel
    public Button enterButton;    // Enter button (starts the app)
    public Button exitButton;     // Exit button (quits the app)
    public Button menuToggleButton; // Button to show menu during gameplay

    [Header("Camera Reference")]
    public ThirdPersonCamera cameraController;

    void Start()
    {
        // Show the menu when the app starts
        ShowMenu();
        
        // Setup button listeners
        enterButton.onClick.AddListener(OnEnterClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        menuToggleButton.onClick.AddListener(OnMenuToggleClicked);
        
        // Hide the menu toggle button initially (it only shows after entering)
        menuToggleButton.gameObject.SetActive(false);
    }

    void ShowMenu()
    {
        menuPanel.SetActive(true);
        menuToggleButton.gameObject.SetActive(false);
        
        // Disable camera controls when menu is open
        if (cameraController != null)
        {
            cameraController.enabled = false;
        }
        
        // Force cursor to be visible and unlocked when menu is shown
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void HideMenu()
    {
        menuPanel.SetActive(false);
        menuToggleButton.gameObject.SetActive(true);
        
        // Enable camera controls
        if (cameraController != null)
        {
            cameraController.enabled = true;
        }
        
        // Don't lock cursor here - let the camera script handle it
        // The camera script will lock the cursor when it starts working
    }

    void OnEnterClicked()
    {
        Debug.Log("Enter clicked - Starting app");
        HideMenu();
    }

    void OnExitClicked()
    {
        Debug.Log("Exit clicked - Quitting app");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    void OnMenuToggleClicked()
    {
        ShowMenu();
    }
}