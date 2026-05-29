using UnityEngine;
using UnityEngine.InputSystem; // <-- Added the New Input System namespace

public class MainMenuCameraSway : MonoBehaviour
{
    [Header("Sway Settings")]
    [Tooltip("How many degrees the camera will rotate towards the mouse.")]
    public float rotationAmount = 3f;
    
    [Tooltip("How much the camera will physically move left/right/up/down.")]
    public float positionAmount = 0.05f; 
    
    [Tooltip("How smooth and floaty the movement feels. Lower = slower/smoother.")]
    public float smoothSpeed = 3f;

    private Quaternion startRotation;
    private Vector3 startPosition;

    void Start()
    {
        // Save the camera's exact starting position and rotation in the scene
        startRotation = transform.localRotation;
        startPosition = transform.localPosition;
    }

    void Update()
    {
        // Safety check: Ensure a mouse is actually connected
        if (Mouse.current == null) return;

        // 1. Get Mouse Position from the New Input System
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Convert it to a range between -1 and 1 (0,0 is the dead center of the screen)
        float mouseX = (mousePos.x / Screen.width) * 2f - 1f;
        float mouseY = (mousePos.y / Screen.height) * 2f - 1f;

        // Clamp the values so the camera doesn't spin out of control if the mouse leaves the game window
        mouseX = Mathf.Clamp(mouseX, -1f, 1f);
        mouseY = Mathf.Clamp(mouseY, -1f, 1f);

        // 2. Calculate the Target Rotation
        // Mouse Up (Positive Y) pitches camera UP (Negative X axis)
        // Mouse Right (Positive X) yaws camera RIGHT (Positive Y axis)
        Quaternion targetRotation = startRotation * Quaternion.Euler(-mouseY * rotationAmount, mouseX * rotationAmount, 0f);

        // 3. Calculate the Target Position (adds extra 3D depth)
        Vector3 targetPosition = startPosition + new Vector3(mouseX * positionAmount, mouseY * positionAmount, 0f);

        // 4. Apply smooth transitions using Lerp
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * smoothSpeed);
    }
}