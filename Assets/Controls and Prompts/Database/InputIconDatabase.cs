using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "InputIconDatabase", menuName = "UI/Input Icon Database")]
public class InputIconDatabase : ScriptableObject
{
    [System.Serializable]
    public struct IconMapping
    {
        public string inputPath; // e.g. "<Keyboard>/e" or "<Mouse>/leftButton"
        public Sprite icon;
    }

    public List<IconMapping> icons;
    public Sprite defaultIcon; // Fallback if not found

    public Sprite GetSprite(string path)
    {
        // Clean the path slightly to ensure matches (Unity sometimes adds control schemes)
        // This simple check looks for exact matches first
        foreach (var mapping in icons)
        {
            if (mapping.inputPath == path) return mapping.icon;
        }
        
        // Optional: Add logic here to handle case-sensitivity or partial matches
        
        return defaultIcon;
    }
}