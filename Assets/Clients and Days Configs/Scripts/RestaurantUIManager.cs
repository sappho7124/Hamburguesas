using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;

public class RestaurantUIManager : MonoBehaviour
{
    public static RestaurantUIManager Instance;

    [Header("Top Bar UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI shiftTimeText; 

    [Header("Dialogue UI Settings")]
    public RectTransform dialoguePanelRect; 
    public TextMeshProUGUI dialogueText;
    public UnityEngine.UI.Image dialogueFaceImage; 
    public float hiddenY = -300f; 
    public float visibleY = 50f;  
    public float slideSpeed = 10f; 

    [Header("Dialogue Timing")]
    public float dialogueDisplayTime = 5f;
    public float typingSpeed = 0.03f; 
    public float mouthAnimationSpeed = 0.15f; 

    [Header("Dialogue Options (Pre-Setup)")]
    public GameObject optionsContainer; // Optional now!
    public UnityEngine.UI.Button[] optionButtons; // Array of exactly 5 buttons

    private Coroutine dialogueCoroutine;
    private bool isTyping = false;
    private bool isSliding = false; 
    private string currentFullText = "";
    private EmotionSet currentEmotionSet;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        dialogueText.text = ""; 
        if (dialogueFaceImage) dialogueFaceImage.gameObject.SetActive(false);
        if (shiftTimeText) shiftTimeText.text = "00:00";

        // Hide options container initially
        if (optionsContainer) optionsContainer.SetActive(false);
        
        // NEW: Also forcefully hide all individual buttons just in case there is no container
        if (optionButtons != null)
        {
            foreach (var btn in optionButtons)
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }
        }

        // Snap panel off-screen immediately
        if (dialoguePanelRect)
            dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, hiddenY);
    }

    void Update()
    {
        if ((isTyping || isSliding) && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SkipAnimation();
        }
    }

    public void ShowDialogue(string characterName, string text, CustomerFaceController.Mood mood = CustomerFaceController.Mood.Neutral)
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        
        // Hide options when normal dialogue plays
        if (optionsContainer) optionsContainer.SetActive(false);
        
        // NEW: Forcefully hide the buttons directly
        if (optionButtons != null)
        {
            foreach (var btn in optionButtons)
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }
        }
        
        dialogueCoroutine = StartCoroutine(TypewriterRoutine(characterName, text, mood));
    }

    private void SkipAnimation()
    {
        isSliding = false; 
        isTyping = false; 
    }

    private IEnumerator TypewriterRoutine(string characterName, string text, CustomerFaceController.Mood mood)
    {
        isSliding = true;
        isTyping = true;
        currentFullText = text;
        dialogueText.text = $"";

        // Face setup
        currentEmotionSet = null;
        if (CustomerSpawner.Instance != null && dialogueFaceImage != null)
        {
            CharacterFaceSet faceSet = CustomerSpawner.Instance.GetCustomerFaceSet(characterName);
            if (faceSet != null)
            {
                currentEmotionSet = faceSet.GetEmotion(mood);
                dialogueFaceImage.sprite = currentEmotionSet.closedMouth;
                dialogueFaceImage.gameObject.SetActive(true);
            }
            else dialogueFaceImage.gameObject.SetActive(false);
        }

        if (dialoguePanelRect != null)
        {
            while (isSliding && Mathf.Abs(dialoguePanelRect.anchoredPosition.y - visibleY) > 1f)
            {
                float newY = Mathf.Lerp(dialoguePanelRect.anchoredPosition.y, visibleY, Time.deltaTime * slideSpeed);
                dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, newY);
                yield return null;
            }
            
            dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, visibleY);
            isSliding = false;
        }

        float mouthTimer = 0f;
        bool isMouthOpen = false;

        for (int i = 0; i <= text.Length; i++)
        {
            if (!isTyping) break; 

            dialogueText.text = $"{text.Substring(0, i)}";

            if (currentEmotionSet != null)
            {
                mouthTimer += typingSpeed;
                if (mouthTimer >= mouthAnimationSpeed)
                {
                    isMouthOpen = !isMouthOpen;
                    dialogueFaceImage.sprite = isMouthOpen ? currentEmotionSet.openMouth : currentEmotionSet.closedMouth;
                    mouthTimer = 0f;
                }
            }

            char c = i > 0 ? text[i - 1] : ' ';
            if (c == '.' || c == '!' || c == '?') yield return new WaitForSeconds(typingSpeed * 4f);
            else yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        dialogueText.text = $"{currentFullText}";
        if (currentEmotionSet != null && dialogueFaceImage != null) dialogueFaceImage.sprite = currentEmotionSet.closedMouth;

        yield return new WaitForSeconds(dialogueDisplayTime);

        if (dialoguePanelRect != null)
        {
            while (Mathf.Abs(dialoguePanelRect.anchoredPosition.y - hiddenY) > 1f)
            {
                float newY = Mathf.Lerp(dialoguePanelRect.anchoredPosition.y, hiddenY, Time.deltaTime * slideSpeed);
                dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, newY);
                yield return null;
            }
            dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, hiddenY);
        }

        dialogueText.text = "";
        if (dialogueFaceImage != null) dialogueFaceImage.gameObject.SetActive(false);
    }

    public void DisplayDialogueOptions(List<string> options, Action<int> onOptionSelected)
    {
        // REMOVED: if (optionsContainer == null) return; (This caused the script to abort entirely without a container)
        if (optionButtons == null || optionButtons.Length == 0) return;

        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        if (dialoguePanelRect) dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, visibleY);

        // Turn on container if it exists
        if (optionsContainer) optionsContainer.SetActive(true);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue; // Safety check

            if (options != null && i < options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                
                TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = options[i];

                int capturedIndex = i; 
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => 
                {
                    // Hide UI upon clicking
                    if (optionsContainer) optionsContainer.SetActive(false);
                    foreach (var btn in optionButtons) { if (btn != null) btn.gameObject.SetActive(false); }

                    onOptionSelected?.Invoke(capturedIndex);
                });
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void UpdateScore(int newScore) { if (scoreText != null) scoreText.text = $"{newScore}"; }
    public void UpdateMoney(int newMoney) { if (moneyText != null) moneyText.text = $"{newMoney}"; }
    public void UpdateShiftTimer(float currentTimer, float maxTimer)
    {
        if (shiftTimeText == null) return;
        float remaining = Mathf.Max(0, maxTimer - currentTimer);
        int minutes = Mathf.FloorToInt(remaining / 60);
        int seconds = Mathf.FloorToInt(remaining % 60);
        shiftTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        shiftTimeText.color = remaining <= 30f ? Color.red : Color.white;
    }
}