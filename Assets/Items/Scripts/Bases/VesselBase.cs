using UnityEngine;
using System.Collections.Generic;

public class VesselBase : MonoBehaviour
{
    [Header("Core Capacity")]
    public int maxCapacity = 30; 
    public int currentAmount = 0;
    public bool startFull = false;

    [Header("Spawning & Physics")]
    public GameObject itemPrefabToSpawn;
    public Transform spawnVolume;
    public Vector3 volumeSize = new Vector3(0.2f, 0.2f, 0.2f);
    
    [Header("Funnel Settings")]
    public Transform pourEntryVolume;
    public Vector2 entryVolumeSize = new Vector2(0.2f, 0.2f); 

    [Header("Thermal Conduit")]
    public float currentEnvTemp = 20f;
    public float currentEnvMultiplier = 1f;

    [HideInInspector] public List<GameObject> spawnedItems = new List<GameObject>();

    protected virtual void Start()
    {
        if (startFull)
        {
            currentAmount = maxCapacity;
            ForceSpawnCurrentAmount();
        }
    }

    protected virtual void Update()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            GameObject item = spawnedItems[i];

            // 1. Safety check for completely destroyed items
            if (item == null)
            {
                spawnedItems.RemoveAt(i);
                currentAmount--;
                OnVesselAmountChanged();
                RefreshParentHighlight();
                continue;
            }

            // 2. CRITICAL FIX: Is it currently flying through the air from a pour?
            // If yes, DO NOT kick it out of the list! Let the animation finish!
            if (item.GetComponent<ItemTransferAnimator>() != null)
                continue; 

            // 3. If it has been physically pulled out of the spawn volume natively, release it
            if (item.transform.parent != spawnVolume)
            {
                SetItemPhysicsAndInteraction(item, true); 
                spawnedItems.RemoveAt(i);
                currentAmount--;
                OnVesselAmountChanged();
                RefreshParentHighlight();
            }
        }
    }

    public void SetEnvironmentState(float temp, float multiplier)
    {
        currentEnvTemp = temp;
        currentEnvMultiplier = multiplier;

        // Propagate the temperature to all items currently inside
        foreach (var item in spawnedItems)
        {
            if (item != null && item.TryGetComponent(out CookableItem cookable))
            {
                cookable.targetEnvironmentTemperature = temp;
                cookable.currentHeatMultiplier = multiplier;
            }
        }
    }

    private void RefreshParentHighlight()
    {
        if (TryGetComponent(out HighlightableObject hl)) hl.RefreshOutlineRenderers();
    }

    public virtual void SetOpenState(bool isOpen) { }
    protected virtual void OnVesselAmountChanged() { }

    protected virtual void CalculateItemPlacement(int itemIndex, out Vector3 localPos, out Quaternion localRot)
    {
        localPos = new Vector3(Random.Range(-volumeSize.x/2, volumeSize.x/2), Random.Range(-volumeSize.y/2, volumeSize.y/2), Random.Range(-volumeSize.z/2, volumeSize.z/2));
        localRot = Random.rotation;
    }

    private void SetLayerRecursive(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform) SetLayerRecursive(child.gameObject, newLayer);
    }

    private void LockItemState(GameObject item)
    {
        SetLayerRecursive(item, 2); // 2 = Ignore Raycast usually
        
        if (item.TryGetComponent(out HighlightableObject hl)) hl.enabled = false;
        if (item.TryGetComponent(out InteractableObject io)) io.enabled = false;
        if (item.TryGetComponent(out GrabbableItem gi)) gi.enabled = false; 

        Outline objOutline = item.GetComponent<Outline>();
        if (objOutline != null) Destroy(objOutline);

        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
            rb.useGravity = false; 
            rb.detectCollisions = false; // NO COLLISIONS AT ALL
        }

        Collider[] cols = item.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            c.enabled = false; // ENTIRELY DISABLED FOR PERFORMANCE
        }
    }

    private void SetItemPhysicsAndInteraction(GameObject item, bool active)
    {
        if (!active) { LockItemState(item); return; }

        SetLayerRecursive(item, 0); 
        if (item.TryGetComponent(out HighlightableObject hl)) hl.enabled = true;
        if (item.TryGetComponent(out InteractableObject io)) io.enabled = true;
        if (item.TryGetComponent(out GrabbableItem gi)) gi.enabled = true; 

        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false; 
            rb.useGravity = true; 
            rb.detectCollisions = true; 
        }

        Collider[] cols = item.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            c.enabled = true; 
            c.isTrigger = false; 
        }

        // Reset temperature to room temp when it leaves the vessel
        if (item.TryGetComponent(out CookableItem cookable))
        {
            cookable.targetEnvironmentTemperature = cookable.ambientTemperature;
            cookable.currentHeatMultiplier = 1f;
        }
    }

    private void ForceSpawnCurrentAmount()
    {
        for (int i = spawnedItems.Count; i < currentAmount; i++)
        {
            CalculateItemPlacement(i, out Vector3 targetPos, out Quaternion targetRot);
            GameObject newItem = Instantiate(itemPrefabToSpawn, spawnVolume.TransformPoint(targetPos), targetRot, spawnVolume);
            
            LockItemState(newItem); 
            
            // Sync initial temp
            if (newItem.TryGetComponent(out CookableItem cookable))
            {
                cookable.targetEnvironmentTemperature = currentEnvTemp;
                cookable.currentHeatMultiplier = currentEnvMultiplier;
            }

            spawnedItems.Add(newItem);
        }
        OnVesselAmountChanged();
        RefreshParentHighlight();
    }

    public List<GameObject> TakeExactItems(int amount)
    {
        List<GameObject> taken = new List<GameObject>();
        int toTake = Mathf.Min(amount, spawnedItems.Count);

        for (int i = 0; i < toTake; i++)
        {
            GameObject item = spawnedItems[spawnedItems.Count - 1];
            spawnedItems.RemoveAt(spawnedItems.Count - 1);
            currentAmount--;
            
            item.transform.SetParent(null); 
            // Do not restore physics yet, they are flying!
            taken.Add(item);
        }
        
        OnVesselAmountChanged();
        RefreshParentHighlight();
        return taken;
    }

    public void ReceiveExactItems(List<GameObject> items, VesselBase sourceVessel = null)
    {
        foreach (var item in items)
        {
            if (currentAmount >= maxCapacity) 
            {
                Destroy(item); 
                continue;
            }

            CalculateItemPlacement(currentAmount, out Vector3 targetLocalPos, out Quaternion targetLocalRot);
            
            LockItemState(item); 

            // Sync new temp!
            if (item.TryGetComponent(out CookableItem cookable))
            {
                cookable.targetEnvironmentTemperature = currentEnvTemp;
                cookable.currentHeatMultiplier = currentEnvMultiplier;
            }

            spawnedItems.Add(item);
            currentAmount++;

            if (sourceVessel != null)
            {
                item.transform.SetParent(null); 
                ItemTransferAnimator anim = item.AddComponent<ItemTransferAnimator>();
                anim.StartTransfer(sourceVessel.pourEntryVolume, sourceVessel.entryVolumeSize, this.pourEntryVolume, this.entryVolumeSize, this.spawnVolume, targetLocalPos, targetLocalRot);
            }
            else
            {
                item.transform.SetParent(spawnVolume);
                item.transform.localPosition = targetLocalPos;
                item.transform.localRotation = targetLocalRot;
            }
        }
        OnVesselAmountChanged();
        RefreshParentHighlight();
    }

    void OnDrawGizmosSelected()
    {
        if (spawnVolume != null) {
            Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(spawnVolume.position, spawnVolume.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, volumeSize);
        }
        if (pourEntryVolume != null) {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(pourEntryVolume.position, pourEntryVolume.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(entryVolumeSize.x, 0.05f, entryVolumeSize.y));
            if (spawnVolume != null) {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawLine(pourEntryVolume.position, spawnVolume.position);
            }
        }
    }
}