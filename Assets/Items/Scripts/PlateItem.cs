using UnityEngine;
using System.Collections.Generic;//[RequireComponent(typeof(EquippableItem))] It Does need Equipable item to function but we are removing it to now show the warnings on ghost placement
public class PlateItem : MonoBehaviour
{
    private EquippableItem equippable;
    private bool wasEquipped = false;
    private List<Rigidbody> attachedItems = new List<Rigidbody>();

    void Awake()
    {
        equippable = GetComponent<EquippableItem>();
    }

    void OnTriggerEnter(Collider other)
    {
        // Don't detect the plate's own solid colliders
        if (other.transform.IsChildOf(transform)) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && !attachedItems.Contains(rb) && rb.GetComponent<GrabbableItem>())
        {
            attachedItems.Add(rb);
        }
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && attachedItems.Contains(rb))
        {
            attachedItems.Remove(rb);
        }
    }

    void Update()
    {
        // If the plate is kinematic, it means the player is holding it.
        bool isCurrentlyEquipped = equippable.GetRigidbody().isKinematic;

        if (isCurrentlyEquipped && !wasEquipped)
        {
            LockItemsToPlate();
            wasEquipped = true;
        }
        else if (!isCurrentlyEquipped && wasEquipped)
        {
            UnlockItemsFromPlate();
            wasEquipped = false;
        }
    }

    private void LockItemsToPlate()
    {
        attachedItems.RemoveAll(rb => rb == null);

        foreach (var rb in attachedItems)
        {
            // Make them static and attach them to the plate
            rb.isKinematic = true;
            rb.transform.SetParent(transform, true);

            // Turn off their colliders so they don't block raycasts or hit walls while walking
            Collider[] cols = rb.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;
        }
    }

    private void UnlockItemsFromPlate()
    {
        attachedItems.RemoveAll(rb => rb == null);

        foreach (var rb in attachedItems)
        {
            // Drop them from the plate and restore physics
            rb.transform.SetParent(null, true);
            rb.isKinematic = false;

            Collider[] cols = rb.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = true;
        }
    }
}