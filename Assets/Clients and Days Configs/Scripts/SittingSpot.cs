using UnityEngine;
using System.Collections.Generic;

public class SittingSpot : MonoBehaviour
{
    [Tooltip("The table surface zone where food should be placed for this chair.")]
    public TableSpot linkedTableSpot;
    
    [Header("Group Seating Network")][Tooltip("Drag adjacent SittingSpots here. If this is a stool, drag the stools to the left and right of it.")]
    public List<SittingSpot> connectedSpots = new List<SittingSpot>();
    
    [HideInInspector] public bool isReserved = false;[HideInInspector] public bool isOccupied = false;
    [HideInInspector] public CustomerPill currentCustomer;

    public void ReserveSeat(CustomerPill customer)
    {
        isReserved = true;
        currentCustomer = customer;
    }

    public void OccupySpot()
    {
        isOccupied = true;
    }

    public void FreeSeat()
    {
        isReserved = false;
        isOccupied = false;
        currentCustomer = null;
    }
}