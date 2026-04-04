using UnityEngine.Events;
using UnityEngine;
using System;[RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
public class PhysicsDoor : MonoBehaviour
{
    [Header("Door Settings")][Tooltip("How far the door opens. Use negative numbers to swing the opposite way.")]
    public float openAngle = 90f;
    
    [Tooltip("How strongly the door pushes itself open/closed.")]
    public float springForce = 200f;[Tooltip("Higher dampening slows the door down as it reaches the end, creating a smooth stop without bouncing.")]
    public float springDampening = 20f;
    
    [Header("UI Prompts")]
    public HighlightableObject doorHighlight;
    public string openVerb = "Open";
    public string closeVerb = "Close";

    [Header("Events")]
    public UnityEvent OnDoorClosed;

    private HingeJoint hinge;
    private Rigidbody rb;
    private bool isTargetOpen = false; 
    private float closeThreshold = 2f; 
    private Quaternion closedRotation;

    // --- NEW VARIABLES FOR FIX ---
    private bool hasLeftClosedZone = false; 
    private float timeTryingToOpen = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();
        closedRotation = transform.localRotation;

        hinge.useLimits = true;
        LockDoor(); 
    }

    public void ToggleDoor()
    {
        isTargetOpen = !isTargetOpen;
        
        if (isTargetOpen)
            OpenDoor();
        else
            CloseDoor();
    }

    private void OpenDoor()
    {
        rb.isKinematic = false;
        
        // Reset our safety checks
        hasLeftClosedZone = false;
        timeTryingToOpen = 0f;

        JointLimits limits = hinge.limits;
        if (openAngle < 0) { limits.min = openAngle; limits.max = 0; }
        else { limits.min = 0; limits.max = openAngle; }
        hinge.limits = limits;

        hinge.useMotor = false;
        hinge.useSpring = true;
        
        JointSpring spring = hinge.spring;
        spring.targetPosition = openAngle;
        spring.spring = springForce;
        spring.damper = springDampening;
        hinge.spring = spring;
        
        //Debug.Log("Opening fridge");
        UpdatePrompt();
    }

    private void CloseDoor()
    {
        rb.isKinematic = false; 

        hinge.useMotor = false;
        hinge.useSpring = true;
        
        JointSpring spring = hinge.spring;
        spring.targetPosition = 0f;
        spring.spring = springForce;
        spring.damper = springDampening;
        hinge.spring = spring;

        //Debug.Log("Closing fridge");
        UpdatePrompt();
    }

    void Update()
    {
        if (isTargetOpen)
        {
            // 1. Check if the door successfully moved past the threshold
            if (!hasLeftClosedZone && Mathf.Abs(hinge.angle) > closeThreshold)
            {
                hasLeftClosedZone = true;
            }

            // 2. STUCK LOGIC: If it hasn't left the zone after 0.5 seconds, it's blocked!
            if (!hasLeftClosedZone)
            {
                timeTryingToOpen += Time.deltaTime;
                if (timeTryingToOpen > 0.5f)
                {
                    Debug.LogWarning("Fridge attempt failed: The door is physically blocked and cannot open!");
                    timeTryingToOpen = -999f; // Set to massive negative so it doesn't spam the console every frame
                }
            }

            // 3. Turn off the spring once it reaches the open angle to allow free-swinging physics
            if (hinge.useSpring && Mathf.Abs(hinge.angle - openAngle) <= 5f)
            {
                hinge.useSpring = false;
            }

            // 4. LATCH SHUT: ONLY trigger this if the door had actually made it out of the zone first!
            if (hasLeftClosedZone && Mathf.Abs(hinge.angle) <= closeThreshold)
            {
                isTargetOpen = false;
                LockDoor();
            }
        }
        else
        {
            // Latch the door completely shut when it reaches 0 (while targeting closed)
            if (!rb.isKinematic && Mathf.Abs(hinge.angle) <= closeThreshold)
            {
                LockDoor();
            }
        }
    }

    private void LockDoor()
    {
        isTargetOpen = false;
        hinge.useSpring = false;

        JointLimits limits = hinge.limits;
        limits.min = 0;
        limits.max = 0;
        hinge.limits = limits;

        rb.isKinematic = true;
        transform.localRotation = closedRotation;

        UpdatePrompt();
        OnDoorClosed?.Invoke();
        //Debug.Log("Locked door");
    }

    private void UpdatePrompt()
    {
        if (doorHighlight != null)
        {
            doorHighlight.interactionVerb = isTargetOpen ? closeVerb : openVerb;
        }
    }
}