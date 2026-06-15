using UnityEngine;

public class ThoughtTrigger : MonoBehaviour
{
    public string protagonistName = "Yo";
    [TextArea] public string thoughtText = "Necesito limpiar esto...";

    // Hook this to the Fridge's InteractableObject -> OnInteract event!
    public void TriggerThought()
    {
        // Because "Yo" is not a customer, the UI will hide the face box automatically.
        RestaurantUIManager.Instance.ShowDialogue(protagonistName, thoughtText);
    }
}