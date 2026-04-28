using UnityEngine;
using UnityEngine.InputSystem;

public class EquipmentController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public InteractionController interactionController;
    
    [Header("Hand Settings")]
    public Transform handMount; 
    public float swayAmount = 0.05f;
    public float swaySmooth = 8f;[Header("Throwing/Placing")]
    public float throwForce = 15f;
    public float placeDistance = 2f;
    public Material ghostMaterial; 

    private Player_Controls controls;
    private EquippableItem currentEquippedItem;
    private bool isPhysicsHolding = false; 
    private bool isPlacingMode = false;    
    
    private GameObject ghostObject; 
    private Vector3 defaultHandPos;
    private Vector3 loweredHandPos;
    private int itemOriginalLayer;

    void Awake()
    {
        controls = new Player_Controls();
        defaultHandPos = Vector3.zero;
        loweredHandPos = new Vector3(0, -1.0f, 0); 
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    void Update()
    {
        HandleHandAnimation();
        HandleInput();
        HandlePlacementPreview();
    }

    public void Equip(EquippableItem item)
    {
        // 1. If we are already holding something, drop it first
        if (currentEquippedItem != null) 
        {
            Drop(); 
        }

        currentEquippedItem = item;
        
        // 2. Capture Original Layer 
        // Crucial so we can restore it when dropping, allowing it to be highlighted again.
        itemOriginalLayer = item.gameObject.layer;

        // 3. Disable Physics & Visuals for "Hand Mode" INSTANTLY
        // This stops collisions immediately so it doesn't hit the player or environment while moving
        item.SetPhysics(false);
        
        // 4. Change Layer to Ignore Raycast
        // Prevents the player's own interaction raycast from hitting the tool in their hand
        SetLayerRecursive(item.gameObject, LayerMask.NameToLayer("Ignore Raycast"));

        // 5. START TRANSITION: Move smoothly to hand offset
        item.StartTransition(handMount, item.handPositionOffset, Quaternion.Euler(item.handRotationOffset), true, () => {
            // Callback: Reached hand, nothing extra needed here.
        });

        // 6. UI PROMPTS
        // Only show these if we aren't currently busy holding a physics object
        if (!isPhysicsHolding)
        {
            RefreshPrompts();
        }
    }

    public void Drop()
    {
        if (currentEquippedItem == null) return;

        // Simple drop: Put it 1 meter in front of the camera
        Vector3 dropPos = cameraTransform.position + cameraTransform.forward * 1.0f;
        Quaternion dropRot = transform.rotation;

        // true = use smooth transition
        DetachItem(dropPos, dropRot, true);
    }

    // Helper used by Equip and DetachItem
    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) 
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    public bool HasItem(ItemDefinition def)
    {
        if (currentEquippedItem == null) return false;
        return currentEquippedItem.itemDef == def;
    }

    public void SetPhysicsHolding(bool isHolding) => isPhysicsHolding = isHolding;

    public void RefreshPrompts()
    {
        // If we have no tool, or we are still supposedly holding physics, don't show tool prompts.
        if (currentEquippedItem == null || isPhysicsHolding) return;

        ActionPromptManager.Instance.ShowPrompt("EquipThrow", "Normal", "Throw Equipment", "Throw Tool");
        ActionPromptManager.Instance.ShowPrompt("EquipPlace", "Normal", "Put Down Equipment", "Place Tool");
        ActionPromptManager.Instance.ShowPrompt("EquipDrop", "Normal", "Drop Equipment", "Drop Tool");
    }

    public void ClearPrompts(bool fast)
    {
        ActionPromptManager.Instance.HidePrompt("EquipThrow", fast);
        ActionPromptManager.Instance.HidePrompt("EquipPlace", fast);
        ActionPromptManager.Instance.HidePrompt("EquipDrop", fast);
    }

    void HandleInput()
    {
        if (currentEquippedItem == null || isPhysicsHolding) return; 

        // 1. Put Down (Ghost Placement)
        if (controls.Normal.PutDownEquipment.WasPressedThisFrame())
        {
            isPlacingMode = true;
            CreateGhost();
        }
        
        if (controls.Normal.PutDownEquipment.WasReleasedThisFrame())
        {
            if (isPlacingMode) PlaceGently();
            isPlacingMode = false;
            DestroyGhost();
        }

        // 2. Drop (Simple release) - Uses the new animated Drop function
        if (controls.Normal.DropEquipment.triggered)
        {
            Drop();
        }

        // 3. Throw
        if (controls.Normal.ThrowEquipment.triggered && !isPlacingMode)
        {
            Throw();
        }
    }

    void HandleHandAnimation()
    {
        if (handMount == null) return;
        Vector3 targetPos = isPhysicsHolding ? loweredHandPos : defaultHandPos;
        
        if (!isPhysicsHolding)
        {
            // We use the pointer delta for sway
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            targetPos.x += -mouseDelta.x * swayAmount * 0.01f;
            targetPos.y += -mouseDelta.y * swayAmount * 0.01f;
        }

        handMount.localPosition = Vector3.Lerp(handMount.localPosition, targetPos, Time.deltaTime * swaySmooth);
    }

void CreateGhost()
    {
        if (currentEquippedItem == null) return;
        if (ghostObject != null) Destroy(ghostObject);
        ghostObject = Instantiate(currentEquippedItem.gameObject);
        
        // BUG FIX: Safely strip components in reverse order using DestroyImmediate 
        // This stops Unity from throwing "Component depends on it" errors.
        MonoBehaviour[] scripts = ghostObject.GetComponentsInChildren<MonoBehaviour>();
        for (int i = scripts.Length - 1; i >= 0; i--) DestroyImmediate(scripts[i]);

        Collider[] cols = ghostObject.GetComponentsInChildren<Collider>();
        for (int i = cols.Length - 1; i >= 0; i--) DestroyImmediate(cols[i]);

        Rigidbody[] rbs = ghostObject.GetComponentsInChildren<Rigidbody>();
        for (int i = rbs.Length - 1; i >= 0; i--) DestroyImmediate(rbs[i]);
        
        if (ghostMaterial != null)
        {
            foreach (var r in ghostObject.GetComponentsInChildren<Renderer>()) r.material = ghostMaterial;
        }
    }

void HandlePlacementPreview()
    {
        if (!isPlacingMode || ghostObject == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, placeDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            ghostObject.SetActive(true);
            
            float offset = currentEquippedItem != null ? currentEquippedItem.placementOffset : 0.1f;
            ghostObject.transform.position = hit.point + (hit.normal * offset);
            ghostObject.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up));
        }
        else ghostObject.SetActive(false);
    }

    void PlaceGently()
    {
        if (currentEquippedItem == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Vector3 pos = cameraTransform.position + cameraTransform.forward * 1.5f;
        Quaternion rot = Quaternion.identity;

        if (Physics.Raycast(ray, out RaycastHit hit, placeDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            float offset = currentEquippedItem.placementOffset;
            pos = hit.point + (hit.normal * offset); 
            rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up));
        }
        
        DetachItem(pos, rot, true);
    }

    void DestroyGhost() { if (ghostObject != null) Destroy(ghostObject); }

    void Throw()
    {
        if (currentEquippedItem == null) return;
        Rigidbody rb = currentEquippedItem.GetRigidbody();
        
        // false = INSTANT detach, no smooth transition. Physics take over immediately.
        DetachItem(cameraTransform.position + cameraTransform.forward * 0.5f, cameraTransform.rotation, false);
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
        }
    }

    // Refactored DetachItem to support animations and deferring physics restoration
    void DetachItem(Vector3 pos, Quaternion rot, bool animate = false)
    {
        if (currentEquippedItem == null) return;
        
        EquippableItem item = currentEquippedItem;
        int layerToRestore = itemOriginalLayer;
        currentEquippedItem = null;

        // UI Prompt Cleanup
        ActionPromptManager.Instance.HidePrompt("EquipThrow");
        ActionPromptManager.Instance.HidePrompt("EquipPlace");
        ActionPromptManager.Instance.HidePrompt("EquipDrop");

        if (animate)
        {
            // Transition smoothly to target spot in World Space. 
            // Physics and Collisions remain OFF until the callback is fired.
            item.StartTransition(null, pos, rot, false, () => {
                item.SetPhysics(true);
                SetLayerRecursive(item.gameObject, layerToRestore);
            });
        }
        else
        {
            // Instant drop (Used for throwing where Rigidbody handles the arc)
            item.transform.SetParent(null);
            item.transform.position = pos;
            item.transform.rotation = rot;
            item.SetPhysics(true);
            SetLayerRecursive(item.gameObject, layerToRestore);
        }
    }
    public EquippableItem GetEquippedItem() => currentEquippedItem;

    public void PlaceItemExact(Vector3 pos, Quaternion rot, bool animate)
    {
        DetachItem(pos, rot, animate);
    }

    public void ReturnToHand()
    {
        if (currentEquippedItem != null)
        {
            currentEquippedItem.StartTransition(handMount, currentEquippedItem.handPositionOffset, Quaternion.Euler(currentEquippedItem.handRotationOffset), true, () => { });
        }
    }
}