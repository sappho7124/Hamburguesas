// Location: C:\Games\Unity\Hamburguesas\Assets\Clients and Days Configs\Scripts\RestaurantUIManager.cs
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

    [Header("Sprite Clock Settings")]
    public UnityEngine.UI.Image clockImage;
    public Sprite[] clockSprites;
    public UnityEngine.UI.Image pauseIconOverlay;
    
    [Header("Clock Animation")]
    [Tooltip("How many seconds between each 'tick'.")]
    public float tickInterval = 1.0f;
    [Tooltip("Minimum angle the clock will snap to when ticking.")]
    public float minTickAngle = 5f;
    [Tooltip("Maximum angle the clock will snap to when ticking.")]
    public float maxTickAngle = 15f;

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
    public GameObject optionsContainer;
    public UnityEngine.UI.Button[] optionButtons;

    // Internal State
    private Coroutine dialogueCoroutine;
    private bool isTyping = false;
    private bool isSliding = false; 
    private string currentFullText = "";
    private EmotionSet currentEmotionSet;
    
    // Ticking State
    private RectTransform clockRect;
    private float tickTimer = 0f;
    private int currentTickSide = 1; // 1 = Right, -1 = Left
    
    public bool IsDialogueActive => isTyping || isSliding;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        dialogueText.text = ""; 
        if (dialogueFaceImage) dialogueFaceImage.gameObject.SetActive(false);
        if (shiftTimeText) shiftTimeText.text = "00:00";
        if (pauseIconOverlay) pauseIconOverlay.gameObject.SetActive(false);

        if (clockImage != null) clockRect = clockImage.rectTransform;

        if (optionsContainer) optionsContainer.SetActive(false);
        if (optionButtons != null) foreach (var btn in optionButtons) if (btn != null) btn.gameObject.SetActive(false);

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

    public void ShowDialogue(string characterName, string text, CustomerFaceController.Mood mood = CustomerFaceController.Mood.Neutral, CustomerFaceController speakerFace = null)
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        
        if (optionsContainer) optionsContainer.SetActive(false);
        if (optionButtons != null) foreach (var btn in optionButtons) if (btn != null) btn.gameObject.SetActive(false);
        
        dialogueCoroutine = StartCoroutine(TypewriterRoutine(characterName, text, mood, speakerFace));
    }

    private void SkipAnimation()
    {
        isSliding = false; 
        isTyping = false; 
    }

    private IEnumerator TypewriterRoutine(string characterName, string text, CustomerFaceController.Mood mood, CustomerFaceController speakerFace)
    {
        isSliding = true;
        isTyping = true;
        currentFullText = text;
        dialogueText.text = $"";

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

            if (currentEmotionSet != null || speakerFace != null)
            {
                mouthTimer += typingSpeed;
                if (mouthTimer >= mouthAnimationSpeed)
                {
                    isMouthOpen = !isMouthOpen;
                    if (currentEmotionSet != null) dialogueFaceImage.sprite = isMouthOpen ? currentEmotionSet.openMouth : currentEmotionSet.closedMouth;
                    
                    if (speakerFace != null) speakerFace.SetTalking(isMouthOpen);

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
        if (speakerFace != null) speakerFace.SetTalking(false);

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
        if (optionButtons == null || optionButtons.Length == 0) return;

        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        if (dialoguePanelRect) dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, visibleY);

        if (optionsContainer) optionsContainer.SetActive(true);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue; 

            if (options != null && i < options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = options[i];

                int capturedIndex = i; 
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => 
                {
                    if (optionsContainer) optionsContainer.SetActive(false);
                    foreach (var btn in optionButtons) { if (btn != null) btn.gameObject.SetActive(false); }
                    onOptionSelected?.Invoke(capturedIndex);
                });
            }
            else optionButtons[i].gameObject.SetActive(false);
        }
    }

    public void UpdateScore(int newScore) { if (scoreText != null) scoreText.text = $"{newScore}"; }
    public void UpdateMoney(int newMoney) { if (moneyText != null) moneyText.text = $"{newMoney}"; }
    
    public void UpdateShiftTimer(float currentTimer, float maxTimer, bool isPaused)
    {
        // 1. Text Update (Optional / Debug)
        if (shiftTimeText != null)
        {
            float remaining = Mathf.Max(0, maxTimer - currentTimer);
            int minutes = Mathf.FloorToInt(remaining / 60);
            int seconds = Mathf.FloorToInt(remaining % 60);
            shiftTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            shiftTimeText.color = remaining <= 30f ? Color.red : Color.white;
        }

        // 2. Sprite Sheet Progress Update
        if (clockImage != null && clockSprites != null && clockSprites.Length > 0 && maxTimer > 0)
        {
            float progress = Mathf.Clamp01(currentTimer / maxTimer);
            int targetIndex = Mathf.FloorToInt(progress * (clockSprites.Length - 1));
            clockImage.sprite = clockSprites[targetIndex];
            
            // --- NEW: Ticking Animation ---
            if (clockRect != null)
            {
                if (isPaused)
                {
                    // Snap back to normal immediately when paused
                    clockRect.localRotation = Quaternion.identity; 
                    tickTimer = tickInterval; // Pre-load the timer so it ticks immediately when unpaused
                }
                else
                {
                    tickTimer += Time.deltaTime;
                    if (tickTimer >= tickInterval)
                    {
                        tickTimer -= tickInterval; 
                        currentTickSide = -currentTickSide; // Swap sides (-1 to 1)
                        
                        // Generate a random angle between min and max
                        float randomAngle = UnityEngine.Random.Range(minTickAngle, maxTickAngle);
                        
                        // Apply rotation (Z-axis)
                        clockRect.localRotation = Quaternion.Euler(0, 0, randomAngle * currentTickSide);
                    }
                }
            }
        }

        // 3. Pause Icon Overlay Update
        if (pauseIconOverlay != null)
        {
            pauseIconOverlay.gameObject.SetActive(isPaused);
        }
    }
}