using UnityEngine;

public class GrabbableItem : MonoBehaviour 
{
    [Header("Item Identity")]
    [Tooltip("What is this object? (e.g. Crowbar, Battery)")]
    public ItemDefinition itemDefinition; 

    [Header("Physics Settings")]
    [Tooltip("Check this for Baskets/Crates (Increases mass when held).")]
    public bool isContainer = false;
}