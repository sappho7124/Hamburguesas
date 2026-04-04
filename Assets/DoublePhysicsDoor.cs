using UnityEngine;

public class DoublePhysicsDoor : MonoBehaviour
{[System.Serializable]
    public class DoorData
    {
        public Rigidbody rb;
        public HingeJoint hinge;[Tooltip("Use a positive number for one door (e.g. 90) and negative for the other (e.g. -90)")]
        public float openAngle = 90f;
        
        [HideInInspector] public bool isOpen = false;
        [HideInInspector] public Quaternion closedRotation;
    }

    [Header("Left Door")]
    public DoorData leftDoor;

    [Header("Right Door")]
    public DoorData rightDoor;

    [Header("Motor Settings")]
    public float motorVelocity = 200f;
    public float motorForce = 150f;
    public float closeThreshold = 2f; // Angle at which the door "latches" shut

    [Header("UI Prompts")]
    public HighlightableObject doorHighlight;
    public string openVerb = "Open Doors";
    public string closeVerb = "Close Doors";

    void Awake()
    {
        InitializeDoor(leftDoor);
        InitializeDoor(rightDoor);
    }

    private void InitializeDoor(DoorData door)
    {
        if (door.rb == null || door.hinge == null) return;
        door.closedRotation = door.rb.transform.localRotation;
        door.hinge.useLimits = true;
        LockDoor(door);
    }

    // Link this to your InteractableObject's UnityEvent!
    public void ToggleDoors()
    {
        // If either door is currently open, clicking the fridge closes them. 
        // Otherwise, it pushes both open.
        if (leftDoor.isOpen || rightDoor.isOpen)
        {
            if (leftDoor.isOpen) CloseDoorWithMotor(leftDoor);
            if (rightDoor.isOpen) CloseDoorWithMotor(rightDoor);
        }
        else
        {
            OpenDoor(leftDoor);
            OpenDoor(rightDoor);
        }
    }

    private void OpenDoor(DoorData door)
    {
        if (door.rb == null || door.hinge == null) return;
        
        door.isOpen = true;
        door.rb.isKinematic = false; // Wake up physics

        JointLimits limits = door.hinge.limits;
        
        // Hinge limits in Unity must always have min < max. 
        // We handle negative open angles (like -90) here:
        if (door.openAngle < 0) { limits.min = door.openAngle; limits.max = 0; }
        else { limits.min = 0; limits.max = door.openAngle; }
        door.hinge.limits = limits;

        // Activate Motor
        door.hinge.useMotor = true;
        JointMotor motor = door.hinge.motor;
        // If the angle is negative, the motor needs a negative velocity to push it that way
        motor.targetVelocity = door.openAngle > 0 ? motorVelocity : -motorVelocity;
        motor.force = motorForce;
        door.hinge.motor = motor;

        UpdatePrompt();
    }

    private void CloseDoorWithMotor(DoorData door)
    {
        if (door.rb == null || door.hinge == null) return;
        door.hinge.useMotor = true;
        JointMotor motor = door.hinge.motor;
        // Reverse the velocity to pull it shut
        motor.targetVelocity = door.openAngle > 0 ? -motorVelocity : motorVelocity;
        motor.force = motorForce;
        door.hinge.motor = motor;
    }

    void Update()
    {
        ProcessDoorPhysics(leftDoor);
        ProcessDoorPhysics(rightDoor);
    }

    private void ProcessDoorPhysics(DoorData door)
    {
        if (!door.isOpen || door.hinge == null) return;

        // 1. FREE-SWINGING PHYSICS: Turn motor off when it finishes opening
        if (door.hinge.useMotor)
        {
            float currentAngle = door.hinge.angle;
            bool reachedOpen = false;

            // Check if motor finished pushing positively or negatively
            if (door.openAngle > 0 && currentAngle >= door.openAngle - 5f && door.hinge.motor.targetVelocity > 0)
                reachedOpen = true;
            else if (door.openAngle < 0 && currentAngle <= door.openAngle + 5f && door.hinge.motor.targetVelocity < 0)
                reachedOpen = true;

            if (reachedOpen) door.hinge.useMotor = false;
        }

        // 2. LATCHING SHUT: If pushed shut via physics or motor
        if (Mathf.Abs(door.hinge.angle) <= closeThreshold)
        {
            LockDoor(door);
        }
    }

    private void LockDoor(DoorData door)
    {
        if (door.rb == null || door.hinge == null) return;
        
        door.isOpen = false;
        door.hinge.useMotor = false;

        // Lock the joint
        JointLimits limits = door.hinge.limits;
        limits.min = 0;
        limits.max = 0;
        door.hinge.limits = limits;

        // Freeze the door so bumping it doesn't jiggle it
        door.rb.isKinematic = true;
        door.rb.transform.localRotation = door.closedRotation;

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (doorHighlight != null)
        {
            doorHighlight.interactionVerb = (leftDoor.isOpen || rightDoor.isOpen) ? closeVerb : openVerb;
        }
    }
}