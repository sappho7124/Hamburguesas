using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TableSpot : MonoBehaviour
{[Tooltip("The chair associated with this table zone.")]
    public SittingSpot linkedSittingSpot;
    
    public GameObject moneyPrefab; 
    public float eatingDuration = 5f;
    
    private bool isEating = false;
    private List<PlateItem> platesInZone = new List<PlateItem>();
    private HashSet<AssembledBurger> rejectedBurgers = new HashSet<AssembledBurger>();

    void OnTriggerEnter(Collider other)
    {
        PlateItem plate = other.GetComponentInParent<PlateItem>();
        if (plate != null && !platesInZone.Contains(plate)) platesInZone.Add(plate);
    }

    void OnTriggerExit(Collider other)
    {
        PlateItem plate = other.GetComponentInParent<PlateItem>();
        if (plate != null && platesInZone.Contains(plate))
        {
            platesInZone.Remove(plate);
            AssembledBurger burger = plate.GetComponentInChildren<AssembledBurger>();
            if (burger != null) rejectedBurgers.Remove(burger);
        }
    }

    void Update()
    {
        if (isEating || linkedSittingSpot == null || !linkedSittingSpot.isOccupied) return;
        platesInZone.RemoveAll(p => p == null);

        foreach (var plate in platesInZone)
        {
            EquippableItem eqPlate = plate.GetComponent<EquippableItem>();
            if (eqPlate != null && !eqPlate.GetRigidbody().isKinematic)
            {
                AssembledBurger burger = plate.GetComponentInChildren<AssembledBurger>();
                if (burger != null && !rejectedBurgers.Contains(burger))
                {
                    // --- NEW AUTO-CORRECTION LOGIC ---
                    TableSpot correctSpot = FindCorrectSpotInCluster(burger);
                    if (correctSpot != null && correctSpot != this)
                    {
                        // Slide the plate over to the correct table
                        eqPlate.transform.position = correctSpot.transform.position + (Vector3.up * 0.1f);
                        platesInZone.Remove(plate);
                        Debug.Log($"[TableSpot] Auto-corrected plate to adjacent table!");
                        break; // Let the correct table process it on the next frame
                    }

                    // --- NORMAL EVALUATION ---
                    int moneyToSpawn;
                    string customerDialogue;
                    
                    if (OrderManager.Instance.TryServeFood(this, burger, out moneyToSpawn, out customerDialogue))
                    {
                        RestaurantUIManager.Instance.ShowDialogue(OrderManager.Instance.GetActiveProfileName(this), customerDialogue);
                        StartCoroutine(EatRoutine(plate, eqPlate, burger, moneyToSpawn));
                    }
                    else
                    {
                        RestaurantUIManager.Instance.ShowDialogue(OrderManager.Instance.GetActiveProfileName(this), customerDialogue);
                        rejectedBurgers.Add(burger);
                    }
                    break; 
                }
            }
        }
    }

    private TableSpot FindCorrectSpotInCluster(AssembledBurger burger)
    {
        // 1. If THIS table wants it, just keep it here
        if (OrderManager.Instance.WouldAcceptBurger(this, burger)) return this;

        // 2. Search connected tables for a perfect match
        Queue<SittingSpot> queue = new Queue<SittingSpot>();
        HashSet<SittingSpot> visited = new HashSet<SittingSpot>();
        
        queue.Enqueue(linkedSittingSpot);
        visited.Add(linkedSittingSpot);

        TableSpot emptySpotWithOrder = null;

        while (queue.Count > 0)
        {
            SittingSpot current = queue.Dequeue();
            
            if (current.linkedTableSpot != null && current.linkedTableSpot != this)
            {
                // If it's a perfect match, teleport it to them!
                if (OrderManager.Instance.WouldAcceptBurger(current.linkedTableSpot, burger))
                {
                    return current.linkedTableSpot;
                }
                
                // Track ANY adjacent table that has an active order (Fallback)
                if (emptySpotWithOrder == null && OrderManager.Instance.HasActiveOrder(current.linkedTableSpot))
                {
                    emptySpotWithOrder = current.linkedTableSpot;
                }
            }

            foreach (var neighbor in current.connectedSpots)
            {
                if (neighbor != null && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // 3. Fallback: If no perfect match was found, but you placed the food on a table 
        // with NO active order, slide it to an adjacent table that DOES have an order so they can reject it verbally.
        if (!OrderManager.Instance.HasActiveOrder(this) && emptySpotWithOrder != null)
        {
            return emptySpotWithOrder;
        }

        // 4. Default: No better place found, keep it here and face the consequences
        return this;
    }

    private IEnumerator EatRoutine(PlateItem plate, EquippableItem eqPlate, AssembledBurger burger, int moneyToSpawn)
    {
        isEating = true;
        eqPlate.SetPhysics(false); 
        plate.enabled = false; 

        yield return new WaitForSeconds(eatingDuration);

        Destroy(burger.gameObject); 
        eqPlate.SetPhysics(true);
        plate.enabled = true;

        if (moneyPrefab != null && moneyToSpawn > 0)
        {
            GameObject moneyObj = Instantiate(moneyPrefab, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            MoneyPickup pickup = moneyObj.GetComponent<MoneyPickup>();
            if (pickup != null) pickup.Initialize(moneyToSpawn);
        }

        if (linkedSittingSpot.currentCustomer != null)
        {
            linkedSittingSpot.currentCustomer.Leave();
        }
        isEating = false;
    }
}