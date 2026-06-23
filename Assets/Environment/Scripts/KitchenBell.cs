using UnityEngine;
using Yarn.Unity; 

public class KitchenBell : MonoBehaviour
{
    [Header("UI & Feedback")]
    public GameObject exclamationMarkPrefab;
    public Vector3 exclamationOffset = new Vector3(0, 0.5f, 0);
    private GameObject activeExclamationMark;

    public void ShowExclamationMark()
    {
        if (exclamationMarkPrefab != null && activeExclamationMark == null)
        {
            activeExclamationMark = Instantiate(exclamationMarkPrefab, transform.position + exclamationOffset, Quaternion.identity, transform);
        }
    }

    public void HideExclamationMark()
    {
        if (activeExclamationMark != null) Destroy(activeExclamationMark);
    }

    public void RingBell()
    {
        HideExclamationMark(); // NEW: Hide it once clicked

        int day = StoryFlowManager.Instance.overrideSaveDay ? StoryFlowManager.Instance.debugForceDay : SaveManager.Instance.CurrentSave.currentDay;
        
        // 1. Day 1 Event Trigger
        if (day == 1 && !StoryFlowManager.Instance.hasLucasAppeared)
        {
            StoryFlowManager.Instance.hasLucasAppeared = true; 
            
            DialogueRunner runner = FindAnyObjectByType<DialogueRunner>();
            if (runner != null)
            {
                runner.StartDialogue("BellRingDay1");
            }
            return; // Stop here! The shift doesn't start yet.
        }

        // 2. Normal Shift Start (Happens on Day 2+, or Day 1 after Lucas leaves)
        if (CustomerSpawner.Instance != null)
        {
            CustomerSpawner.Instance.StartShift(day);
        }
    }
}