using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BurgerAssemblyStation : MonoBehaviour
{
    [Header("Assembly Rules")]
    public ItemDefinition completedBurgerDefinition;
    
    [Header("Clustering Settings (Multi-Burger)")]
    public float stackGroupingRadius = 0.4f;
    public float strictVerticalRadius = 0.3f;

    [Header("Animation Settings")]
    public float skewerDropHeight = 1.5f;     // How high the skewer starts before falling
    public float skewerDropDuration = 0.25f;  // How fast it falls

    [Header("Prefabs")]
    public GameObject skewerPrefab; 

    // Tracks items currently sitting in the Trigger Collider
    private List<GrabbableItem> itemsInZone = new List<GrabbableItem>();

    void OnTriggerEnter(Collider other)
    {
        GrabbableItem grabbable = other.GetComponentInParent<GrabbableItem>();
        EquippableItem equippable = other.GetComponentInParent<EquippableItem>();

        // Must be grabbable, must NOT be equippable, and not already in the list
        if (grabbable != null && equippable == null && !itemsInZone.Contains(grabbable))
        {
            itemsInZone.Add(grabbable);
        }
    }

    void OnTriggerExit(Collider other)
    {
        GrabbableItem grabbable = other.GetComponentInParent<GrabbableItem>();
        if (grabbable != null && itemsInZone.Contains(grabbable))
        {
            itemsInZone.Remove(grabbable);
        }
    }

    public void TryAssembleBurger()
    {
        // 1. Clean the list in case items were destroyed
        itemsInZone.RemoveAll(item => item == null);

        if (itemsInZone.Count == 0)
        {
            Debug.LogWarning("[Assembly] No valid ingredients found on the station.");
            return;
        }

        List<GrabbableItem> allValidItems = new List<GrabbableItem>(itemsInZone);
        List<List<GrabbableItem>> itemClusters = GroupItemsIntoStacks(allValidItems);
        
        Debug.Log($"[Assembly] Detected {itemClusters.Count} distinct item cluster(s).");
        int successCount = 0;

        foreach (var stack in itemClusters)
        {
            var orderedStack = stack.OrderBy(item => item.transform.position.y).ToList();

            if (orderedStack.Count < 2) continue; 

            float avgX = orderedStack.Average(item => item.transform.position.x);
            float avgZ = orderedStack.Average(item => item.transform.position.z);
            float avgY = orderedStack.Average(item => item.transform.position.y);
            Vector2 stackCenterXZ = new Vector2(avgX, avgZ);

            bool isStraight = true;

            foreach (var item in orderedStack)
            {
                Vector2 itemXZ = new Vector2(item.transform.position.x, item.transform.position.z);
                if (Vector2.Distance(itemXZ, stackCenterXZ) > strictVerticalRadius)
                {
                    Debug.LogWarning($"[Assembly Failed] Cluster is not stacked straight! '{item.gameObject.name}' is falling off.");
                    isStraight = false;
                    break; 
                }
            }

            if (isStraight)
            {
                Debug.Log($"[Assembly Success] Built Burger with {orderedStack.Count} ingredients!");
                
                // Immediately remove these ingredients from the zone list so they can't be processed twice
                foreach (var item in orderedStack) itemsInZone.Remove(item);

                // Start the animated assembly process
                StartCoroutine(AssembleBurgerRoutine(orderedStack, new Vector3(avgX, avgY, avgZ)));
                successCount++;
            }
        }
    }

    private List<List<GrabbableItem>> GroupItemsIntoStacks(List<GrabbableItem> items)
    {
        List<List<GrabbableItem>> clusters = new List<List<GrabbableItem>>();
        HashSet<GrabbableItem> assignedItems = new HashSet<GrabbableItem>();

        foreach (var item in items)
        {
            if (assignedItems.Contains(item)) continue;

            List<GrabbableItem> currentCluster = new List<GrabbableItem>();
            Queue<GrabbableItem> queue = new Queue<GrabbableItem>();
            
            queue.Enqueue(item);
            assignedItems.Add(item);

            while (queue.Count > 0)
            {
                GrabbableItem current = queue.Dequeue();
                currentCluster.Add(current);
                Vector2 currentXZ = new Vector2(current.transform.position.x, current.transform.position.z);

                foreach (var otherItem in items)
                {
                    if (!assignedItems.Contains(otherItem))
                    {
                        Vector2 otherXZ = new Vector2(otherItem.transform.position.x, otherItem.transform.position.z);
                        if (Vector2.Distance(currentXZ, otherXZ) <= stackGroupingRadius)
                        {
                            assignedItems.Add(otherItem);
                            queue.Enqueue(otherItem);
                        }
                    }
                }
            }
            clusters.Add(currentCluster);
        }
        return clusters;
    }

    private IEnumerator AssembleBurgerRoutine(List<GrabbableItem> ingredients, Vector3 centerPosition)
    {
        // 1. FREEZE INGREDIENTS INSTANTLY
        // We strip rigidbodies immediately so the player can't knock them over while the skewer is falling
        foreach (var ingredient in ingredients)
        {
            Rigidbody rb = ingredient.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            // Turn off their highlights temporarily so it looks clean during the animation
            HighlightableObject highlight = ingredient.GetComponent<HighlightableObject>();
            if (highlight != null) highlight.ToggleHighlight(false);
        }

        // 2. SPAWN SKEWER IN THE AIR
        Vector3 dropStartPos = centerPosition + (Vector3.up * skewerDropHeight);
        GameObject skewer = Instantiate(skewerPrefab, dropStartPos, Quaternion.identity);
        skewer.name = "Completed Burger";

        // 3. ANIMATE SKEWER FALLING
        float elapsed = 0f;
        while (elapsed < skewerDropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / skewerDropDuration);
            
            // "t * t" acts as an ease-in, simulating gravity speeding up as it falls
            skewer.transform.position = Vector3.Lerp(dropStartPos, centerPosition, t * t);
            
            yield return null;
        }

        // Snap to exact center just in case
        skewer.transform.position = centerPosition;

        // 4. FINALIZE ASSEMBLY
        Rigidbody skewerRb = skewer.AddComponent<Rigidbody>();
        skewerRb.mass = ingredients.Count * 0.5f; 
        skewerRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        GrabbableItem masterGrabbable = skewer.AddComponent<GrabbableItem>();
        masterGrabbable.itemDefinition = completedBurgerDefinition;

        HighlightableObject masterHighlight = skewer.AddComponent<HighlightableObject>();
        masterHighlight.objectName = "Burger";
        masterHighlight.interactionVerb = "Grab";
        masterHighlight.animSpeed = 15f;
        masterHighlight.maxOutlineWidth = 10f;
        masterHighlight.hoverColor = Color.white;

        foreach (var ingredient in ingredients)
        {
            // Parent it to the newly landed skewer
            ingredient.transform.SetParent(skewer.transform, true);

            HighlightableObject highlight = ingredient.GetComponent<HighlightableObject>();
            if (highlight != null)
            {
                if (highlight.OutlineComponent != null) Destroy(highlight.OutlineComponent);
                Destroy(highlight);
            }

            CookableItem cookable = ingredient.GetComponent<CookableItem>();
            if (cookable != null) Destroy(cookable);

            // Destroy individual grab logic
            Destroy(ingredient); 
        }
    }
}