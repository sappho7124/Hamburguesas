using UnityEngine;
using System.Collections.Generic;

public class SittingSpot : MonoBehaviour
{
    [Tooltip("The table surface zone where food should be placed for this chair.")]
    public TableSpot linkedTableSpot;
    
    [Header("Group Seating Network")][Tooltip("Drag adjacent SittingSpots here. If this is a stool, drag the stools to the left and right of it.")]
    public List<SittingSpot> connectedSpots = new List<SittingSpot>();

    [Header("Visual Settings")]
    [Tooltip("Local offset for the customer when seated. Use this to prevent them from clipping into the chair.")]
    public Vector3 customerOffset = Vector3.zero;

    [HideInInspector] public bool isReserved = false;
    [HideInInspector] public bool isOccupied = false;[HideInInspector] public Customer currentCustomer;

    public void ReserveSeat(Customer customer)
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

    // --- NEW: Visual Gizmo Helper ---
    // Draws a yellow sphere in the Scene view so you can visually see the exact seating position!
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // TransformPoint converts the local offset to global world space automatically!
        Vector3 targetPos = transform.TransformPoint(customerOffset);
        Gizmos.DrawWireSphere(targetPos, 0.2f);
        Gizmos.DrawLine(transform.position, targetPos);
    }
}