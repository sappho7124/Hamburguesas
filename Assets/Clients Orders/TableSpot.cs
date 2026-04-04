using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TableSpot : MonoBehaviour
{
    [Tooltip("The chair associated with this table zone.")]
    public SittingSpot linkedSittingSpot;
    
    public float eatingDuration = 5f;
    private bool isEating = false;

    // Track items using a list instead of OnTriggerStay to avoid Rigidbody Sleeping issues
    private List<PlateItem> platesInZone = new List<PlateItem>();
    private HashSet<AssembledBurger> rejectedBurgers = new HashSet<AssembledBurger>();

    void OnTriggerEnter(Collider other)
    {
        PlateItem plate = other.GetComponentInParent<PlateItem>();
        if (plate != null && !platesInZone.Contains(plate))
        {
            platesInZone.Add(plate);
        }
    }

    void OnTriggerExit(Collider other)
    {
        PlateItem plate = other.GetComponentInParent<PlateItem>();
        if (plate != null && platesInZone.Contains(plate))
        {
            platesInZone.Remove(plate);
            
            // Forget the rejection if the player removes the plate to fix it
            AssembledBurger burger = plate.GetComponentInChildren<AssembledBurger>();
            if (burger != null) rejectedBurgers.Remove(burger);
        }
    }

    void Update()
    {
        //This should be optimized to only check on trigger, but that doesnt work for some reason so it will stay like this for now
        if (isEating || linkedSittingSpot == null || !linkedSittingSpot.isOccupied) return;

        // Clean up any deleted plates
        platesInZone.RemoveAll(p => p == null);

        foreach (var plate in platesInZone)
        {
            EquippableItem eqPlate = plate.GetComponent<EquippableItem>();
            
            // Only trigger if the player has dropped the plate (it is no longer kinematic)
            if (eqPlate != null && !eqPlate.GetRigidbody().isKinematic)
            {
                AssembledBurger burger = plate.GetComponentInChildren<AssembledBurger>();
                if (burger != null && !rejectedBurgers.Contains(burger))
                {
                    // Ask the Manager if this food is correct!
                    if (OrderManager.Instance.TryServeFood(this, burger))
                    {
                        StartCoroutine(EatRoutine(plate, eqPlate, burger));
                    }
                    else
                    {
                        // Add to rejected list so they don't spam the complaint every frame
                        rejectedBurgers.Add(burger);
                    }
                    
                    break; // Only process one plate per frame
                }
            }
        }
    }

    private IEnumerator EatRoutine(PlateItem plate, EquippableItem eqPlate, AssembledBurger burger)
    {
        isEating = true;
        
        eqPlate.SetPhysics(false); 
        plate.enabled = false; 

        yield return new WaitForSeconds(eatingDuration);

        Destroy(burger.gameObject); 
        eqPlate.SetPhysics(true);
        plate.enabled = true;

        linkedSittingSpot.ClientLeaves();
        isEating = false;
    }
}