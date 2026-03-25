using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class OutlineCleanupTool : EditorWindow
{
    [MenuItem("Tools/Robot Factory/Remove Redundant Outlines")]
    public static void ShowWindow()
    {
        GetWindow<OutlineCleanupTool>("Cleanup Outlines");
    }

    void OnGUI()
    {
        GUILayout.Label("Cleanup Redundant Components", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Since HighlightableObject now adds the Outline component dynamically at runtime, we should remove any Outline components that are manually attached in the Editor to prevent conflicts.", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Clean All in Scene"))
        {
            CleanScene();
        }

        if (GUILayout.Button("Clean Selected Prefabs/Objects"))
        {
            CleanSelection();
        }
    }

    private void CleanScene()
    {
        HighlightableObject[] highlightables = FindObjectsByType<HighlightableObject>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var item in highlightables)
        {
            if (RemoveOutline(item.gameObject)) count++;
        }

        Debug.Log($"<color=green>Cleanup Complete:</color> Removed Outline component from {count} objects in the scene.");
    }

    private void CleanSelection()
    {
        int count = 0;
        foreach (GameObject obj in Selection.gameObjects)
        {
            // Check if it has HighlightableObject (we only want to clean those)
            if (obj.GetComponent<HighlightableObject>() != null)
            {
                if (RemoveOutline(obj)) count++;
            }
        }
        Debug.Log($"Removed Outline from {count} selected objects.");
    }

    private bool RemoveOutline(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        
        if (outline != null)
        {
            Undo.DestroyObjectImmediate(outline);
            EditorUtility.SetDirty(obj);
            return true;
        }
        return false;
    }
}