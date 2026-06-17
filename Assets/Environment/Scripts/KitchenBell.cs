using UnityEngine;

public class KitchenBell : MonoBehaviour
{
    public void RingBell()
    {
        int day = StoryFlowManager.Instance.overrideSaveDay ? StoryFlowManager.Instance.debugForceDay : SaveManager.Instance.CurrentSave.currentDay;
        
        // 1. Check if we need to trigger Lucas (Day 1, and he hasn't appeared yet)
        if (day == 1 && !StoryFlowManager.Instance.hasLucasAppeared)
        {
            StoryFlowManager.Instance.ReportAction("RingBell");
            return; // Stop here! The shift doesn't start yet.
        }

        // 2. Normal Shift Start (Happens on Day 2+, or Day 1 after Lucas leaves)
        if (CustomerSpawner.Instance != null)
        {
            CustomerSpawner.Instance.StartShift(day);
        }
    }
}