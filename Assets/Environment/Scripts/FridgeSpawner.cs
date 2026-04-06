using UnityEngine;
using System.Collections.Generic;

public class FridgeSpawner : MonoBehaviour
{
    [Header("Fridge Environment")]
    public float fridgeTemperature = 4f;
    [System.Serializable]
    public class FridgeStock
    {
        public string stockName = "Tomato Stock";
        [Tooltip("The definition to check for in the fridge.")]
        public ItemDefinition itemDefinition;
        [Tooltip("The prefab to spawn if we are missing this item.")]
        public GameObject prefabToSpawn;
        [Tooltip("Stop spawning if we have this many inside the fridge.")]
        public int maxThreshold = 3;
        [Tooltip("How large of an area to check around the spawn point.")]
        public float spawnPointCheckRadius = 0.1f;[Tooltip("Empty GameObjects representing where this item can appear.")]
        public List<Transform> spawnPoints;   
    }[Header("Fridge Stock Settings")]
    public List<FridgeStock> fridgeStocks;

    // This list represents the giant trigger covering the whole fridge.
    // It keeps track of total inventory regardless of what shelf things are on.
    private List<GrabbableItem> itemsInside = new List<GrabbableItem>();

void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return; 
        
        GrabbableItem item = other.GetComponentInParent<GrabbableItem>();
        if (item != null && !itemsInside.Contains(item))
        {
            itemsInside.Add(item);
        }

        CookableItem cookable = other.GetComponentInParent<CookableItem>();
        if (cookable != null)
        {
            cookable.targetEnvironmentTemperature = fridgeTemperature;
            if (cookable.isOnFire) cookable.Extinguish(); // Puts out the fire!
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;
        
        GrabbableItem item = other.GetComponentInParent<GrabbableItem>();
        if (item != null && itemsInside.Contains(item))
        {
            itemsInside.Remove(item);
        }

        CookableItem cookable = other.GetComponentInParent<CookableItem>();
        if (cookable != null)
        {
            cookable.targetEnvironmentTemperature = cookable.ambientTemperature;
        }
    }

public void TrySpawnItems()
    {
        //Debug.Log($"[FridgeSpawner] Door closed! Checking {itemsInside.Count} items.");
        
        // 1. Clean the list in case items were destroyed
        itemsInside.RemoveAll(i => i == null);

        // --- EXTINGUISH EVERYTHING INSIDE WHEN DOOR CLOSES ---
        foreach (var item in itemsInside)
        {
            CookableItem cookable = item.GetComponent<CookableItem>();
            if (cookable != null && cookable.isOnFire)
            {
                cookable.Extinguish();
                //Debug.Log($"[FridgeSpawner] Extinguished {item.gameObject.name} by closing the door.");
            }
        }

        if (fridgeStocks == null || fridgeStocks.Count == 0) return;

        foreach (var stock in fridgeStocks)
        {
            if (stock.itemDefinition == null || stock.prefabToSpawn == null) continue;

            // 2. GIANT TRIGGER CHECK: Count how many of this item type are ANYWHERE inside the fridge
            int currentCount = 0;
            foreach (var item in itemsInside)
            {
                if (item.itemDefinition == stock.itemDefinition)
                {
                    currentCount++;
                }
            }

             //Debug.Log($"[FridgeSpawner] '{stock.stockName}': Found {currentCount}/{stock.maxThreshold} inside.");

            // 3. If the total fridge inventory is below the threshold, try to spawn replacements
            if (currentCount < stock.maxThreshold)
            {
                int amountToSpawn = stock.maxThreshold - currentCount;
                int spawned = 0;

                foreach (Transform pt in stock.spawnPoints)
                {
                    if (pt == null) continue;
                    if (spawned >= amountToSpawn) break; // Finished spawning needed amount

                    // 4. LOCAL SPAWN CHECK: Ensure the exact spawn point isn't blocked by ANOTHER ITEM
                    Collider[] hits = Physics.OverlapSphere(pt.position, stock.spawnPointCheckRadius);
                    bool isOccupied = false;

                    foreach (var hit in hits)
                    {
                        if (hit.isTrigger) continue;

                        // FIX: Does this collider belong to a Grabbable Item or Rigidbody?
                        // If it doesn't have a GrabbableItem or Rigidbody, it is likely the Fridge Shelf. We ignore it!
                        GrabbableItem blockingItem = hit.GetComponentInParent<GrabbableItem>();
                        bool hasRigidbody = hit.attachedRigidbody != null;

                        if (blockingItem != null || hasRigidbody)
                        {
                            isOccupied = true;
                            //Debug.Log($"[FridgeSpawner] Spawn point '{pt.name}' is blocked by ITEM: {hit.gameObject.name}. Skipping point.");
                            break;
                        }
                    }

                    if (!isOccupied)
                    {
                        GameObject newItem = Instantiate(stock.prefabToSpawn, pt.position, pt.rotation);
                        //Debug.Log($"[FridgeSpawner] SUCCESS: Spawned '{stock.stockName}' at {pt.name}!");
                        
                        // Automatically register the newly spawned item into the fridge's memory
                        GrabbableItem newGrabbable = newItem.GetComponent<GrabbableItem>();
                        if (newGrabbable != null)
                        {
                            itemsInside.Add(newGrabbable);
                        }
                        
                        spawned++;
                    }
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f); 
        foreach (var stock in fridgeStocks)
        {
            foreach (Transform pt in stock.spawnPoints)
            {
                if (pt != null)
                {
                    Gizmos.DrawSphere(pt.position, stock.spawnPointCheckRadius);
                }
            }
        }
    }
}