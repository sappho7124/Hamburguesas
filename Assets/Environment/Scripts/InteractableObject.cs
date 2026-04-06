using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour
{
    [Header("Requirements")]
    public ItemDefinition requiredItem;
    public string lockedVerb = "Requires Tool";
    public Color lockedColor = Color.red;

    [Header("Minigame (Optional)")]
    public MinigameBase linkedMinigame;

    [Header("Event Logic")]
    public UnityEvent OnInteract;

    public bool TryInteract(ItemDefinition heldItem)
    {
        if (requiredItem != null)
        {
            if (heldItem != requiredItem)
            {
                Debug.Log($"[Interaction] Blocked! Requires {requiredItem.name}");
                return false; 
            }
        }

        // Trigger Minigame if assigned
        if (linkedMinigame != null)
        {
            linkedMinigame.StartMinigame();
            return true;
        }

        // Otherwise trigger standard event
        Debug.Log($"[Interaction] Triggered on {gameObject.name}");
        OnInteract.Invoke();
        return true;
    }
}