using UnityEngine;

public class TrashCan : MonoBehaviour
{
    public ParticleSystem trashVFX;
    
    [Header("Rejection Force")]
    public float bounceForce = 5f;

    void OnTriggerEnter(Collider other)
    {
        // Don't interact with player or purely trigger colliders
        if (other.isTrigger && other.GetComponentInParent<GrabbableItem>() == null) return;

        GrabbableItem item = other.GetComponentInParent<GrabbableItem>();
        if (item != null)
        {
            EquippableItem equip = item.GetComponent<EquippableItem>();
            
            // If it is a tool or plate, DO NOT DESTROY IT! Bounce it out.
            if (equip != null)
            {
                Rigidbody rb = equip.GetRigidbody();
                if (rb != null && !rb.isKinematic)
                {
                    // Create a direction straight up, with a little random tilt so it doesn't just bounce infinitely in place
                    Vector3 pushDir = Vector3.up + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
                    
                    // Reset velocity so it doesn't fight falling momentum, then pop it out
                    rb.linearVelocity = Vector3.zero; 
                    rb.AddForce(pushDir.normalized * bounceForce, ForceMode.Impulse);
                    
                    Debug.Log($"[TrashCan] Rejected {item.gameObject.name}! Tools cannot be thrown away.");
                }
            }
            else
            {
                // It's normal food/trash. Destroy it.
                if (trashVFX != null) trashVFX.Play();
                Destroy(item.gameObject);
            }
        }
    }
}