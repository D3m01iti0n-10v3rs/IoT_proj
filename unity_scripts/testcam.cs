using UnityEngine;
using UnityEngine.EventSystems;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public float distance = 5.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 10.0f;

    [Header("Rotation Settings")]
    public float xSpeed = 120.0f;
    public float ySpeed = 120.0f;
    public float rotationSmoothing = 0.3f;

    [Header("Vertical Limits")]
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 2.0f;
    public float zoomSmoothing = 0.2f;

    [Header("Collision Detection")]
    public bool enableCollision = true;
    public LayerMask collisionLayers = -1;
    public float collisionOffset = 0.3f;

    [Header("Input Settings")]
    public bool invertY = false;
    public int touchInputButton = 0; // 0 = left mouse, 1 = right mouse, 2 = middle mouse
    public bool disableWhenMenuOpen = true; // New: Disable controls when UI is active

    [Header("WASD Movement Settings")]
    public bool enableMovement = true;
    public float moveSpeed = 5.0f;
    public bool enableMovementLimits = true;
    public float xMin = -50f;
    public float xMax = 50f;
    public float zMin = -50f;
    public float zMax = 50f;
    public bool showMovementGizmos = true;
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);

    [Header("Keybindings")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;

    // Camera rotation variables
    private float x = 0.0f;
    private float y = 0.0f;
    private Vector3 smoothVelocity = Vector3.zero;
    private float currentDistance;
    private float desiredDistance;
    private float zoomVelocity;

    // Camera position (for movement)
    private Vector3 cameraPosition;

    // Touch variables
    private int lastTouchCount = 0;
    private Vector2[] lastTouchPositions = new Vector2[2];

    void Start()
    {
        // Initialize camera position to current position
        cameraPosition = transform.position;
        
        // Initialize rotation from current rotation
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
        
        // Initialize distance
        currentDistance = distance;
        desiredDistance = distance;

        // Only lock cursor on non-mobile platforms
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LateUpdate()
    {
        // Check if UI is blocking input
        if (IsUIBlockingInput())
            return;
        
        // Handle all input
        HandleInput();
        
        // Update camera position based on rotation and zoom
        UpdateCamera();
    }

    bool IsUIBlockingInput()
    {
        // Check if any UI panel is active and we should disable controls
        if (disableWhenMenuOpen)
        {
            // Check if mouse is over UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;
            
            // You can add additional checks here for specific UI panels
            // For example, check if your MainMenu panel is active
        }
        
        return false;
    }

    void HandleInput()
    {
        // Handle rotation input (drag)
        HandleRotationInput();

        // Handle zoom input
        HandleZoomInput();

        // Handle WASD movement input
        if (enableMovement)
        {
            HandleMovementInput();
        }

        // Apply vertical limits
        y = ClampAngle(y, yMinLimit, yMaxLimit);
    }

    void HandleMovementInput()
    {
        Vector3 inputDirection = GetMovementInput();
        
        // Calculate movement in camera's local forward/right directions
        Vector3 forward = Quaternion.Euler(0, x, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, x, 0) * Vector3.right;
        
        // Calculate movement vector
        Vector3 movement = (forward * inputDirection.z + right * inputDirection.x) * moveSpeed * Time.deltaTime;
        
        // Update camera position
        cameraPosition += movement;
        
        // Apply movement limits
        if (enableMovementLimits)
        {
            cameraPosition.x = Mathf.Clamp(cameraPosition.x, xMin, xMax);
            cameraPosition.z = Mathf.Clamp(cameraPosition.z, zMin, zMax);
        }
    }

    Vector3 GetMovementInput()
    {
        Vector3 direction = Vector3.zero;

        // Get input from keys
        if (Input.GetKey(forwardKey)) direction.z += 1;
        if (Input.GetKey(backwardKey)) direction.z -= 1;
        if (Input.GetKey(rightKey)) direction.x += 1;
        if (Input.GetKey(leftKey)) direction.x -= 1;

        // Normalize diagonal movement
        if (direction.magnitude > 1f)
        {
            direction.Normalize();
        }
        
        return direction;
    }

    void HandleRotationInput()
    {
        if (Application.isMobilePlatform)
        {
            HandleTouchRotation();
        }
        else
        {
            HandleMouseRotation();
        }
    }

    void HandleMouseRotation()
    {
        // Check if mouse button is being dragged
        if (Input.GetMouseButton(touchInputButton))
        {
            x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
            
            float yInput = Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            y += invertY ? -yInput : yInput;
        }
    }

    void HandleTouchRotation()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == TouchPhase.Moved)
            {
                x += touch.deltaPosition.x * xSpeed * 0.002f;
                
                float yInput = touch.deltaPosition.y * ySpeed * 0.002f;
                y += invertY ? -yInput : yInput;
            }
        }
        else if (Input.touchCount == 2)
        {
            // Two-finger input for zoom
            HandleTouchZoom();
        }
    }

    void HandleZoomInput()
    {
        if (Application.isMobilePlatform)
        {
            // Mobile zoom is handled in HandleTouchRotation for two fingers
            if (Input.touchCount != 2)
            {
                // Smooth zoom to desired distance
                currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVelocity, zoomSmoothing);
            }
        }
        else
        {
            // Mouse scroll wheel zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                desiredDistance = Mathf.Clamp(desiredDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }
            
            // Smooth zoom
            currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVelocity, zoomSmoothing);
        }
    }

    void HandleTouchZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            // Apply zoom based on pinch gesture
            desiredDistance = Mathf.Clamp(desiredDistance + deltaMagnitudeDiff * zoomSpeed * 0.01f, minDistance, maxDistance);
            
            // Smooth zoom for two-finger gesture
            currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref zoomVelocity, zoomSmoothing);
        }
    }

    void UpdateCamera()
    {
        // Calculate rotation
        Quaternion rotation = Quaternion.Euler(y, x, 0);
        
        // Calculate look-at point (a point in front of the camera)
        Vector3 lookAtPoint = cameraPosition + rotation * Vector3.forward * currentDistance;
        
        // Handle collision
        if (enableCollision)
        {
            HandleCollision(ref rotation, cameraPosition, lookAtPoint);
        }
        
        // Calculate desired camera position
        Vector3 desiredPosition = cameraPosition + rotation * new Vector3(0.0f, 0.0f, -currentDistance);
        
        // Smooth damping for position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothVelocity, rotationSmoothing);
        
        // Make camera look at the point
        transform.LookAt(lookAtPoint);
    }

    void HandleCollision(ref Quaternion rotation, Vector3 cameraPos, Vector3 lookAtPoint)
    {
        if (enableCollision)
        {
            RaycastHit hit;
            Vector3 direction = (lookAtPoint - cameraPos).normalized;

            if (Physics.Raycast(cameraPos, direction, out hit, currentDistance, collisionLayers))
            {
                currentDistance = Mathf.Clamp(hit.distance - collisionOffset, minDistance, currentDistance);
            }
        }
    }

    float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

    // Draw movement limits in the Scene view
    void OnDrawGizmosSelected()
    {
        if (!showMovementGizmos || !enableMovementLimits) return;

        Gizmos.color = gizmoColor;
        
        // Draw a wireframe rectangle representing the movement boundaries
        Vector3 center = new Vector3((xMin + xMax) * 0.5f, transform.position.y, (zMin + zMax) * 0.5f);
        Vector3 size = new Vector3(xMax - xMin, 0.1f, zMax - zMin);
        
        Gizmos.DrawWireCube(center, size);
        
        // Draw a semi-transparent plane
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.2f);
        Gizmos.DrawCube(center, size);
    }

    // Public methods to control camera from other scripts
    public void SetCameraPosition(Vector3 position)
    {
        cameraPosition = position;
        if (enableMovementLimits)
        {
            cameraPosition.x = Mathf.Clamp(cameraPosition.x, xMin, xMax);
            cameraPosition.z = Mathf.Clamp(cameraPosition.z, zMin, zMax);
        }
        transform.position = cameraPosition;
    }

    public void SetMovementLimits(float minX, float maxX, float minZ, float maxZ)
    {
        xMin = minX;
        xMax = maxX;
        zMin = minZ;
        zMax = maxZ;
    }

    public void SetDistance(float newDistance)
    {
        desiredDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        currentDistance = desiredDistance;
    }

    public void ResetCamera()
    {
        // Reset rotation
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
        
        // Reset distance
        desiredDistance = distance;
        currentDistance = distance;
        
        // Reset camera position to current position
        cameraPosition = transform.position;
    }

    public void EnableMovement(bool enable)
    {
        enableMovement = enable;
    }

    // New: Enable/disable UI blocking
    public void SetUIBlocking(bool block)
    {
        disableWhenMenuOpen = block;
    }

    // Properties for external access
    public Vector3 CurrentPosition
    {
        get { return cameraPosition; }
    }

    public bool IsMoving
    {
        get 
        { 
            if (IsUIBlockingInput()) return false;
            
            Vector3 inputDir = GetMovementInput();
            return inputDir.magnitude > 0.1f && enableMovement; 
        }
    }

    public Vector3 GetMovementBoundaries()
    {
        return new Vector3(xMax - xMin, 0, zMax - zMin);
    }
}