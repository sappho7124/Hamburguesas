using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TableSpot : MonoBehaviour
{
    [Tooltip("The chair associated with this table zone.")]
    public SittingSpot linkedSittingSpot;
    
    public GameObject moneyPrefab; // NEW: Assign your Money Prefab here
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
                    // MODIFIED: Request dialogue and money from the OrderManager
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

    private IEnumerator EatRoutine(PlateItem plate, EquippableItem eqPlate, AssembledBurger burger, int moneyToSpawn)
    {
        isEating = true;
        eqPlate.SetPhysics(false); 
        plate.enabled = false; 

        yield return new WaitForSeconds(eatingDuration);

        Destroy(burger.gameObject); 
        eqPlate.SetPhysics(true);
        plate.enabled = true;

        // NEW: Spawn Money on the table
        if (moneyPrefab != null && moneyToSpawn > 0)
        {
            GameObject moneyObj = Instantiate(moneyPrefab, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            MoneyPickup pickup = moneyObj.GetComponent<MoneyPickup>();
            if (pickup != null) pickup.moneyValue = moneyToSpawn;
        }

        if (linkedSittingSpot.currentCustomer != null)
        {
            linkedSittingSpot.currentCustomer.Leave();
        }
        isEating = false;
    }
}