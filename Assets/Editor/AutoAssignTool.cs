using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AutoAssignTool : EditorWindow
{
    [MenuItem("Tools/Robot Factory/Auto-Assign Highlightables")]
    public static void ShowWindow()
    {
        GetWindow<AutoAssignTool>("Auto-Assign");
    }

    void OnGUI()
    {
        GUILayout.Label("Fix Missing Components", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This tool finds objects with 'GrabbableItem' or 'InteractableObject' scripts that are missing the 'HighlightableObject' component and adds it automatically.", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix All in Current Scene"))
        {
            FixSceneObjects();
        }

        if (GUILayout.Button("Fix Selected Prefabs/Objects"))
        {
            FixSelectedObjects();
        }
    }

    private void FixSceneObjects()
    {
        // Find all MonoBehaviours to filter them
        GrabbableItem[] grabbables = FindObjectsByType<GrabbableItem>(FindObjectsSortMode.None);
        InteractableObject[] interactables = FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);

        int count = 0;

        // 1. Process Grabbables
        foreach (var item in grabbables)
        {
            if (EnsureHighlightable(item.gameObject, "Grab")) count++;
        }

        // 2. Process Interactables
        foreach (var item in interactables)
        {
            if (EnsureHighlightable(item.gameObject, "Interact")) count++;
        }

        if (count > 0)
        {
            Debug.Log($"<color=green>Success:</color> Added HighlightableObject to {count} items in the scene.");
        }
        else
        {
            Debug.Log("Scene is clean! No missing components found.");
        }
    }

    private void FixSelectedObjects()
    {
        int count = 0;
        foreach (GameObject obj in Selection.gameObjects)
        {
            // Check for scripts on this object
            bool hasGrabbable = obj.GetComponent<GrabbableItem>() != null;
            bool hasInteractable = obj.GetComponent<InteractableObject>() != null;

            if (hasGrabbable)
            {
                if (EnsureHighlightable(obj, "Grab")) count++;
            }
            else if (hasInteractable)
            {
                if (EnsureHighlightable(obj, "Interact")) count++;
            }
        }
        
        Debug.Log($"Fixed {count} selected objects.");
    }

    private bool EnsureHighlightable(GameObject obj, string defaultVerb)
    {
        // Check if it already exists
        if (obj.GetComponent<HighlightableObject>() != null) return false;

        // Register Undo so you can Ctrl+Z
        Undo.AddComponent<HighlightableObject>(obj);
        
        // Get the new component to configure defaults
        HighlightableObject highlight = obj.GetComponent<HighlightableObject>();
        
        // --- APPLY DEFAULT SETTINGS ---
        highlight.interactionVerb = defaultVerb;
        highlight.objectName = obj.name; // Default to GameObject name
        
        // Physics items usually need a bit more slide distance for visual clarity
        if (defaultVerb == "Grab")
        {
            highlight.hoverColor = Color.yellow;
            highlight.slideInDistance = 0.5f;
        }
        // Interactables usually default to Cyan (optional preference)
        else
        {
            highlight.hoverColor = Color.cyan;
            highlight.slideInDistance = 0.5f;
        }

        // Mark as dirty so Unity saves the scene change
        EditorUtility.SetDirty(obj);
        
        return true;
    }
}