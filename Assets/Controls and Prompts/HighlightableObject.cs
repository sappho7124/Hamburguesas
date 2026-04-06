using UnityEngine;

public class HighlightableObject : MonoBehaviour
{
    [Header("UI Text")]
    public string objectName = ""; 
    public string interactionVerb = "";

    [Header("UI Positioning")]
    public Transform uiAnchor; 
    public Vector3 uiWorldOffset = Vector3.zero;[Header("UI Animation Settings")]
    public float slideInDistance = 0.5f;[Tooltip("The GAP between the detected visual edge and the UI.")]
    public float slideOutDistance = 0.2f;
    public float thicknessMultiplier = 1.2f;

    [Header("Visual Settings")][Tooltip("Leave empty to highlight this object. Assign a child object (like a handle) to outline that instead.")]
    public GameObject outlineTarget; 
    public Color hoverColor = Color.white; 
    public float maxOutlineWidth = 10f; 
    public float animSpeed = 15f; 

    private Outline outline; 
    private float targetWidth = 0f;
    private Color targetColor; 

    public Outline OutlineComponent => outline; 
    public bool IsFadingOut => targetWidth == 0;

    void Awake()
    {
        GameObject targetObj = outlineTarget != null ? outlineTarget : gameObject;
        outline = targetObj.GetComponent<Outline>();
        if (outline != null) Destroy(outline);
        
        targetWidth = 0f;
        targetColor = Color.clear;
    }

    void Update()
    {
        if (outline == null) return;

        // Smoothly lerp width
        if (Mathf.Abs(outline.OutlineWidth - targetWidth) > 0.01f)
            outline.OutlineWidth = Mathf.Lerp(outline.OutlineWidth, targetWidth, Time.deltaTime * animSpeed);
        else
            outline.OutlineWidth = targetWidth;

        // Apply Color
        outline.OutlineColor = targetColor;

        // Final Cleanup
        if (targetWidth == 0 && outline.OutlineWidth < 0.1f)
        {
            Destroy(outline);
            outline = null;
        }
    }

    public void SetTempColor(Color c) { if (outline != null) targetColor = c; }
    
    public void ResetColor() { targetColor = hoverColor; }

    public void ToggleHighlight(bool active)
    {
        GameObject targetObj = outlineTarget != null ? outlineTarget : gameObject;

        if (active)
        {
            if (outline == null)
            {
                outline = targetObj.AddComponent<Outline>();
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineWidth = 0f; 
                outline.OutlineColor = hoverColor;
                outline.SetRenderQueue(3100);
            }
            targetWidth = maxOutlineWidth;
            targetColor = hoverColor;
        }
        else
        {
            targetWidth = 0f;
        }
    }

    public Vector3 GetUIPosition()
    {
        Vector3 basePos = (uiAnchor != null) ? uiAnchor.position : transform.position;
        return basePos + uiWorldOffset;
    }
}