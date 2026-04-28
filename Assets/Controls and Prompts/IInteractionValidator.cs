using UnityEngine;

public interface IInteractionValidator
{
    // Returns true if the interaction is allowed. If false, populates failReason (e.g., "Bag is empty!")
    bool IsInteractionValid(EquipmentController equipment, out string failReason);
}