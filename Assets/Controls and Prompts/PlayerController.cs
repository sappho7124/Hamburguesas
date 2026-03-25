using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f; 
    public float crouchSpeed = 2.5f; 
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    
    [Header("Crouch Settings")]
    public float standHeight = 2.0f;
    public float crouchHeight = 1.0f;
    public float crouchTransitionSpeed = 10f;
    public LayerMask obstacleLayer; // Set this to "Default" or "Ground" to detect ceilings

    [Header("Physics Interaction")]
    public float pushPower = 2.0f;
    
    [Header("Look Settings")]
    public Camera playerCamera;
    public float mouseSensitivity = 0.1f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;
    
    // Crouch State
    private bool isCrouchToggled = false; 

    // Locking Flags
    private bool isMovementLocked = false;
    private bool isLookLocked = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        
        // Force initial state
        controller.height = standHeight;
        controller.center = new Vector3(0, standHeight / 2f, 0);
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // --- 1. Ground Check ---
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }

        // --- 2. Look Logic ---
        if (!isLookLocked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        // --- 3. Movement & Crouch Logic ---
        if (!isMovementLocked)
        {
            // Determine Crouch State
            bool wantsToCrouch = isCrouchToggled || Keyboard.current.leftCtrlKey.isPressed;
            
            // Toggle Logic
            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                isCrouchToggled = !isCrouchToggled;
                // Update local variable immediately for this frame
                wantsToCrouch = isCrouchToggled || Keyboard.current.leftCtrlKey.isPressed;
            }

            HandleCrouch(wantsToCrouch);
            HandleMovement(wantsToCrouch);
        }

        // --- 4. Gravity ---
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleMovement(bool isCrouching)
    {
        float x = 0;
        float z = 0;
        
        if (Keyboard.current.dKey.isPressed) x += 1;
        if (Keyboard.current.aKey.isPressed) x -= 1;
        if (Keyboard.current.wKey.isPressed) z += 1;
        if (Keyboard.current.sKey.isPressed) z -= 1;

        float currentSpeed = walkSpeed;
        
        if (isCrouching) 
        {
            currentSpeed = crouchSpeed;
        }
        else if (Keyboard.current.leftShiftKey.isPressed)
        {
            currentSpeed = runSpeed;
        }

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Jump (Only if grounded AND NOT crouching)
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void HandleCrouch(bool wantsToCrouch)
    {
        // CEILING CHECK:
        // If we want to stand up (!wantsToCrouch), check if there is a ceiling above us.
        // If there is, force us to stay crouched.
        if (!wantsToCrouch && IsHeadBlocked())
        {
            wantsToCrouch = true; // Forced crouch
        }

        float targetHeight = wantsToCrouch ? crouchHeight : standHeight;
        float currentHeight = controller.height;

        // If we are not at the target height, move towards it
        if (Mathf.Abs(currentHeight - targetHeight) > 0.001f)
        {
            float newHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            
            // Apply Height
            controller.height = newHeight;
            
            // Apply Center (Always half of height to keep feet on ground)
            controller.center = new Vector3(0, newHeight / 2f, 0);
        }
        else
        {
            // Snap to exact values to prevent drift
            if (currentHeight != targetHeight)
            {
                controller.height = targetHeight;
                controller.center = new Vector3(0, targetHeight / 2f, 0);
            }
        }
    }
    
    // Cast a sphere up to see if we can stand
    bool IsHeadBlocked()
    {
        // Start ray at center of character
        Vector3 start = transform.position + controller.center;
        
        // Calculate distance to top of standing height
        float distanceToTop = standHeight - controller.center.y;
        
        // Raycast up
        // We use a small sphere cast for better detection than a thin ray
        return Physics.SphereCast(start, controller.radius * 0.9f, Vector3.up, out RaycastHit hit, distanceToTop, obstacleLayer);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.linearVelocity = pushDir * pushPower;
    }

    public void SetInputLock(bool lockMovement, bool lockCamera)
    {
        isMovementLocked = lockMovement;
        isLookLocked = lockCamera;
    }
}