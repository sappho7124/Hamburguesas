using UnityEngine;
using UnityEditor;
using System.Linq; // Required for .ToList()

public class AutoLinkSeatingTool : EditorWindow
{[MenuItem("Tools/Kitchen Game/Auto-Link Tables and Chairs")]
    public static void AutoLinkSpots()
    {
        // --- FIXED: Using the new Unity 2023+ syntax ---
        SittingSpot[] allChairs = FindObjectsByType<SittingSpot>(FindObjectsSortMode.None);
        TableSpot[] allTables = FindObjectsByType<TableSpot>(FindObjectsSortMode.None);

        int linksMade = 0;
        int conflicts = 0;
        int orphans = 0;

        // Pass 1: Chairs -> Tables
        foreach (SittingSpot chair in allChairs)
        {
            if (chair.linkedTableSpot != null)
            {
                if (chair.linkedTableSpot.linkedSittingSpot != chair)
                {
                    if (chair.linkedTableSpot.linkedSittingSpot != null)
                    {
                        Debug.LogWarning($"[Conflict] {chair.name} wants to link to {chair.linkedTableSpot.name}, but that table is already linked to {chair.linkedTableSpot.linkedSittingSpot.name}.");
                        conflicts++;
                    }
                    else
                    {
                        Undo.RecordObject(chair.linkedTableSpot, "Auto-Link Table to Chair");
                        chair.linkedTableSpot.linkedSittingSpot = chair;
                        EditorUtility.SetDirty(chair.linkedTableSpot);
                        linksMade++;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Unlinked] The Chair '{chair.name}' has no table assigned to it!");
                orphans++;
            }
        }

        // Pass 2: Tables -> Chairs
        foreach (TableSpot table in allTables)
        {
            if (table.linkedSittingSpot != null)
            {
                if (table.linkedSittingSpot.linkedTableSpot != table)
                {
                    if (table.linkedSittingSpot.linkedTableSpot != null)
                    {
                        Debug.LogWarning($"[Conflict] {table.name} wants to link to {table.linkedSittingSpot.name}, but that chair is already linked to {table.linkedSittingSpot.linkedTableSpot.name}.");
                        conflicts++;
                    }
                    else
                    {
                        Undo.RecordObject(table.linkedSittingSpot, "Auto-Link Chair to Table");
                        table.linkedSittingSpot.linkedTableSpot = table;
                        EditorUtility.SetDirty(table.linkedSittingSpot);
                        linksMade++;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Unlinked] The Table '{table.name}' has no chair assigned to it!");
                orphans++;
            }
        }

        // --- NEW: Auto-assign chairs to the Customer Spawner ---
        CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        if (spawner != null)
        {
            Undo.RecordObject(spawner, "Auto-Assign Sitting Spots");
            spawner.allSittingSpots = allChairs.ToList();
            EditorUtility.SetDirty(spawner);
            Debug.Log($"<color=#00FF00>[Auto-Link]</color> Auto-Assigned {allChairs.Length} Sitting Spots to the Customer Spawner.");
        }
        else
        {
            Debug.LogWarning("[Auto-Link] Could not find the CustomerSpawner in the scene to auto-assign chairs!");
        }

        // Final Report
        if (linksMade > 0) Debug.Log($"<color=#00FF00><b>[Auto-Link Complete]</b></color> Created {linksMade} missing connections.");
        else if (conflicts == 0) Debug.Log("<color=#00FFFF>[Auto-Link Complete]</color> All existing tables and chairs are perfectly linked!");
        
        if (orphans > 0) Debug.LogWarning($"<color=#FF8800>[Auto-Link Complete]</color> Found {orphans} totally unlinked spots. Check the warnings above!");
    }
}