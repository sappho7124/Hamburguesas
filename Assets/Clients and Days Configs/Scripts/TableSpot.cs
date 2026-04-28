using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TableSpot : MonoBehaviour
{
    public SittingSpot linkedSittingSpot;
    public GameObject moneyPrefab; 
    public float eatingDuration = 5f;
    
    private bool isEating = false;
    private List<PlateItem> platesInZone = new List<PlateItem>();
    private HashSet<PlateItem> rejectedPlates = new HashSet<PlateItem>();

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
            rejectedPlates.Remove(plate);
        }
    }

    void Update()
    {
        if (isEating || linkedSittingSpot == null || !linkedSittingSpot.isOccupied) return;
        platesInZone.RemoveAll(p => p == null);

        foreach (var plate in platesInZone)
        {
            EquippableItem eqPlate = plate.GetComponent<EquippableItem>();
            // If the plate is resting on the table and has food/items on it...
            if (eqPlate != null && !eqPlate.GetRigidbody().isKinematic && plate.GetAttachedItems().Count > 0)
            {
                if (!rejectedPlates.Contains(plate))
                {
                    TableSpot correctSpot = FindCorrectSpotInCluster(plate);
                    if (correctSpot != null && correctSpot != this)
                    {
                        eqPlate.transform.position = correctSpot.transform.position + (Vector3.up * 0.1f);
                        platesInZone.Remove(plate);
                        Debug.Log($"[TableSpot] Auto-corrected plate to adjacent table!");
                        break; 
                    }

                    int moneyToSpawn;
                    string customerDialogue;
                    
                    if (OrderManager.Instance.TryServeFood(this, plate, out moneyToSpawn, out customerDialogue))
                    {
                        RestaurantUIManager.Instance.ShowDialogue(OrderManager.Instance.GetActiveProfileName(this), customerDialogue);
                        StartCoroutine(EatRoutine(plate, eqPlate, moneyToSpawn));
                    }
                    else
                    {
                        RestaurantUIManager.Instance.ShowDialogue(OrderManager.Instance.GetActiveProfileName(this), customerDialogue);
                        rejectedPlates.Add(plate);
                    }
                    break; 
                }
            }
        }
    }

    private TableSpot FindCorrectSpotInCluster(PlateItem plate)
    {
        if (OrderManager.Instance.WouldAcceptOrder(this, plate)) return this;

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
                if (OrderManager.Instance.WouldAcceptOrder(current.linkedTableSpot, plate))
                    return current.linkedTableSpot;
                
                if (emptySpotWithOrder == null && OrderManager.Instance.HasActiveOrder(current.linkedTableSpot))
                    emptySpotWithOrder = current.linkedTableSpot;
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

        if (!OrderManager.Instance.HasActiveOrder(this) && emptySpotWithOrder != null) return emptySpotWithOrder;
        return this;
    }

    private IEnumerator EatRoutine(PlateItem plate, EquippableItem eqPlate, int moneyToSpawn)
    {
        isEating = true;
        eqPlate.SetPhysics(false); 
        plate.enabled = false; 

        yield return new WaitForSeconds(eatingDuration);

        // --- Consume all food on the plate ---
        foreach (var item in plate.GetAttachedItems())
        {
            if (item != null && item.gameObject != plate.gameObject)
            {
                Destroy(item.gameObject);
            }
        }
        
        // --- Make the plate dirty ---
        plate.MakeDirty();

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