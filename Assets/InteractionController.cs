using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;

public class InteractionController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public PlayerController playerController; 
    public EquipmentController equipmentController; 
    public Image crosshair;

    // --- EVENTS ---
    public event Action<bool, Transform> OnRotationModeChanged; 
    public event Action<bool, bool> OnAxisLockChanged; 
    public event Action<bool, HighlightableObject, string, string, bool> OnInteractableHover; 

    [Header("Interaction Settings")]
    public LayerMask grabLayer; 
    public float reachDistance = 4f;
    public float throwForce = 15f;

    [Header("Hold Settings")]
    public float holdDistance = 2.5f;       
    public float minHoldDistance = 1.0f;    
    public float maxHoldDistance = 4.0f;
    [Range(0.1f, 2.0f)] public float scrollSpeed = 0.5f;  

    [Header("Physics Settings")]
    public float snapSpeed = 50f; 
    public float maxVelocity = 20f; 

    [Header("Rotation Settings")]
    public float rotationSensitivity = 0.5f;
    public float precisionRotationMultiplier = 0.1f; 

    // Internal State
    private HighlightableObject currentHoverObject; 
    private Rigidbody heldObjectRB;
    private ItemDefinition heldPhysicsItemDef; 
    private bool isHolding = false;
    
    private float originalMass;
    private CollisionDetectionMode originalCollisionMode;
    
    public bool IsRotationMode { get; private set; } = false;
    private bool lockX = false; 
    private bool lockY = false; 

    // Input System
    private Player_Controls controls;

    private void Awake()
    {
        controls = new Player_Controls();
    }

    private void OnEnable()
    {
        controls.Enable();
        // The Object Manipulation map should only be active when holding something
        controls.ObjectManipulation.Disable(); 
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    private void Start()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (equipmentController == null) equipmentController = FindAnyObjectByType<EquipmentController>();
    }

    void Update()
    {
        if (isHolding)
        {
            HandleInputHolding();
            if (IsRotationMode) HandleRotationMode();
        }
        else
        {
            HandleHover();
            HandleInputEmpty();
        }
    }

    void FixedUpdate()
    {
        if (isHolding && heldObjectRB != null) MoveHeldObjectSnappy();
    }

    // --- INPUT: NOTHING HELD (Using Normal Map) ---
    void HandleInputEmpty()
    {
        if (controls.Normal.Interact.triggered && currentHoverObject != null)
        {
            // 1. IS IT AN EQUIPPABLE ITEM?
            EquippableItem equippable = currentHoverObject.GetComponent<EquippableItem>();
            if (equippable != null && equipmentController != null)
            {
                currentHoverObject.ToggleHighlight(false);
                OnInteractableHover?.Invoke(false, null, "", "", false);
                currentHoverObject = null;

                equipmentController.Equip(equippable);
                return;
            }

            // 2. IS IT AN INTERACTABLE? (Button, Lever)
            InteractableObject interactable = currentHoverObject.GetComponent<InteractableObject>();
            if (interactable != null)
            {
                bool hasPhysicsItem = (heldPhysicsItemDef != null && heldPhysicsItemDef == interactable.requiredItem);
                bool hasEquippedTool = (equipmentController != null && equipmentController.HasItem(interactable.requiredItem));

                ItemDefinition itemToUse = hasPhysicsItem ? heldPhysicsItemDef : (hasEquippedTool ? interactable.requiredItem : null);
                interactable.TryInteract(itemToUse);
                return; 
            }

            // 3. IS IT GRABBABLE? (Physics)
            GrabbableItem grabbable = currentHoverObject.GetComponent<GrabbableItem>();
            if (grabbable != null)
            {
                Pickup(grabbable.GetComponent<Rigidbody>());
            }
        }
    }

    // --- INPUT: HOLDING OBJECT (Using Object Manipulation Map) ---
    void HandleInputHolding()
    {
        // Drop / Put Down
        if (controls.ObjectManipulation.PutDown.triggered)
        {
            Drop();
            return; 
        }

        // Throw
        if (controls.ObjectManipulation.Throw.triggered)
        {
            Throw();
            return;
        }

        // Scroll (Move Closer/Further) - Read as 1D Axis
        float scrollValue = controls.ObjectManipulation.MoveCloserAway.ReadValue<float>();
        if (Mathf.Abs(scrollValue) > 0.1f)
        {
            float direction = Mathf.Sign(scrollValue); 
            holdDistance += direction * scrollSpeed;
            holdDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
        }

        // Toggle Rotation Mode
        if (controls.ObjectManipulation.Rotate.triggered) 
        {
            ToggleRotationMode(!IsRotationMode);
        }

        // Axis Locks (Only relevant when Rotation Mode is UI-active)
        if (IsRotationMode)
        {
            bool locksChanged = false;
            if (controls.ObjectManipulation.LockX.triggered) { lockX = !lockX; if(lockX) lockY = false; locksChanged = true; }
            if (controls.ObjectManipulation.LockY.triggered) { lockY = !lockY; if(lockY) lockX = false; locksChanged = true; }
            
            if (locksChanged) OnAxisLockChanged?.Invoke(lockX, lockY);
        }
    }

    // --- PHYSICS MOVEMENT ---
    void MoveHeldObjectSnappy()
    {
        Vector3 targetPos = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
        
        if (heldObjectRB.isKinematic)
        {
            heldObjectRB.MovePosition(Vector3.Lerp(heldObjectRB.position, targetPos, snapSpeed * Time.fixedDeltaTime));
            return;
        }

        Vector3 directionToTarget = targetPos - heldObjectRB.position;
        Vector3 targetVelocity = directionToTarget * snapSpeed;

        if (targetVelocity.magnitude > maxVelocity) targetVelocity = targetVelocity.normalized * maxVelocity;
        heldObjectRB.linearVelocity = targetVelocity;
    }

    void HandleRotationMode()
    {
        // We still use Mouse Delta for the actual movement of the object
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        // Check for precision modifier (Shift is in Normal map, but usually safe to check directly or via action)
        float speedMultiplier = Keyboard.current.leftShiftKey.isPressed ? precisionRotationMultiplier : 1f;

        float rotYaw = mouseDelta.x * rotationSensitivity * speedMultiplier;  
        float rotPitch = mouseDelta.y * rotationSensitivity * speedMultiplier; 

        if (lockY) rotYaw = 0;   
        if (lockX) rotPitch = 0; 

        if (Mathf.Abs(rotYaw) > 0) heldObjectRB.transform.Rotate(playerCamera.transform.up, -rotYaw, Space.World);
        if (Mathf.Abs(rotPitch) > 0) heldObjectRB.transform.Rotate(playerCamera.transform.right, rotPitch, Space.World);
    }

    void ToggleRotationMode(bool enable)
    {
        IsRotationMode = enable;
        
        // Lock player camera/movement via the PlayerController
        if (playerController != null) playerController.SetInputLock(enable, enable);
        
        // Fire Event for Gizmos
        Transform targetTransform = (enable && heldObjectRB != null) ? heldObjectRB.transform : null;
        OnRotationModeChanged?.Invoke(enable, targetTransform);

        if (!enable) 
        { 
            lockX = false; 
            lockY = false; 
            OnAxisLockChanged?.Invoke(false, false); 

            ActionPromptManager.Instance.HidePrompt("RotX");
            ActionPromptManager.Instance.HidePrompt("RotY");
        } else
        {
            ActionPromptManager.Instance.ShowPrompt("RotX", "Object Manipulation", "Lock X", "Toggle X Lock");
            ActionPromptManager.Instance.ShowPrompt("RotY", "Object Manipulation", "Lock Y", "Toggle Y Lock");
        }

        if (crosshair) crosshair.color = enable ? Color.blue : Color.red; 
    }

    void Pickup(Rigidbody rb)
    {
        if (rb == null) return;
        heldObjectRB = rb;
        
        // 1. INPUT MAP SWAP
        // We disable Interact so we don't trigger buttons while carrying.
        // We enable ObjectManipulation for throw/rotate/drop.
        controls.Normal.Interact.Disable(); 
        controls.ObjectManipulation.Enable();

        // 2. UI CONTEXT SWAP
        // Clear any current tool prompts FAST so the physics prompts start at the bottom.
        ActionPromptManager.Instance.ClearAll(true);
        
        // Show Physics Prompts
        ActionPromptManager.Instance.ShowPrompt("PhysDrop", "Object Manipulation", "Put Down", "Drop Object");
        ActionPromptManager.Instance.ShowPrompt("PhysThrow", "Object Manipulation", "Throw", "Throw Object");
        ActionPromptManager.Instance.ShowPrompt("PhysRotate", "Object Manipulation", "Rotate", "Rotation Mode");
        ActionPromptManager.Instance.ShowPrompt("PhysDist", "Object Manipulation", "Move Closer/Away", "Adjust Distance");

        // 3. PHYSICS SETUP
        float distanceToPlayer = Vector3.Distance(playerCamera.transform.position, heldObjectRB.transform.position);
        holdDistance = Mathf.Clamp(distanceToPlayer, minHoldDistance, maxHoldDistance);

        originalMass = heldObjectRB.mass;
        originalCollisionMode = heldObjectRB.collisionDetectionMode;

        GrabbableItem itemScript = heldObjectRB.GetComponent<GrabbableItem>();
        if (itemScript != null)
        {
            // If it's a container (basket/crate), we increase mass to make it feel heavy
            if (itemScript.isContainer)
            {
                heldObjectRB.mass = originalMass * 50f;
                heldObjectRB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        heldObjectRB.useGravity = false;
        heldObjectRB.linearDamping = 10f; 
        heldObjectRB.angularDamping = 5f; 
        heldObjectRB.constraints = RigidbodyConstraints.FreezeRotation;
        heldObjectRB.transform.parent = null;
        
        isHolding = true;
        
        // 4. NOTIFY SYSTEMS
        if (equipmentController) equipmentController.SetPhysicsHolding(true);
        
        // Hide Hover UI immediately if it was visible
        if(currentHoverObject) currentHoverObject.ToggleHighlight(false);
        OnInteractableHover?.Invoke(false, null, "", "", false); 
    }

    void Drop()
    {
        if (heldObjectRB == null) return;
        ExitGrabState();
    }

    void Throw()
    {
        if (heldObjectRB == null) return;
        Rigidbody tempRB = heldObjectRB;
        ExitGrabState(); 
        tempRB.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
    }

    void ExitGrabState()
    {
        if (heldObjectRB == null) return;

        // 1. INPUT MAP SWAP
        controls.ObjectManipulation.Disable();
        controls.Normal.Interact.Enable();

        // 2. PHYSICS RESTORE (Do this before UI to update states)
        heldObjectRB.mass = originalMass;
        heldObjectRB.collisionDetectionMode = originalCollisionMode;
        heldObjectRB.useGravity = true;
        heldObjectRB.linearDamping = 1f; 
        heldObjectRB.angularDamping = 0.05f;
        heldObjectRB.constraints = RigidbodyConstraints.None;

        if (IsRotationMode) 
        { 
            OnRotationModeChanged?.Invoke(false, null); 
            ToggleRotationMode(false); 
        }

        // 3. STATE UPDATES (Crucial order)
        isHolding = false;
        if (equipmentController) 
        {
            // Must set this to false BEFORE RefreshPrompts
            equipmentController.SetPhysicsHolding(false); 
        }

        // 4. UI CONTEXT SWAP
        // Clear Physics prompts
        ActionPromptManager.Instance.ClearAll(false);
        
        // Now refresh tool prompts (will pass the internal isPhysicsHolding check)
        if (equipmentController) 
        {
            equipmentController.RefreshPrompts();
        }

        heldObjectRB = null;
        lockX = false; 
        lockY = false;
        if(crosshair) crosshair.color = Color.white;
    }

    void HandleHover()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, reachDistance, grabLayer))
        {
            HighlightableObject item = hit.collider.GetComponentInParent<HighlightableObject>();
            
            if (item != null)
            {
                if (currentHoverObject != item)
                {
                    if (currentHoverObject != null) currentHoverObject.ToggleHighlight(false);
                    currentHoverObject = item;
                    currentHoverObject.ToggleHighlight(true);
                    if(crosshair) crosshair.color = Color.green; 
                    
                    CheckAndFireUIEvent(item);
                }
                return;
            }
        }

        if (currentHoverObject != null)
        {
            currentHoverObject.ToggleHighlight(false);
            currentHoverObject = null;
            if(crosshair) crosshair.color = Color.white;
            OnInteractableHover?.Invoke(false, null, "", "", false);
        }
    }

    void CheckAndFireUIEvent(HighlightableObject item)
    {
        bool hasName = !string.IsNullOrEmpty(item.objectName);
        bool hasVerb = !string.IsNullOrEmpty(item.interactionVerb);

        if (hasName || hasVerb)
        {
            string map = "Normal";
            string action = "Interact"; 
            bool isBlocked = false;

            InteractableObject interactable = item.GetComponent<InteractableObject>();
            if (interactable != null && interactable.requiredItem != null)
            {
                bool hasPhysicsItem = (heldPhysicsItemDef == interactable.requiredItem);
                bool hasEquippedItem = (equipmentController != null && equipmentController.HasItem(interactable.requiredItem));

                if (!hasPhysicsItem && !hasEquippedItem)
                {
                    isBlocked = true;
                    item.SetTempColor(interactable.lockedColor);
                }
                else
                {
                    item.ResetColor();
                }
            }
            else
            {
                item.ResetColor();
            }

            OnInteractableHover?.Invoke(true, item, map, action, isBlocked);
        }
        else
        {
            OnInteractableHover?.Invoke(false, null, "", "", false);
        }
    }
}