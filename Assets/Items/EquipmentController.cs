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
    public float swaySmooth = 8f;
    
    [Header("Throwing/Placing")]
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

// --- Inside EquipmentController.cs ---

public void Equip(EquippableItem item)
{
    // 1. If we are already holding something, drop it first
    if (currentEquippedItem != null) 
    {
        Drop(); 
    }

    currentEquippedItem = item;
    
    // 2. Capture Original Layer 
    // This is crucial so we can restore it when dropping, allowing it to be highlighted again.
    itemOriginalLayer = item.gameObject.layer;

    // 3. Parent & Position to the Hand
    item.transform.SetParent(handMount);
    item.transform.localPosition = item.handPositionOffset;
    item.transform.localRotation = Quaternion.Euler(item.handRotationOffset);
    
    // 4. Disable Physics & Visuals for "Hand Mode"
    // This script calls rb.isKinematic = true and col.enabled = false internally
    item.SetPhysics(false);
    
    // 5. Change Layer to Ignore Raycast
    // This prevents the player's own interaction raycast from hitting the tool in their hand
    SetLayerRecursive(item.gameObject, LayerMask.NameToLayer("Ignore Raycast"));

    // 6. UI PROMPTS
    // We only show these if we aren't currently busy holding a physics object (like a box)
    if (!isPhysicsHolding)
    {
        RefreshPrompts();
    }
}

// --- Add to EquipmentController.cs ---

public void Drop()
{
    if (currentEquippedItem == null) return;

    // Simple drop: Put it 1 meter in front of the camera
    Vector3 dropPos = cameraTransform.position + cameraTransform.forward * 1.0f;
    Quaternion dropRot = transform.rotation;

    // This handles the unparenting, physics restore, and UI clearing
    DetachItem(dropPos, dropRot);
}

// Helper used by Equip
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

        // Debug.Log("Refreshing Equipment Prompts...");

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

        // 2. Drop (Simple release)
        if (controls.Normal.DropEquipment.triggered)
        {
            DetachItem(currentEquippedItem.transform.position, currentEquippedItem.transform.rotation);
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
        
        foreach (var comp in ghostObject.GetComponents<Component>())
        {
            if (!(comp is Transform || comp is Renderer || comp is MeshFilter)) Destroy(comp);
        }
        
        if (ghostMaterial != null)
        {
            foreach (var r in ghostObject.GetComponentsInChildren<Renderer>()) r.material = ghostMaterial;
        }
    }

    void HandlePlacementPreview()
    {
        if (!isPlacingMode || ghostObject == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, placeDistance))
        {
            ghostObject.SetActive(true);
            ghostObject.transform.position = hit.point;
            ghostObject.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up));
        }
        else ghostObject.SetActive(false);
    }

    void DestroyGhost() { if (ghostObject != null) Destroy(ghostObject); }

    void PlaceGently()
    {
        if (currentEquippedItem == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Vector3 pos = cameraTransform.position + cameraTransform.forward * 1.5f;
        Quaternion rot = Quaternion.identity;

        if (Physics.Raycast(ray, out RaycastHit hit, placeDistance))
        {
            pos = hit.point + (hit.normal * 0.1f); 
            rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up));
        }
        DetachItem(pos, rot);
    }

    void Throw()
    {
        if (currentEquippedItem == null) return;
        Rigidbody rb = currentEquippedItem.GetRigidbody();
        DetachItem(cameraTransform.position + cameraTransform.forward * 0.5f, cameraTransform.rotation);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
        }
    }

    void DetachItem(Vector3 pos, Quaternion rot)
    {
        if (currentEquippedItem == null) return;
        currentEquippedItem.transform.SetParent(null);
        currentEquippedItem.transform.position = pos;
        currentEquippedItem.transform.rotation = rot;
        currentEquippedItem.SetPhysics(true);
        SetLayerRecursive(currentEquippedItem.gameObject, itemOriginalLayer);
        currentEquippedItem = null;

        //UI Prompt
        ActionPromptManager.Instance.HidePrompt("EquipThrow");
        ActionPromptManager.Instance.HidePrompt("EquipPlace");
        ActionPromptManager.Instance.HidePrompt("EquipDrop");
    }
}