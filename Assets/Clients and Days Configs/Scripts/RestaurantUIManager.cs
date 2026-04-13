using UnityEngine;
using TMPro;
using System.Collections;

public class RestaurantUIManager : MonoBehaviour
{
    public static RestaurantUIManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI shiftTimeText; // <--- NEW: Link this to your new Timer Text

    [Header("Settings")]
    public float dialogueDisplayTime = 5f;

    private Coroutine dialogueCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        dialogueText.text = ""; 
        if (shiftTimeText) shiftTimeText.text = "00:00";
    }

    public void UpdateScore(int newScore) { if (scoreText != null) scoreText.text = $"{newScore}"; }
    public void UpdateMoney(int newMoney) { if (moneyText != null) moneyText.text = $"${newMoney}"; }

    // --- NEW: Format Timer Display ---
    public void UpdateShiftTimer(float currentTimer, float maxTimer)
    {
        if (shiftTimeText == null) return;
        
        float remaining = Mathf.Max(0, maxTimer - currentTimer);
        int minutes = Mathf.FloorToInt(remaining / 60);
        int seconds = Mathf.FloorToInt(remaining % 60);
        
        shiftTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        // Turn red in the last 30 seconds
        if (remaining <= 30f) shiftTimeText.color = Color.red;
        else shiftTimeText.color = Color.white;
    }

    public void ShowDialogue(string characterName, string text)
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        dialogueCoroutine = StartCoroutine(DialogueRoutine(characterName, text));
    }

    private IEnumerator DialogueRoutine(string characterName, string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = $"<b>{characterName}:</b> \"{text}\"";
            yield return new WaitForSeconds(dialogueDisplayTime);
            dialogueText.text = "";
        }
    }
}