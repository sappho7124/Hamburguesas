using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("Chalkboard Buttons")]
    public ChalkboardButton continueButton;
    public ChalkboardButton newGameButton;
    public ChalkboardButton settingsButton;
    public ChalkboardButton creditsButton;
    public ChalkboardButton exitButton;

    [Header("2D Overlay Panels (Screen Space)")]
    public RectTransform settingsPanel;
    public RectTransform creditsPanel;
    public Button closeBlockerButton; // The invisible background button to close panels

    [Header("Transition Settings")]
    public CanvasGroup blackoutScreen;
    public float transitionDuration = 1.5f;
    public float slideDuration = 0.4f;
    public string gameplaySceneName = "GameplayScene";
    public string finalDaySceneName = "FinalDayScene"; // <-- The scene for Day 8
    public int finalDayNumber = 8; // <-- Which day triggers the different scene

    private Vector2 offScreenPosition = new Vector2(0, -1500f); // Below screen
    private Vector2 onScreenPosition = Vector2.zero; // Center screen
    
    private Coroutine activeSlide;

    void Start()
    {
        // 1. Setup Overlay Panels
        settingsPanel.anchoredPosition = offScreenPosition;
        creditsPanel.anchoredPosition = offScreenPosition;
        
        // 2. Setup Blocker Button (Clicking outside)
        closeBlockerButton.gameObject.SetActive(false);
        closeBlockerButton.onClick.AddListener(CloseAllPanels);

        // 3. Blackout setup
        blackoutScreen.alpha = 0;
        blackoutScreen.blocksRaycasts = false;

        // 4. Bind Chalkboard Buttons
        continueButton.onClick.AddListener(() => LoadGameSequence(false));
        newGameButton.onClick.AddListener(() => LoadGameSequence(true));
        settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
        creditsButton.onClick.AddListener(() => OpenPanel(creditsPanel));
        exitButton.onClick.AddListener(OnExitClicked);

        // 5. Check Save
        if (SaveManager.Instance.HasSave())
            continueButton.gameObject.SetActive(true);
        else
            continueButton.gameObject.SetActive(false);
    }

    // --- GAME LOADING & TRANSITIONS ---
    private void LoadGameSequence(bool isNewGame)
    {
        if (isNewGame) SaveManager.Instance.CreateNewSave();
        else SaveManager.Instance.LoadGame();

        StartCoroutine(FadeOutAndLoad());
    }

    private IEnumerator FadeOutAndLoad()
    {
        blackoutScreen.blocksRaycasts = true; 
        
        float timer = 0f;
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            blackoutScreen.alpha = Mathf.Lerp(0f, 1f, timer / transitionDuration);
            yield return null;
        }

        // --- NEW ROUTING LOGIC ---
        int currentDay = SaveManager.Instance.CurrentSave.currentDay;
        
        if (currentDay >= finalDayNumber)
        {
            SceneManager.LoadScene(finalDaySceneName);
        }
        else
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    // --- OVERLAY PANEL SLIDING ---
    private void OpenPanel(RectTransform panelToOpen)
    {
        closeBlockerButton.gameObject.SetActive(true); // Enable background clicking
        if (activeSlide != null) StopCoroutine(activeSlide);
        activeSlide = StartCoroutine(SlidePanel(panelToOpen, onScreenPosition));
    }

    private void CloseAllPanels()
    {
        closeBlockerButton.gameObject.SetActive(false);
        if (activeSlide != null) StopCoroutine(activeSlide);
        
        // Slide both down just to be safe
        StartCoroutine(SlidePanel(settingsPanel, offScreenPosition));
        StartCoroutine(SlidePanel(creditsPanel, offScreenPosition));
    }

    private IEnumerator SlidePanel(RectTransform panel, Vector2 targetPosition)
    {
        Vector2 startPos = panel.anchoredPosition;
        float timer = 0f;

        while (timer < slideDuration)
        {
            timer += Time.deltaTime;
            // SmoothStep makes the slide look much more natural than a linear Lerp
            float t = Mathf.SmoothStep(0f, 1f, timer / slideDuration);
            panel.anchoredPosition = Vector2.Lerp(startPos, targetPosition, t);
            yield return null;
        }
        panel.anchoredPosition = targetPosition;
    }

    private void OnExitClicked()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }
}