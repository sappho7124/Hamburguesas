using UnityEngine;

public class HighlightableObject : MonoBehaviour
{
    [Header("UI Text")]
    public string objectName = ""; 
    public string interactionVerb = "";

    [Header("UI Positioning")]
    public Transform uiAnchor; 
    public Vector3 uiWorldOffset = Vector3.zero;
    
    [Header("UI Animation Settings")]
    public float slideInDistance = 0.5f;
    [Tooltip("The GAP between the detected visual edge and the UI.")]
    public float slideOutDistance = 0.2f;
    public float thicknessMultiplier = 1.2f;

    [Header("Visual Settings")]
    [Tooltip("Leave empty to highlight this object. Assign a child object (like a handle) to outline that instead.")]
    public GameObject outlineTarget; 
    public Color hoverColor = Color.white; 
    public float maxOutlineWidth = 10f; 
    public float animSpeed = 15f; 

    [Header("Hand-Drawn Noise Settings")]
    [Tooltip("The standard amount of wobble when just looking at the object")]
    public float baseNoiseAmount = 7f;      // UPDATED DEFAULT
    public float baseNoiseScale = 250f;     // UPDATED DEFAULT
    public float baseFrameRate = 6f;        // UPDATED DEFAULT

    private Outline outline; 
    private float targetWidth = 0f;
    private Color targetColor; 
    private float targetNoiseAmount; 

    public Outline OutlineComponent => outline; 
    public bool IsFadingOut => targetWidth == 0;

    void Awake()
    {
        GameObject targetObj = outlineTarget != null ? outlineTarget : gameObject;
        outline = targetObj.GetComponent<Outline>();
        
        // Disable instead of destroying to prevent race conditions
        if (outline != null) 
        {
            outline.OutlineWidth = 0f;
            outline.NoiseAmount = 0f;
            outline.enabled = false;
        }
        
        targetWidth = 0f;
        targetColor = Color.clear;
        targetNoiseAmount = 0f;
    }

    void Update()
    {
        if (outline == null) return;

        // Smoothly lerp Width
        if (Mathf.Abs(outline.OutlineWidth - targetWidth) > 0.01f)
            outline.OutlineWidth = Mathf.Lerp(outline.OutlineWidth, targetWidth, Time.deltaTime * animSpeed);
        else
            outline.OutlineWidth = targetWidth;

        // Smoothly lerp Noise Amount (Fades out with the outline!)
        if (Mathf.Abs(outline.NoiseAmount - targetNoiseAmount) > 0.01f)
            outline.NoiseAmount = Mathf.Lerp(outline.NoiseAmount, targetNoiseAmount, Time.deltaTime * animSpeed);
        else
            outline.NoiseAmount = targetNoiseAmount;

        // Apply Color
        outline.OutlineColor = targetColor;

        // Final Cleanup: DISABLE instead of DESTROY
        if (targetWidth == 0 && outline.OutlineWidth < 0.1f)
        {
            outline.OutlineWidth = 0f;
            outline.NoiseAmount = 0f;
            if (outline.enabled) outline.enabled = false;
        }
    }

    // --- METHODS FOR EXTERNAL SCRIPTS ---
    public void SetDynamicNoise(float newNoiseAmount) { targetNoiseAmount = newNoiseAmount; }
    public void ResetDynamicNoise() { targetNoiseAmount = baseNoiseAmount; }
    // ------------------------------------

    public void SetTempColor(Color c) { if (outline != null) targetColor = c; }
    
    public void ResetColor() { targetColor = hoverColor; }

    public void ToggleHighlight(bool active)
    {
        GameObject targetObj = outlineTarget != null ? outlineTarget : gameObject;

        if (active)
        {
            if (outline == null)
            {
                if (!targetObj.TryGetComponent<Outline>(out outline))
                {
                    outline = targetObj.AddComponent<Outline>();
                }
            }

            if (outline != null)
            {
                outline.enabled = true; 
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineColor = hoverColor;
                outline.SetRenderQueue(3100);

                // Apply static drawing properties
                outline.NoiseScale = baseNoiseScale;
                outline.FrameRate = baseFrameRate;
            }
            
            targetWidth = maxOutlineWidth;
            targetColor = hoverColor;
            targetNoiseAmount = baseNoiseAmount; // Ramp noise UP when highlighted
        }
        else
        {
            targetWidth = 0f;
            targetNoiseAmount = 0f; // Ramp noise DOWN when fading out
        }
    }

    public Vector3 GetUIPosition()
    {
        Vector3 basePos = (uiAnchor != null) ? uiAnchor.position : transform.position;
        return basePos + uiWorldOffset;
    }
    public void RefreshOutlineRenderers()
    {
        if (outline != null)
        {
            bool wasEnabled = outline.enabled;
            
            // DestroyImmediate forces Unity to wipe it this exact millisecond
            DestroyImmediate(outline); 
            outline = null;
            
            // Re-apply it cleanly so it only highlights current children!
            if (wasEnabled) ToggleHighlight(true); 
        }
    }
}