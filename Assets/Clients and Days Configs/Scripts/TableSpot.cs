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
        if (plate != null && !platesInZone.Contains(plate)) 
        {
            platesInZone.Add(plate);
            // NEW: Standing near the table with a plate triggers Step 10!
            if (StoryFlowManager.Instance != null) StoryFlowManager.Instance.ReportAction("HoverTable"); 
        }
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
        if (isEating || linkedSittingSpot == null) return;

        platesInZone.RemoveAll(p => p == null);

        foreach (var plate in platesInZone)
        {
            EquippableItem eqPlate = plate.GetComponent<EquippableItem>();
            if (eqPlate != null && !eqPlate.GetRigidbody().isKinematic && plate.GetAttachedItems().Count > 0)
            {
                if (!rejectedPlates.Contains(plate))
                {
                    TableSpot correctSpot = FindCorrectSpotInCluster(plate);
                    if (correctSpot != null && correctSpot != this)
                    {
                        eqPlate.transform.position = correctSpot.transform.position + (Vector3.up * 0.1f);
                        platesInZone.Remove(plate);
                        Debug.Log($"[TableSpot] Auto-corrected plate to adjacent table '{correctSpot.name}'!");
                        break; 
                    }

                    int moneyToSpawn;
                    string customerDialogue;
                    CustomerFaceController.Mood reactionMood;

                    CustomerFaceController face = linkedSittingSpot.currentCustomer != null ? linkedSittingSpot.currentCustomer.faceController : null;

                    // Clean check relying entirely on the manager
                    if (OrderManager.Instance.TryServeFood(this, plate, out moneyToSpawn, out customerDialogue, out reactionMood))
                    {
                        // Table just reports what happened, taking no decisions
                        if (StoryFlowManager.Instance != null) StoryFlowManager.Instance.ReportAction("ServeTable");

                        if (face != null) face.SetMood(reactionMood);
                        if (!string.IsNullOrEmpty(customerDialogue)) 
                        {
                            RestaurantUIManager.Instance.ShowDialogue(OrderManager.Instance.GetActiveProfileName(this), customerDialogue, reactionMood, face);
                        }
                        StartCoroutine(EatRoutine(plate, eqPlate, moneyToSpawn));
                    }
                    else
                    {
                        Debug.Log($"[TableSpot] Food rejected by customer!");
                        if (face != null) face.SetMood(reactionMood);
                        if (!string.IsNullOrEmpty(customerDialogue))
                        {
                            RestaurantUIManager.Instance.ShowDialogue(OrderManager.Instance.GetActiveProfileName(this), customerDialogue, reactionMood, face, false, null, 3f);
                        }
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
        //plate.MakeDirty();

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
            // If they have a post-meal conversation, make them wait!
            if (!string.IsNullOrEmpty(linkedSittingSpot.currentCustomer.profile.yarnPostMealNodeName))
            {
                linkedSittingSpot.currentCustomer.MarkAsFinishedEating();
            }
            else
            {
                // Normal customer, leave immediately
                linkedSittingSpot.currentCustomer.Leave();
            }
        }
        isEating = false;
    } // End of EatRoutine
}