using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(InteractableObject))]
[RequireComponent(typeof(VesselBase))]
public class PourActionBase : MonoBehaviour, IInteractionValidator
{
    [Header("Core Transfer Settings")]
    public List<ItemDefinition> allowedSourceItems = new List<ItemDefinition>();
    [Tooltip("Seconds between each item falling into the basket")]
    public float pourSpeed = 0.1f; 

    [Header("Alignment Settings")]
    [Tooltip("If true, the offset is relative to the player's camera. If false, it's relative to the object's local axes.")]
    public bool alignToView = true;
    [Tooltip("X = Right/Left, Y = Up/Down, Z = Forward/Backward (Closer to camera)")]
    public Vector3 hoverOffset = new Vector3(0, 0.4f, 0);
    [Tooltip("Set Y to -90 or 90 to make it pour sideways!")]
    public Vector3 pourRotationOffset = Vector3.zero;

    [Header("Tilt Mechanics")]
    [Tooltip("The angle it tilts when the bag is completely FULL")]
    public float startingPourAngle = 45f;
    [Tooltip("The angle it tilts when trying to get the very last fry out of an EMPTY bag")]
    public float endingPourAngle = 180f; // 180 = upside down

    [Header("Shake Animation")]
    public bool enableShake = true;
    public float shakeSpeed = 40f;
    public float baseShakeAngle = 4f;
    [Tooltip("If true, the bag shakes more violently as it runs out of items.")]
    public bool increaseShakeWhenEmpty = true;
    public float maxEmptyShakeMultiplier = 3f;

    [Header("Target Condition")]
    public Vector3 targetUpDirection = Vector3.up;
    public float uprightTolerance = 0.7f;
    public float maxDistanceBeforeCancel = 2.5f;

    protected bool isPouring = false;
    protected VesselBase targetVessel;

    private InteractionController interactController;
    private VesselBase currentlyHoveredSource;
    private Player_Controls controls;
    private InputAction currentMappedAction;

    protected virtual void Awake()
    {
        targetVessel = GetComponent<VesselBase>();
        controls = new Player_Controls();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    protected virtual void Start()
    {
        interactController = FindAnyObjectByType<InteractionController>();
        if (interactController != null) interactController.OnInteractableHover += HandleHover;
    }

    protected virtual void OnDestroy()
    {
        if (interactController != null) interactController.OnInteractableHover -= HandleHover;
    }

    public bool IsInteractionValid(EquipmentController eq, out string failReason)
    {
        failReason = "";
        if (!IsTargetUpright()) { failReason = "Container is tilted!"; return false; }
        if (targetVessel.currentAmount >= targetVessel.maxCapacity) { failReason = "Container is full!"; return false; }

        if (eq != null && eq.GetEquippedItem() != null)
        {
            VesselBase sourceVessel = eq.GetEquippedItem().GetComponent<VesselBase>();
            if (sourceVessel != null)
            {
                if (sourceVessel.currentAmount <= 0) { failReason = "Source is empty!"; return false; }
                if (sourceVessel.itemPrefabToSpawn != targetVessel.itemPrefabToSpawn) { failReason = "Item types do not match!"; return false; }
            }
        }
        return true; 
    }

    private bool IsTargetUpright() => Vector3.Dot(transform.up, targetUpDirection.normalized) >= uprightTolerance;

    private void HandleHover(bool active, HighlightableObject obj, string map, string action, bool isBlocked)
    {
        EquipmentController eq = FindAnyObjectByType<EquipmentController>();
        if (eq == null || isPouring) return;

        bool validHover = active && obj.gameObject == this.gameObject && !isBlocked && IsTargetUpright();

        if (!validHover)
        {
            if (currentlyHoveredSource != null) { currentlyHoveredSource.SetOpenState(false); currentlyHoveredSource = null; }
            currentMappedAction = null;
            return;
        }

        if (eq.GetEquippedItem() != null && allowedSourceItems.Contains(eq.GetEquippedItem().itemDef))
        {
            VesselBase sourceVessel = eq.GetEquippedItem().GetComponent<VesselBase>();
            if (sourceVessel != null && sourceVessel.itemPrefabToSpawn == targetVessel.itemPrefabToSpawn) 
            { 
                currentlyHoveredSource = sourceVessel; 
                currentlyHoveredSource.SetOpenState(true);

                InteractableObject io = GetComponent<InteractableObject>();
                InteractionMapping mapping = io.GetValidMapping(eq.GetEquippedItem().itemDef);
                if (mapping != null)
                {
                    string actionName = string.IsNullOrEmpty(mapping.inputActionName) ? "Interact" : mapping.inputActionName;
                    currentMappedAction = controls.asset.FindAction($"Normal/{actionName}");
                }
            }
        }
    }

    public void PerformPour()
    {
        if (isPouring || !IsTargetUpright() || currentMappedAction == null) return;

        EquipmentController eq = FindAnyObjectByType<EquipmentController>();
        if (eq != null && eq.GetEquippedItem() != null && allowedSourceItems.Contains(eq.GetEquippedItem().itemDef))
        {
            EquippableItem sourceEq = eq.GetEquippedItem();
            VesselBase sourceVessel = sourceEq.GetComponent<VesselBase>();
            
            if (IsInteractionValid(eq, out _)) 
            {
                StartCoroutine(CinematicPourRoutine(sourceEq, sourceVessel, eq));
            }
        }
    }

    private IEnumerator CinematicPourRoutine(EquippableItem sourceEq, VesselBase sourceVessel, EquipmentController eq)
    {
        isPouring = true;
        Transform playerCam = Camera.main.transform;

        sourceEq.StopTransition();
        sourceEq.transform.SetParent(null);

        bool isAutoDump = !currentMappedAction.IsPressed(); 

        Vector3 initialHandPos = sourceEq.transform.position;
        Quaternion initialHandRot = sourceEq.transform.rotation;

        float transitionTime = 0.2f; 
        float currentTransition = 0f;
        float fryDropCooldown = 0f;

        while (sourceVessel.currentAmount > 0 && targetVessel.currentAmount < targetVessel.maxCapacity)
        {
            if (Vector3.Distance(playerCam.position, targetVessel.transform.position) > maxDistanceBeforeCancel) break;
            if (!IsTargetUpright() || sourceEq != eq.GetEquippedItem()) break;
            if (!isAutoDump && !currentMappedAction.IsPressed()) break;
            if (isAutoDump && currentMappedAction.WasPressedThisFrame()) break;

            float emptiness = 1f - ((float)sourceVessel.currentAmount / sourceVessel.maxCapacity);
            float targetAngle = Mathf.Lerp(startingPourAngle, endingPourAngle, emptiness);

            Vector3 dynamicTargetPos;
            Quaternion baseViewRot;

            if (alignToView)
            {
                Vector3 dirToCam = playerCam.position - targetVessel.transform.position;
                dirToCam.y = 0;
                if (dirToCam.sqrMagnitude < 0.001f) dirToCam = -targetVessel.transform.forward;
                dirToCam.Normalize();

                Vector3 flatCamRight = playerCam.right;
                flatCamRight.y = 0;
                flatCamRight.Normalize();

                dynamicTargetPos = targetVessel.transform.position 
                                   + (Vector3.up * hoverOffset.y) 
                                   + (dirToCam * hoverOffset.z) 
                                   + (flatCamRight * hoverOffset.x);

                baseViewRot = Quaternion.LookRotation(-dirToCam);
            }
            else
            {
                dynamicTargetPos = targetVessel.transform.position + targetVessel.transform.TransformDirection(hoverOffset);
                Vector3 lookDir = playerCam.position - dynamicTargetPos;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude < 0.001f) lookDir = Vector3.forward;
                baseViewRot = Quaternion.LookRotation(-lookDir);
            }

            // CRITICAL MATH FIX: 
            // 1. Take the base facing direction (looking at camera/basket)
            // 2. Apply your static custom offset first (e.g. Turn sideways)
            // 3. Apply the pouring tilt AFTER, so it tips "forward" relative to its new orientation!
            Quaternion restingRot = baseViewRot * Quaternion.Euler(pourRotationOffset);
            Quaternion pourRot = restingRot * Quaternion.Euler(targetAngle, 0, 0);

            // --- SHAKE MECHANICS ---
            if (enableShake)
            {
                float currentShakeIntensity = baseShakeAngle;
                if (increaseShakeWhenEmpty) currentShakeIntensity *= Mathf.Lerp(1f, maxEmptyShakeMultiplier, emptiness);

                float shakePitch = Mathf.Sin(Time.time * shakeSpeed) * currentShakeIntensity;
                float shakeRoll = Mathf.Cos(Time.time * shakeSpeed * 1.3f) * (currentShakeIntensity * 0.5f);
                
                pourRot *= Quaternion.Euler(shakePitch, 0, shakeRoll);
                dynamicTargetPos += (Vector3.up * Mathf.Sin(Time.time * shakeSpeed * 1.5f) * currentShakeIntensity * 0.001f);
            }

            if (currentTransition < transitionTime)
            {
                currentTransition += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, currentTransition / transitionTime);
                sourceEq.transform.position = Vector3.Lerp(initialHandPos, dynamicTargetPos, t);
                sourceEq.transform.rotation = Quaternion.Slerp(initialHandRot, pourRot, t);
            }
            else
            {
                sourceEq.transform.position = dynamicTargetPos;
                sourceEq.transform.rotation = pourRot;
            }

            if (currentTransition >= transitionTime)
            {
                fryDropCooldown -= Time.deltaTime;
                if (fryDropCooldown <= 0f)
                {
                    var itemBatch = sourceVessel.TakeExactItems(1);
                    if (itemBatch.Count > 0) targetVessel.ReceiveExactItems(itemBatch, sourceVessel);
                    fryDropCooldown = pourSpeed; 
                }
            }

            yield return null;
        }

        if (sourceVessel != null) sourceVessel.SetOpenState(false);
        if (eq.GetEquippedItem() == sourceEq)
        {
            sourceEq.transform.SetParent(eq.handMount);
            eq.ReturnToHand(); 
        }
        
        yield return new WaitForSeconds(0.25f);
        isPouring = false;
    }
}