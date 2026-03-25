using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject objectToSpawn; // Drag your Robot Part prefab here
    public Vector3 spawnOffset = new Vector3(0, 1, 0); // Where to spawn relative to this object
    
    // Call this function from your Button's "OnInteract" event
    public void SpawnObject()
    {
        if (objectToSpawn != null)
        {
            Instantiate(objectToSpawn, transform.position + spawnOffset, transform.rotation);
        }
        else
        {
            Debug.LogWarning("No object assigned to spawner!");
        }
    }
}