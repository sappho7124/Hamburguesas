using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Kitchen/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public string itemName;
    [TextArea] public string description; // Optional description
    public Sprite icon; // Optional icon for inventory later
}