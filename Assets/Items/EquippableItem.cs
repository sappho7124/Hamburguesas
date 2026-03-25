using UnityEngine;

public class EquippableItem : MonoBehaviour // <--- CHANGED: No longer inherits InteractableObject
{
    [Header("Equipment Data")]
    public ItemDefinition itemDef;
    
    [Header("Hand Positioning")]
    public Vector3 handPositionOffset = new Vector3(0.5f, -0.4f, 0.8f);
    public Vector3 handRotationOffset = new Vector3(0, 90, 0);

    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void Start()
    {
        // Auto-configure the UI prompt
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null && string.IsNullOrEmpty(highlight.interactionVerb))
        {
            highlight.interactionVerb = "Equip";
        }
    }

    public void SetPhysics(bool enabled)
    {
        if (rb) rb.isKinematic = !enabled;
        if (col) col.enabled = enabled;
        
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null)
        {
            if (!enabled)
            {
                // EQUIPPING
                highlight.ToggleHighlight(false);
                
                // Force outline off immediately
                Outline o = highlight.OutlineComponent;
                if (o != null)
                {
                    o.OutlineWidth = 0f;
                    o.OutlineColor = Color.clear;
                }

                highlight.enabled = false; 
            }
            else
            {
                // DROPPING
                highlight.enabled = true;
            }
        }
    }

    public Rigidbody GetRigidbody() => rb;
}