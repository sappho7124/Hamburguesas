using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DuplicateRemoverTool : EditorWindow
{
    [MenuItem("Tools/Robot Factory/Remove Duplicate Scripts")]
    public static void ShowWindow()
    {
        GetWindow<DuplicateRemoverTool>("Dedup Tool");
    }

    void OnGUI()
    {
        GUILayout.Label("Clean Up Duplicates", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Finds objects with multiple instances of the same component and removes the extras.", MessageType.Warning);

        if (GUILayout.Button("Scan & Clean Selection"))
        {
            CleanDuplicates(Selection.gameObjects);
        }

        if (GUILayout.Button("Scan & Clean Entire Scene"))
        {
            // Find all root objects, then get all children
            List<GameObject> allObjects = new List<GameObject>();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                allObjects.Add(root);
                allObjects.AddRange(GetChildren(root));
            }
            CleanDuplicates(allObjects.ToArray());
        }
    }

    List<GameObject> GetChildren(GameObject root)
    {
        List<GameObject> list = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject != root) list.Add(t.gameObject);
        }
        return list;
    }

    void CleanDuplicates(GameObject[] objects)
    {
        int totalRemoved = 0;
        int objectsAffected = 0;

        foreach (GameObject obj in objects)
        {
            bool modified = false;

            // List of scripts we care about (or just check all MonoBehaviours)
            // Let's check ALL MonoBehaviours to be safe.
            var components = obj.GetComponents<MonoBehaviour>();
            
            // Group by Type
            var groups = components.GroupBy(c => c.GetType());

            foreach (var group in groups)
            {
                // If more than 1 of the same type exists
                if (group.Count() > 1)
                {
                    // Keep the first one, destroy the rest
                    var list = group.ToList();
                    
                    // Skip the first one [0]
                    for (int i = 1; i < list.Count; i++)
                    {
                        Undo.DestroyObjectImmediate(list[i]);
                        totalRemoved++;
                        modified = true;
                    }
                    Debug.Log($"Cleaned {group.Key.Name} on {obj.name}");
                }
            }

            // Also check for specific Outline duplication (since it's required)
            var outlines = obj.GetComponents<Outline>();
            if (outlines.Length > 1)
            {
                for (int i = 1; i < outlines.Length; i++)
                {
                    Undo.DestroyObjectImmediate(outlines[i]);
                    totalRemoved++;
                    modified = true;
                }
            }

            if (modified) objectsAffected++;
        }

        if (totalRemoved > 0)
        {
            Debug.Log($"<color=green>Cleanup Complete:</color> Removed {totalRemoved} duplicate components from {objectsAffected} objects.");
        }
        else
        {
            Debug.Log("No duplicates found.");
        }
    }
}