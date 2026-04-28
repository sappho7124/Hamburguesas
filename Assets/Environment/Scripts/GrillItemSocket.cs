using UnityEngine;
using System.Collections;

[RequireComponent(typeof(HighlightableObject), typeof(Collider))]
public class GrillItemSocket : MonoBehaviour
{
    [Header("Socket Settings")]
    [Tooltip("Transform where the item will be snapped and held.")]
    public Transform snapPoint;
    [Tooltip("The specific item required to trigger this socket (e.g., FryerBasket).")]
    public ItemDefinition requiredItem;

    [Header("Placement Animation")]
    [Tooltip("How high above the snap point the basket hovers before dropping.")]
    public float dropHeight = 0.3f;
    [Tooltip("How long it takes to descend into the oil.")]
    public float dropDuration = 0.25f;

    [Header("Hologram Settings")]
    [Tooltip("Material used for the dynamic ghost hologram.")]
    public Material ghostMaterial;

    private InteractionController interactController;
    private EquipmentController equipmentController;
    private HighlightableObject highlight;
    private GameObject currentGhost;
    private string originalVerb;
    
    private bool isOccupied = false;
    private EquippableItem placedItem; 
    
    private float pickupCooldown = 0f; // NEW: Replaces requiresLookAway with a hard timer
    private GrillAppliance parentGrill; 

    private float debugLogTimer = 0f;

    void Start()
    {
        interactController = FindAnyObjectByType<InteractionController>();
        equipmentController = FindAnyObjectByType<EquipmentController>();
        highlight = GetComponent<HighlightableObject>();
        
        // Find the grill this socket belongs to
        parentGrill = GetComponentInParent<GrillAppliance>();
        if (parentGrill == null) 
        {
            Debug.LogError($"<color=red>[GrillItemSocket] CRITICAL: '{gameObject.name}' could not find a GrillAppliance component on its parent object! Heat cannot transfer.</color>");
        }

        if (highlight != null) originalVerb = highlight.interactionVerb;
        
        if (interactController != null)
            interactController.OnInteractableHover += HandleHover;
    }

    private void OnDestroy()
    {
        if (interactController != null)
            interactController.OnInteractableHover -= HandleHover;
        DestroyGhost();
    }

    void Update()
    {
        if (currentGhost != null && snapPoint != null)
            currentGhost.transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);

        // Cooldown timer to prevent hologram from instantly flashing back on pickup
        if (pickupCooldown > 0f) pickupCooldown -= Time.deltaTime;

        if (isOccupied && placedItem != null)
        {
            // Use GetComponentInChildren just in case VesselBase is on a child object
            VesselBase vessel = placedItem.GetComponentInChildren<VesselBase>();
            
            if (parentGrill != null && vessel != null)
            {
                // --- 1. MANUALLY ENFORCE TEMPERATURE ---
                float temp = parentGrill.isOn ? parentGrill.grillTemperature : parentGrill.ambientTemperature;
                float mult = parentGrill.isOn ? parentGrill.heatTransferMultiplier : 1f;
                vessel.SetEnvironmentState(temp, mult);

                // --- ONGOING DEBUG LOGGING (Fires once per second) ---
                debugLogTimer += Time.deltaTime;
                if (debugLogTimer >= 1f)
                {
                    debugLogTimer = 0f; 
                    Debug.Log($"<color=cyan>[GrillItemSocket] Heating... Grill is {(parentGrill.isOn ? "ON" : "OFF")} | Temp: {temp}° | Fries in basket: {vessel.spawnedItems.Count}</color>");
                    
                    if (vessel.spawnedItems.Count > 0 && vessel.spawnedItems[0] != null)
                    {
                        CookableItem fry = vessel.spawnedItems[0].GetComponent<CookableItem>();
                        if (fry != null)
                        {
                            Debug.Log($"<color=yellow>   -> First Fry Status: Temp: {fry.currentTemperature:F1} | Cook Progress: {fry.currentHeatProgress:F1}</color>");
                        }
                    }
                }
            }

            // --- 2. DETECT NATIVE PICKUP ---
            if (placedItem.transform.parent != snapPoint)
            {
                if (vessel != null) vessel.SetEnvironmentState(20f, 1f);
                isOccupied = false;
                placedItem = null;
                pickupCooldown = 1.5f; // LOCK HOLOGRAM FOR 1.5 SECONDS
                ResetState();
            }
        }
    }

    private void HandleHover(bool active, HighlightableObject obj, string map, string action, bool isBlocked)
    {
        // If we look away, clear the ghost
        if (!active || (obj != null && obj.gameObject != this.gameObject))
        {
            ResetState();
            return;
        }

        // If we just picked up the basket, ignore hover for 1.5 seconds so ghost doesn't spawn
        if (pickupCooldown > 0f) return; 

        bool hasRequiredItem = equipmentController != null && 
                               equipmentController.GetEquippedItem() != null && 
                               equipmentController.HasItem(requiredItem);

        if (!isBlocked && !isOccupied && hasRequiredItem)
        {
            if (highlight != null) highlight.interactionVerb = $"Place {requiredItem.itemName}";
            if (currentGhost == null)
                CreateGhost(equipmentController.GetEquippedItem().gameObject);
        }
        else
        {
            ResetState();
        }
    }

    private void ResetState()
    {
        if (highlight != null) highlight.interactionVerb = originalVerb;
        DestroyGhost();
    }

    private void CreateGhost(GameObject source)
    {
        DestroyGhost();
        if (source == null || snapPoint == null) return;

        currentGhost = Instantiate(source, snapPoint.position, snapPoint.rotation);
        currentGhost.name = "Ghost_" + source.name;
        currentGhost.SetActive(true);

        foreach (var mb in currentGhost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (var col in currentGhost.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
        foreach (var rb in currentGhost.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);

        if (ghostMaterial != null)
            foreach (var r in currentGhost.GetComponentsInChildren<Renderer>()) r.material = ghostMaterial;
    }

    private void DestroyGhost()
    {
        if (currentGhost != null) 
        { 
            Destroy(currentGhost); 
            currentGhost = null; 
        }
    }

    public void HandleInteraction()
    {
        if (!isOccupied && pickupCooldown <= 0f)
            SnapAndPlaceItem();
    }

    private void SnapAndPlaceItem()
    {
        if (equipmentController == null || isOccupied || !equipmentController.HasItem(requiredItem)) return;

        EquippableItem equipped = equipmentController.GetEquippedItem();
        if (equipped == null) return;

        // Animate detach from hand to a hovering point ABOVE the snap point
        Vector3 hoverPos = snapPoint.position + (Vector3.up * dropHeight);
        equipmentController.PlaceItemExact(hoverPos, snapPoint.rotation, true);
        
        placedItem = equipped;
        isOccupied = true;
        DestroyGhost();

        StartCoroutine(LockItemToSocket(equipped));
    }

    private IEnumerator LockItemToSocket(EquippableItem item)
    {
        yield return new WaitForSeconds(0.3f); 
        if (item == null) yield break;

        item.transform.SetParent(snapPoint, true);

        Vector3 startLocalPos = item.transform.localPosition;
        Vector3 targetLocalPos = Vector3.zero;
        Quaternion startLocalRot = item.transform.localRotation;
        Quaternion targetLocalRot = Quaternion.identity;

        float elapsed = 0f;
        
        // Slowly descend into the oil
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            item.transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, t);
            item.transform.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, t);
            yield return null;
        }

        item.transform.localPosition = targetLocalPos;
        item.transform.localRotation = targetLocalRot;

        Rigidbody rb = item.GetRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
        }

        // --- PLACEMENT DEBUG LOG ---
        VesselBase placedVessel = item.GetComponentInChildren<VesselBase>();
        if (placedVessel != null) {
            Debug.Log($"<color=orange>[GrillItemSocket] Basket Placed! It contains {placedVessel.spawnedItems.Count} items.</color>");
        } else {
            Debug.LogError("<color=red>[GrillItemSocket] Basket placed, but NO VesselBase component found on it! Fries cannot be detected.</color>");
        }
    }
}