using UnityEngine;

public class WASDMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;

    [Header("Movement Limits")]
    public bool enableLimits = true;
    public float xMin = -20f;
    public float xMax = 20f;
    public float zMin = -20f;
    public float zMax = 20f;
    public bool showGizmos = true;
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);

    [Header("Input Settings")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;

    void Update()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        Vector3 inputDirection = GetInputDirection();
        
        // Calculate movement
        Vector3 movement = inputDirection * moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;
        
        // Apply limits
        if (enableLimits)
        {
            newPosition.x = Mathf.Clamp(newPosition.x, xMin, xMax);
            newPosition.z = Mathf.Clamp(newPosition.z, zMin, zMax);
        }

        // Apply movement
        transform.position = newPosition;
    }

    Vector3 GetInputDirection()
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

    // Public methods to control movement from other scripts
    public void MoveToPosition(Vector3 position)
    {
        if (enableLimits)
        {
            position.x = Mathf.Clamp(position.x, xMin, xMax);
            position.z = Mathf.Clamp(position.z, zMin, zMax);
        }
        
        transform.position = position;
    }

    public void SetLimits(float minX, float maxX, float minZ, float maxZ)
    {
        xMin = minX;
        xMax = maxX;
        zMin = minZ;
        zMax = maxZ;
    }

    public void EnableMovement(bool enable)
    {
        enabled = enable;
    }

    // Draw movement limits in the Scene view
    void OnDrawGizmosSelected()
    {
        if (!showGizmos || !enableLimits) return;

        Gizmos.color = gizmoColor;
        
        // Draw a wireframe rectangle representing the movement boundaries
        Vector3 center = new Vector3((xMin + xMax) * 0.5f, transform.position.y, (zMin + zMax) * 0.5f);
        Vector3 size = new Vector3(xMax - xMin, 0.1f, zMax - zMin);
        
        Gizmos.DrawWireCube(center, size);
        
        // Draw a semi-transparent plane
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.2f);
        Gizmos.DrawCube(center, size);
    }

    // Properties for external access
    public bool IsMoving
    {
        get 
        { 
            Vector3 inputDir = GetInputDirection();
            return inputDir.magnitude > 0.1f; 
        }
    }

    public Vector3 GetMovementBoundaries()
    {
        return new Vector3(xMax - xMin, 0, zMax - zMin);
    }
}