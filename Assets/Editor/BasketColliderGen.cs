using UnityEngine;
using UnityEditor;

public class BasketColliderGen : MonoBehaviour
{
    [MenuItem("CONTEXT/MeshFilter/Generate Basket Colliders")]
    static void GenerateBasketColliders(MenuCommand command)
    {
        MeshFilter mf = (MeshFilter)command.context;
        GameObject obj = mf.gameObject;
        
        // Ensure we have the mesh bounds
        Mesh mesh = mf.sharedMesh;
        if (mesh == null) return;

        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;
        Vector3 center = bounds.center;

        // Settings for wall thickness
        float thickness = 0.1f; // Adjust this if walls are too thin/thick

        Undo.RegisterCompleteObjectUndo(obj, "Generate Basket Colliders");

        // Remove existing colliders to prevent duplicates
        foreach (var c in obj.GetComponents<BoxCollider>())
        {
            Undo.DestroyObjectImmediate(c);
        }

        // 1. Floor
        CreateBox(obj, 
            new Vector3(center.x, center.y - size.y/2 + thickness/2, center.z), 
            new Vector3(size.x, thickness, size.z));

        // 2. Left Wall
        CreateBox(obj, 
            new Vector3(center.x - size.x/2 + thickness/2, center.y, center.z), 
            new Vector3(thickness, size.y, size.z));

        // 3. Right Wall
        CreateBox(obj, 
            new Vector3(center.x + size.x/2 - thickness/2, center.y, center.z), 
            new Vector3(thickness, size.y, size.z));

        // 4. Front Wall
        CreateBox(obj, 
            new Vector3(center.x, center.y, center.z + size.z/2 - thickness/2), 
            new Vector3(size.x, size.y, thickness));

        // 5. Back Wall
        CreateBox(obj, 
            new Vector3(center.x, center.y, center.z - size.z/2 + thickness/2), 
            new Vector3(size.x, size.y, thickness));
            
        Debug.Log("Basket Colliders Generated!");
    }

    static void CreateBox(GameObject go, Vector3 center, Vector3 size)
    {
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = center;
        bc.size = size;
    }
}