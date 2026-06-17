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
    public float tickInterval = 1.0f;
    public float minTickAngle = 5f;
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

    [Header("Narrator Settings")]
    [Tooltip("A full screen black Image with 0 alpha (set raycast target to false)")]
    public UnityEngine.UI.Image narratorDarknessOverlay;
    public float darknessFadeSpeed = 3f;

    // Internal State
    private Coroutine dialogueCoroutine;
    private bool isTyping = false;
    private bool isSliding = false; 
    private string currentFullText = "";
    private EmotionSet currentEmotionSet;
    
    // Ticking State
    private RectTransform clockRect;
    private float tickTimer = 0f;
    private int currentTickSide = 1; 
    
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

        if (narratorDarknessOverlay) narratorDarknessOverlay.color = new Color(0, 0, 0, 0);
    }

    void Update()
    {
        if ((isTyping || isSliding) && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SkipAnimation();
        }
    }

    public void ShowDialogue(string characterName, string text, CustomerFaceController.Mood mood = CustomerFaceController.Mood.Neutral, CustomerFaceController speakerFace = null, Action onComplete = null)
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        
        if (optionsContainer) optionsContainer.SetActive(false);
        if (optionButtons != null) foreach (var btn in optionButtons) if (btn != null) btn.gameObject.SetActive(false);
        
        dialogueCoroutine = StartCoroutine(TypewriterRoutine(characterName, text, mood, speakerFace, onComplete));
    }

    private void SkipAnimation()
    {
        isSliding = false; 
        isTyping = false; 
    }

    private IEnumerator TypewriterRoutine(string characterName, string text, CustomerFaceController.Mood mood, CustomerFaceController speakerFace, Action onComplete)
    {
        isSliding = true;
        isTyping = true;
        currentFullText = text;
        dialogueText.text = $"";

        // --- NARRATOR DARKNESS ---
        bool isNarrator = characterName.ToLower().Contains("narrador");
        if (narratorDarknessOverlay != null)
        {
            float targetAlpha = isNarrator ? 0.85f : 0f;
            StartCoroutine(FadeDarkness(targetAlpha));
        }

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
                // FIX: Usar unscaledDeltaTime para que se mueva aunque el juego esté en pausa (Time.timeScale = 0)
                float newY = Mathf.Lerp(dialoguePanelRect.anchoredPosition.y, visibleY, Time.unscaledDeltaTime * slideSpeed);
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
            
            // FIX: Usar WaitForSecondsRealtime para que el texto corra aunque el juego esté en pausa
            if (c == '.' || c == '!' || c == '?') 
                yield return new WaitForSecondsRealtime(typingSpeed * 4f);
            else 
                yield return new WaitForSecondsRealtime(typingSpeed);
        }

        isTyping = false;
        dialogueText.text = $"{currentFullText}";
        
        if (currentEmotionSet != null && dialogueFaceImage != null) dialogueFaceImage.sprite = currentEmotionSet.closedMouth;
        if (speakerFace != null) speakerFace.SetTalking(false);

        // Esperar a que el jugador presione Click Izquierdo para avanzar
        yield return null; 
        yield return new WaitUntil(() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        // Avisarle a Yarn Spinner que ya puede continuar a la siguiente línea
        onComplete?.Invoke();
    }

        private IEnumerator FadeDarkness(float targetAlpha)
    {
        if (narratorDarknessOverlay == null) yield break;
        
        Color c = narratorDarknessOverlay.color;
        while (Mathf.Abs(c.a - targetAlpha) > 0.01f)
        {
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * darknessFadeSpeed);
            narratorDarknessOverlay.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        narratorDarknessOverlay.color = c;
    }

    public void HideDialoguePanel()
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        
        // Fade darkness away when dialogue ends
        if (narratorDarknessOverlay != null) StartCoroutine(FadeDarkness(0f));
        
        StartCoroutine(SlidePanelAway());
    }

    private IEnumerator SlidePanelAway()
    {
        if (dialoguePanelRect != null)
        {
            while (Mathf.Abs(dialoguePanelRect.anchoredPosition.y - hiddenY) > 1f)
            {
                // FIX: Usar unscaledDeltaTime aquí también
                float newY = Mathf.Lerp(dialoguePanelRect.anchoredPosition.y, hiddenY, Time.unscaledDeltaTime * slideSpeed);
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
                
                // --- STRIP TOMAS PREFIX ---
                string cleanText = options[i];
                if (cleanText.StartsWith("Tomas: ")) cleanText = cleanText.Substring(7);
                if (cleanText.StartsWith("Tomás: ")) cleanText = cleanText.Substring(7); // Handle accent just in case
                
                if (btnText != null) btnText.text = cleanText;

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
        if (shiftTimeText != null)
        {
            float remaining = Mathf.Max(0, maxTimer - currentTimer);
            int minutes = Mathf.FloorToInt(remaining / 60);
            int seconds = Mathf.FloorToInt(remaining % 60);
            shiftTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            shiftTimeText.color = remaining <= 30f ? Color.red : Color.white;
        }

        if (clockImage != null && clockSprites != null && clockSprites.Length > 0 && maxTimer > 0)
        {
            float progress = Mathf.Clamp01(currentTimer / maxTimer);
            int targetIndex = Mathf.FloorToInt(progress * (clockSprites.Length - 1));
            clockImage.sprite = clockSprites[targetIndex];
            
            if (clockRect != null)
            {
                if (isPaused)
                {
                    clockRect.localRotation = Quaternion.identity; 
                    tickTimer = tickInterval; 
                }
                else
                {
                    tickTimer += Time.deltaTime;
                    if (tickTimer >= tickInterval)
                    {
                        tickTimer -= tickInterval; 
                        currentTickSide = -currentTickSide; 
                        float randomAngle = UnityEngine.Random.Range(minTickAngle, maxTickAngle);
                        clockRect.localRotation = Quaternion.Euler(0, 0, randomAngle * currentTickSide);
                    }
                }
            }
        }

        if (pauseIconOverlay != null)
        {
            pauseIconOverlay.gameObject.SetActive(isPaused);
        }
    }
}