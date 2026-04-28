// Location: C:\Games\Unity\Hamburguesas\Assets\Environment\Scripts\InteractableObject.cs
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class InteractionMapping
{
    public string displayVerb = "Interact";
    public string inputActionName = "Interact";
    public bool isFallbackAction = false;
    public bool isMainLock = false; 
    public List<ItemDefinition> validItems = new List<ItemDefinition>();
    public Color validColor = Color.white;
    public Color lockedColor = Color.red;
    public UnityEvent OnInteract;
}

public class InteractableObject : MonoBehaviour
{
    [Header("UI Control")]
    public bool manageUI = true;
    
    [Tooltip("If TRUE, this object will not show red/locked when you lack the tool. It will just act like a normal grabbable/equippable item until the tool is equipped.")]
    public bool hideLockedState = false;

    [Header("Dynamic Interactions")]
    public List<InteractionMapping> interactions = new List<InteractionMapping>();

    [HideInInspector] public string lockedVerb = "Requires Tool"; 

    void Awake()
    {
        if (interactions.Count == 0) return;

        bool hasFallback = false;
        bool hasMainLock = false;

        foreach (var mapping in interactions)
        {
            if (mapping.isFallbackAction) hasFallback = true;
            if (mapping.isMainLock) hasMainLock = true;
        }

        if (hasFallback && hasMainLock)
        {
            Debug.LogError($"[InteractableObject] Error on {gameObject.name}: You cannot mix Fallback actions and Main Lock actions on the same object!");
        }
    }

    public InteractionMapping GetValidMapping(ItemDefinition heldItem)
    {
        if (heldItem != null)
        {
            foreach (var mapping in interactions)
                if (mapping.validItems.Contains(heldItem)) return mapping;
        }
        foreach (var mapping in interactions)
            if (mapping.isFallbackAction) return mapping;
            
        return null; 
    }

    public InteractionMapping GetMainLockMapping()
    {
        foreach (var mapping in interactions)
            if (mapping.isMainLock) return mapping;
            
        return interactions.Count > 0 ? interactions[0] : null; 
    }

    public bool TryInteract(ItemDefinition heldItem, EquipmentController eq)
    {
        InteractionMapping mapping = GetValidMapping(heldItem);
        if (mapping == null) return false;

        var validators = GetComponents<IInteractionValidator>();
        foreach (var validator in validators)
        {
            if (!validator.IsInteractionValid(eq, out string failReason))
            {
                Debug.Log($"[Interaction Blocked] {failReason}");
                return false;
            }
        }

        mapping.OnInteract.Invoke();
        return true;
    }
}