using UnityEngine;
using UnityEngine.AI;

public class CustomerDoor : MonoBehaviour
{
    [Header("Door Pivot Settings")]
    [Tooltip("The Transform that represents the door's hinge pivot.")]
    public Transform hingePivot;
    
    [Tooltip("The local Euler angles when the door is OPEN.")]
    public Vector3 openLocalRotation = new Vector3(0, 90, 0);
    
    [Tooltip("How fast the door swings.")]
    public float swingSpeed = 6f;

    private Vector3 closedLocalRotation;
    private int customersInTrigger = 0;

    void Start()
    {
        if (hingePivot == null) hingePivot = transform;
        closedLocalRotation = hingePivot.localEulerAngles;
    }

    void Update()
    {
        Vector3 targetRotation = (customersInTrigger > 0) ? openLocalRotation : closedLocalRotation;
        
        hingePivot.localRotation = Quaternion.Slerp(
            hingePivot.localRotation, 
            Quaternion.Euler(targetRotation), 
            Time.deltaTime * swingSpeed
        );
    }

    void OnTriggerEnter(Collider other)
    {
        // Only trigger for NPCs walking on the NavMesh
        if (other.GetComponentInParent<NavMeshAgent>() != null)
        {
            customersInTrigger++;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<NavMeshAgent>() != null)
        {
            customersInTrigger--;
            if (customersInTrigger < 0) customersInTrigger = 0;
        }
    }
}