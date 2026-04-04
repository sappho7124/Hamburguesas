using UnityEngine;

public class SittingSpot : MonoBehaviour
{[Tooltip("The table surface zone where food should be placed for this chair.")]
    public TableSpot linkedTableSpot;
    
    public bool isOccupied = false;

    [Header("Testing / Debug Visuals")]
    public float cylinderHeightOffset = 1.0f;
    public Color customerColor = new Color(0.2f, 0.4f, 0.8f); 
    
    private GameObject testCustomerVisual;

    public void OccupySpot()
    {
        isOccupied = true;

        // Create the test cylinder if it doesn't exist yet
        if (testCustomerVisual == null)
        {
            testCustomerVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            testCustomerVisual.name = "Test_Customer";
            testCustomerVisual.transform.SetParent(transform);
            
            // Move it up so it sits on the chair instead of in the floor
            testCustomerVisual.transform.localPosition = new Vector3(0, cylinderHeightOffset, 0);
            
            // IMPORTANT: Destroy the collider so the cylinder doesn't block player raycasts or plates!
            Destroy(testCustomerVisual.GetComponent<Collider>());
            
            // Color it
            Renderer r = testCustomerVisual.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = customerColor;
            }
        }
        
        testCustomerVisual.SetActive(true);
    }

    public void ClientLeaves()
    {
        isOccupied = false;

        // Hide the test cylinder when the customer leaves
        if (testCustomerVisual != null)
        {
            testCustomerVisual.SetActive(false);
        }

        Debug.Log($"[SittingSpot] Client left {gameObject.name}. Spot is now open.");
    }
}