using UnityEngine;
using System.Collections.Generic;

// Inherits from EquippableItem!
public class PlateItem : EquippableItem
{
    [Header("Plate Physics")]
    [Tooltip("How hard the plate needs to hit something to send food flying.")]
    public float shatterVelocityThreshold = 2.0f;
    [Tooltip("How much force is applied to scatter the food on impact.")]
    public float scatterForce = 3.0f;

    [Header("Dirty State Settings")]
    public bool isDirty = false;
    public Color dirtyColor = new Color(0.4f, 0.25f, 0.1f);

    private bool isHoldingItems = false;
    private List<Rigidbody> attachedItems = new List<Rigidbody>();

    private struct RendererData { public Renderer renderer; public Color originalColor; }
    private List<RendererData> renderers = new List<RendererData>();

    protected override void Awake()
    {
        base.Awake(); // Grabs rb and col from the base class

        // Cache original colors for when it gets washed later
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            renderers.Add(new RendererData { renderer = r, originalColor = r.material.color });
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.IsChildOf(transform)) return;

        Rigidbody targetRb = other.attachedRigidbody;
        if (targetRb != null && !attachedItems.Contains(targetRb) && targetRb.GetComponent<GrabbableItem>())
        {
            attachedItems.Add(targetRb);
        }
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody targetRb = other.attachedRigidbody;
        if (targetRb != null && attachedItems.Contains(targetRb))
        {
            attachedItems.Remove(targetRb);
        }
    }

    protected virtual void Update()
    {
        if (rb == null) return; 

        // Uses rb directly from EquippableItem
        bool isCurrentlyEquipped = rb.isKinematic;

        if (isCurrentlyEquipped && !isHoldingItems)
        {
            LockItemsToPlate();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (rb != null && !rb.isKinematic && isHoldingItems)
        {
            UnlockItemsFromPlate(collision.relativeVelocity);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (rb != null && !rb.isKinematic && isHoldingItems)
        {
            UnlockItemsFromPlate(Vector3.zero);
        }
    }

    public void MakeDirty()
    {
        isDirty = true;
        foreach (var data in renderers)
        {
            data.renderer.material.color = dirtyColor;
        }
    }

    public void CleanPlate()
    {
        isDirty = false;
        foreach (var data in renderers)
        {
            data.renderer.material.color = data.originalColor;
        }
    }

    public List<Rigidbody> GetAttachedItems() => attachedItems;

    private void LockItemsToPlate()
    {
        isHoldingItems = true;
        attachedItems.RemoveAll(itemRb => itemRb == null);

        foreach (var itemRb in attachedItems)
        {
            if (itemRb == rb) continue; 

            itemRb.isKinematic = true;
            itemRb.transform.SetParent(transform, true);

            Collider[] cols = itemRb.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;
        }
    }

    private void UnlockItemsFromPlate(Vector3 impactVelocity)
    {
        isHoldingItems = false;
        attachedItems.RemoveAll(itemRb => itemRb == null);

        foreach (var itemRb in attachedItems)
        {
            itemRb.transform.SetParent(null, true);
            itemRb.isKinematic = false;

            Collider[] cols = itemRb.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = true;

            if (impactVelocity.magnitude >= shatterVelocityThreshold)
            {
                itemRb.linearVelocity = impactVelocity * 0.5f; 
                Vector3 randomPop = new Vector3(
                    Random.Range(-1f, 1f), 
                    Random.Range(0.5f, 1.5f), 
                    Random.Range(-1f, 1f)
                ).normalized;
                
                itemRb.AddForce(randomPop * scatterForce, ForceMode.Impulse);
            }
        }
        
        attachedItems.Clear();
    }
}