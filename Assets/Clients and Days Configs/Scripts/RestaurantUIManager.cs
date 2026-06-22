using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;

public class RestaurantUIManager : MonoBehaviour
{
    public static RestaurantUIManager Instance;

    [System.Serializable]
    public class SpecialCharacterFace
    {
        public string characterName;
        public CharacterFaceSet faceSet;
    }

    [System.Serializable]
    public class StatusEffectUI
    {
        public string effectName;
        [Tooltip("Add a UI Image and drop it here")]
        public UnityEngine.UI.Image image; 
    }

    [Header("Special Characters & Status Effects")]
    public List<SpecialCharacterFace> specialFaces;
    public List<StatusEffectUI> statusEffects;
    public float statusFadeSpeed = 3f;

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

    [Header("Dialogue Options")]
    public GameObject optionsContainer;
    public UnityEngine.UI.Button[] optionButtons;

    [Header("Narrator Settings")]
    public UnityEngine.UI.Image narratorDarknessOverlay;
    public float darknessFadeSpeed = 3f;

    // Internal State
    private Coroutine dialogueCoroutine;
    private Coroutine darknessCoroutine; // Track darkness separately
    private bool isTyping = false;
    private bool isSliding = false; 
    private string currentFullText = "";
    private EmotionSet currentEmotionSet;
    private RectTransform clockRect;
    private float tickTimer = 0f;
    private int currentTickSide = 1; 

    // UI Input State
    private Player_Controls controls;
    private InputAction uiUpAction;
    private InputAction uiDownAction;
    private InputAction uiSelectAction;
    private InputAction uiFastForwardAction;
    private int selectedOptionIndex = 0;
    private int activeOptionsCount = 0;
    private bool isOptionsActive = false;
    
    public bool IsDialogueActive => isTyping || isSliding || isOptionsActive;

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
        if (dialoguePanelRect) dialoguePanelRect.anchoredPosition = new Vector2(dialoguePanelRect.anchoredPosition.x, hiddenY);

        // INITIAL STATE: Starts fully black. 
        if (narratorDarknessOverlay) narratorDarknessOverlay.color = new Color(0, 0, 0, 1f);

        controls = new Player_Controls();
        uiUpAction = controls.asset.FindAction("UI/UI up");
        uiDownAction = controls.asset.FindAction("UI/UI down");
        uiSelectAction = controls.asset.FindAction("UI/Select");
        uiFastForwardAction = controls.asset.FindAction("UI/Fast Forward");
    }

    void OnEnable() { if (controls != null) controls.Enable(); }
    void OnDisable() { if (controls != null) controls.Disable(); }

    void Start()
    {
        int currentDay = SaveManager.Instance.HasSave() ? SaveManager.Instance.CurrentSave.currentDay : 1;
        if (StoryFlowManager.Instance != null && StoryFlowManager.Instance.overrideSaveDay) 
            currentDay = StoryFlowManager.Instance.debugForceDay;

        // If it's NOT Day 1, we fade the initial black screen away automatically.
        // If it IS Day 1, we leave it black so the Yarn script can control it!
        if (currentDay != 1)
        {
            SetDarkness(0f);
        }
    }

    void Update()
    {
        bool advancePressed = (uiSelectAction != null && uiSelectAction.WasPressedThisFrame()) ||
                              (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

        if ((isTyping || isSliding) && advancePressed)
        {
            SkipAnimation();
        }

        if (isOptionsActive && activeOptionsCount > 0)
        {
            if (uiUpAction != null && uiUpAction.WasPressedThisFrame()) 
            {
                selectedOptionIndex--;
                if (selectedOptionIndex < 0) selectedOptionIndex = activeOptionsCount - 1;
                HighlightOption(selectedOptionIndex);
            }
            if (uiDownAction != null && uiDownAction.WasPressedThisFrame()) 
            {
                selectedOptionIndex++;
                if (selectedOptionIndex >= activeOptionsCount) selectedOptionIndex = 0;
                HighlightOption(selectedOptionIndex);
            }
            if (uiSelectAction != null && uiSelectAction.WasPressedThisFrame()) 
            {
                optionButtons[selectedOptionIndex].onClick.Invoke();
            }
        }
    }

    // NEW: Public interface for Yarn to trigger the darkness
    public void SetDarkness(float targetAlpha)
    {
        if (darknessCoroutine != null) StopCoroutine(darknessCoroutine);
        if (gameObject.activeInHierarchy)
        {
            darknessCoroutine = StartCoroutine(FadeDarknessRoutine(targetAlpha));
        }
    }

    private IEnumerator FadeDarknessRoutine(float targetAlpha)
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

    public void ToggleStatusEffectUI(string effectName, bool active)
    {
        foreach (var effect in statusEffects)
        {
            if (effect.effectName == effectName && effect.image != null)
            {
                StartCoroutine(FadeStatusUI(effect.image, active ? 1f : 0f));
            }
        }
    }

    private IEnumerator FadeStatusUI(UnityEngine.UI.Image img, float targetAlpha)
    {
        if (targetAlpha > 0) img.gameObject.SetActive(true);

        Color c = img.color;
        while (Mathf.Abs(c.a - targetAlpha) > 0.01f)
        {
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * statusFadeSpeed);
            img.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        img.color = c;

        if (targetAlpha <= 0) img.gameObject.SetActive(false);
    }

    public void ShowDialogue(string characterName, string text, CustomerFaceController.Mood mood = CustomerFaceController.Mood.Neutral, CustomerFaceController speakerFace = null, bool isGameplayNode = false, Action onComplete = null)
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        
        if (optionsContainer) optionsContainer.SetActive(false);
        if (optionButtons != null) foreach (var btn in optionButtons) if (btn != null) btn.gameObject.SetActive(false);
        
        dialogueCoroutine = StartCoroutine(TypewriterRoutine(characterName, text, mood, speakerFace, isGameplayNode, onComplete));
    }

    private void SkipAnimation()
    {
        isSliding = false; 
        isTyping = false; 
    }

    private IEnumerator TypewriterRoutine(string characterName, string text, CustomerFaceController.Mood mood, CustomerFaceController speakerFace, bool isGameplayNode, Action onComplete)
    {
        isSliding = true;
        isTyping = true;
        currentFullText = text;
        dialogueText.text = "";
        ActionPromptManager.Instance.ShowPrompt("DialogueAdvance", "Normal", "Interact", "Avanzar Diálogo");
        
        currentEmotionSet = null;
        if (dialogueFaceImage != null)
        {
            CharacterFaceSet faceSet = null;
            
            if (!string.IsNullOrEmpty(characterName))
            {
                if (CustomerSpawner.Instance != null) faceSet = CustomerSpawner.Instance.GetCustomerFaceSet(characterName);
                
                if (faceSet == null && specialFaces != null)
                {
                    var special = specialFaces.Find(x => x.characterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));
                    if (special != null) faceSet = special.faceSet;
                }
            }

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

            if (uiFastForwardAction != null && uiFastForwardAction.IsPressed())
            {
                dialogueText.text = currentFullText;
                break;
            }

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
            if (c == '.' || c == '!' || c == '?') 
                yield return new WaitForSecondsRealtime(typingSpeed * 4f);
            else 
                yield return new WaitForSecondsRealtime(typingSpeed);
        }

        isTyping = false;
        dialogueText.text = $"{currentFullText}";
            
        if (currentEmotionSet != null && dialogueFaceImage != null) dialogueFaceImage.sprite = currentEmotionSet.closedMouth;
        if (speakerFace != null) speakerFace.SetTalking(false);

        ActionPromptManager.Instance.ShowPrompt("DialogueAdvance", "Normal", "Interact", "Avanzar Diálogo");

        yield return null; 

        bool waitToAdvance = true;
        while (waitToAdvance)
        {
            if (uiFastForwardAction != null && uiFastForwardAction.IsPressed())
            {
                yield return new WaitForSecondsRealtime(0.05f);
                waitToAdvance = false;
            }
            else if (uiSelectAction != null && uiSelectAction.WasPressedThisFrame()) waitToAdvance = false;
            else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) waitToAdvance = false;
            else if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) waitToAdvance = false;

            yield return null;
        }

        ActionPromptManager.Instance.HidePrompt("DialogueAdvance", true);
        onComplete?.Invoke();
    }

    public void HideDialoguePanel()
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        
        // WE NO LONGER AUTO-CLEAR DARKNESS HERE. YARN MANAGES IT!
        
        StartCoroutine(SlidePanelAway());
    }

    private IEnumerator SlidePanelAway()
    {
        if (dialoguePanelRect != null)
        {
            while (Mathf.Abs(dialoguePanelRect.anchoredPosition.y - hiddenY) > 1f)
            {
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

        isOptionsActive = true;
        ActionPromptManager.Instance.HidePrompt("DialogueAdvance", true);
        activeOptionsCount = options.Count;
        selectedOptionIndex = 0; 

        ActionPromptManager.Instance.ShowPrompt("UI_Up", "UI", "UI up", "Subir");
        ActionPromptManager.Instance.ShowPrompt("UI_Down", "UI", "UI down", "Bajar");
        ActionPromptManager.Instance.ShowPrompt("UI_Select", "UI", "Select", "Seleccionar");

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue; 

            if (options != null && i < options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                
                string cleanText = options[i];
                if (cleanText.StartsWith("Tomas: ")) cleanText = cleanText.Substring(7);
                if (cleanText.StartsWith("Tomás: ")) cleanText = cleanText.Substring(7);
                if (btnText != null) btnText.text = cleanText;

                int capturedIndex = i; 
                
                UIButtonHoverScale hoverComponent = optionButtons[i].GetComponent<UIButtonHoverScale>();
                if (hoverComponent != null)
                {
                    hoverComponent.OnHovered = (btn) => {
                        selectedOptionIndex = capturedIndex;
                        HighlightOption(selectedOptionIndex); 
                    };
                }

                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => 
                {
                    isOptionsActive = false;
                    
                    ActionPromptManager.Instance.HidePrompt("UI_Up", true);
                    ActionPromptManager.Instance.HidePrompt("UI_Down", true);
                    ActionPromptManager.Instance.HidePrompt("UI_Select", true);

                    if (optionsContainer) optionsContainer.SetActive(false);
                    foreach (var btn in optionButtons) { if (btn != null) btn.gameObject.SetActive(false); }
                    onOptionSelected?.Invoke(capturedIndex);
                });
            }
            else optionButtons[i].gameObject.SetActive(false);
        }

        HighlightOption(0);
    }

    private void HighlightOption(int index)
    {
        for (int i = 0; i < activeOptionsCount; i++)
        {
            UIButtonHoverScale btnScale = optionButtons[i].GetComponent<UIButtonHoverScale>();
            if (btnScale != null)
            {
                btnScale.isSelected = (i == index);
            }
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

        if (pauseIconOverlay != null) pauseIconOverlay.gameObject.SetActive(isPaused);
    }
}