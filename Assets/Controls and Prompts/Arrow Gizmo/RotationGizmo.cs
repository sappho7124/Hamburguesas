using UnityEngine;

public class RotationGizmo : MonoBehaviour
{
    [Header("References")]
    public Transform xRing; 
    public Transform yRing; 
    public Animator gizmoAnimator; 

    [Header("Appearance")]
    public Material activeMatX;  // Red
    public Material activeMatY;  // Green
    public Material inactiveMat; // Gray
    
    [Header("Animation Parameters")]
    [Tooltip("The FLOAT parameter name in your Animator for X Speed")]
    public string xSpeedParam = "SpeedX"; 
    [Tooltip("The FLOAT parameter name in your Animator for Y Speed")]
    public string ySpeedParam = "SpeedY";
    [Tooltip("How fast the rotation slows down to a halt (Higher = Faster stop)")]
    public float stopSmoothing = 2f;

    [Header("Size & Scale Settings")]
    public float padding = 1.2f; 
    public float minSize = 0.5f; 
    public float animSpeedIn = 15f;  
    public float animSpeedOut = 15f; 

    [Header("Rotation Settings")]
    public Vector3 rotationOffset = Vector3.zero;

    private Transform targetObject;
    private float targetScale = 0f;
    
    // Internal Animation Speed Trackers
    private float currentSpeedX = 0f;
    private float targetSpeedX = 0f;
    private float currentSpeedY = 0f;
    private float targetSpeedY = 0f;

    private InteractionController controller;
    public bool isActiveState = false; 

    void Awake()
    {
        transform.localScale = Vector3.zero;
        if (gizmoAnimator == null) gizmoAnimator = GetComponent<Animator>();
    }

    void Start()
    {
        controller = FindAnyObjectByType<InteractionController>();
        
        if (controller != null)
        {
            controller.OnRotationModeChanged += HandleRotationModeChanged;
            controller.OnAxisLockChanged += HandleAxisLockChanged;
        }
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnRotationModeChanged -= HandleRotationModeChanged;
            controller.OnAxisLockChanged -= HandleAxisLockChanged;
        }
    }

    // --- EVENT HANDLERS ---

    private void HandleRotationModeChanged(bool isActive, Transform target)
    {
        if (isActive && target != null)
        {
            Activate(target);
        }
        else
        {
            Hide();
        }
    }

    private void HandleAxisLockChanged(bool lockX, bool lockY)
    {
        UpdateLocks(lockX, lockY);
    }

    // --- LOGIC ---

    void LateUpdate()
    {
        // 1. ANIMATOR SPEED SMOOTHING (Run this even if hiding for smooth stop)
        if (gizmoAnimator != null)
        {
            // Lerp current speed towards target speed
            currentSpeedX = Mathf.Lerp(currentSpeedX, targetSpeedX, Time.deltaTime * stopSmoothing);
            currentSpeedY = Mathf.Lerp(currentSpeedY, targetSpeedY, Time.deltaTime * stopSmoothing);

            // Send to Animator
            gizmoAnimator.SetFloat(xSpeedParam, currentSpeedX);
            gizmoAnimator.SetFloat(ySpeedParam, currentSpeedY);
        }

        if (targetObject == null)
        {
            if (transform.localScale.x > 0.01f)
            {
                transform.localScale = Vector3.zero;
            }
            return;
        }

        // 2. POSITION
        transform.position = targetObject.position;

        // 3. ROTATION
        if (controller != null && controller.playerCamera != null)
        {
            Vector3 camForward = controller.playerCamera.transform.forward;
            camForward.y = 0; 
            
            if (camForward.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(camForward);
                transform.rotation = lookRot * Quaternion.Euler(rotationOffset);
            }
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }

        // 4. SCALE ANIMATION
        float currentScale = transform.localScale.x;
        float speed = (targetScale > currentScale) ? animSpeedIn : animSpeedOut;

        if (Mathf.Abs(currentScale - targetScale) > 0.001f)
        {
            float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * speed);
            transform.localScale = Vector3.one * newScale;
        }
        else
        {
            transform.localScale = Vector3.one * targetScale;
        }
    }

    private void Activate(Transform target)
    {
        targetObject = target;
        isActiveState = true;

        // Calculate Scale
        float objectSize = 1f;
        MeshFilter mf = target.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 meshSize = mf.sharedMesh.bounds.size;
            Vector3 scaledSize = Vector3.Scale(meshSize, target.lossyScale);
            objectSize = Mathf.Max(scaledSize.x, Mathf.Max(scaledSize.y, scaledSize.z));
        }
        else
        {
            objectSize = Mathf.Max(target.lossyScale.x, Mathf.Max(target.lossyScale.y, target.lossyScale.z));
        }

        objectSize = Mathf.Max(objectSize, minSize);
        targetScale = objectSize * padding;
        
        // Default to both active
        UpdateLocks(false, false);
    }

    private void Hide()
    {
        isActiveState = false;
        targetScale = 0f;
        
        // Slow down to a stop when hiding
        targetSpeedX = 0f;
        targetSpeedY = 0f;
    }

    private void UpdateLocks(bool lockX, bool lockY)
    {
        // LOGIC: If X is Locked, X is DIM/STOPPED, Y is BRIGHT/MOVING.
        bool isXActive = !lockY;
        bool isYActive = !lockX;

        // 1. UPDATE MATERIALS
        if (xRing != null && yRing != null)
        {
            Material targetMatX = isXActive ? activeMatX : inactiveMat;
            Material targetMatY = isYActive ? activeMatY : inactiveMat;

            SetMaterialRecursive(xRing, targetMatX);
            SetMaterialRecursive(yRing, targetMatY);
        }

        // 2. UPDATE ANIMATOR TARGETS (Float 0 to 1)
        targetSpeedX = isXActive ? 1f : 0f;
        targetSpeedY = isYActive ? 1f : 0f;
    }

    void SetMaterialRecursive(Transform root, Material m)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r.sharedMaterial != m)
            {
                r.sharedMaterial = m;
            }
        }
    }
}