using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(HingeJoint))]
public class CustomerDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("Target angle when the door is open.")]
    public float openAngle = 90f;

    [Tooltip("Target angle when the door is closed.")]
    public float closedAngle = 0f;

    [Tooltip("Spring strength.")]
    public float springForce = 50f;

    [Tooltip("Damping applied to the spring.")]
    public float springDamper = 5f;

    private HingeJoint hinge;
    private int customersInTrigger = 0;

    void Start()
    {
        hinge = GetComponent<HingeJoint>();

        JointSpring spring = hinge.spring;
        spring.spring = springForce;
        spring.damper = springDamper;
        spring.targetPosition = closedAngle;

        hinge.spring = spring;
        hinge.useSpring = true;
    }

    void Update()
    {
        JointSpring spring = hinge.spring;
        spring.targetPosition = customersInTrigger > 0 ? openAngle : closedAngle;
        hinge.spring = spring;
    }

    void OnTriggerEnter(Collider other)
    {
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

            if (customersInTrigger < 0)
                customersInTrigger = 0;
        }
    }
}