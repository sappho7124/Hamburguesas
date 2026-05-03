using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class EndOfDayManager : MonoBehaviour
{
    public static EndOfDayManager Instance;

    [Header("UI Panels")]
    public GameObject endOfDayPanel; // The Chalkboard Summary Panel
    public GameObject gameplayUIPanel; // Disable this so the screen is clean

    [Header("Stat Texts (TextMeshPro)")]
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI dailyScoreText;
    public TextMeshProUGUI moneyEarnedText;
    public TextMeshProUGUI moneyLostText;
    public TextMeshProUGUI satisfiedText;
    public TextMeshProUGUI angryText;
    
    [Header("Buttons")]
    public UnityEngine.UI.Button nextDayButton;
    public UnityEngine.UI.Button mainMenuButton;

    void Awake()
    {
        if (Instance == null) Instance = this;
        endOfDayPanel.SetActive(false);

        nextDayButton.onClick.AddListener(StartNextDay);
        mainMenuButton.onClick.AddListener(ReturnToMenu);
    }

    public void ShowEndOfDaySummary()
    {
        // 1. Freeze time and swap UI
        Time.timeScale = 0f;
        if (gameplayUIPanel) gameplayUIPanel.SetActive(false);
        endOfDayPanel.SetActive(true);

        // 2. Grab daily stats from OrderManager
        int earned = OrderManager.Instance.dailyMoneyEarned;
        int lost = OrderManager.Instance.dailyMoneyLost;
        int score = OrderManager.Instance.dailyScore;
        int satisfied = OrderManager.Instance.dailySatisfiedCustomers;
        int angry = OrderManager.Instance.dailyAngryCustomers;

        // 3. Populate Chalkboard UI
        int currentDay = SaveManager.Instance.CurrentSave.currentDay;
        dayText.text = $"Fin del Día {currentDay}";
        dailyScoreText.text = $"Puntuación de Hoy: {score}";
        moneyEarnedText.text = $"Dinero Ganado: +${earned}";
        moneyLostText.text = $"Dinero Perdido: -${lost}";
        satisfiedText.text = $"Clientes Felices: {satisfied}";
        angryText.text = $"Clientes Enojados: {angry}";

        // 4. COMMIT TO SAVE DATA
        SaveManager.Instance.CurrentSave.totalScore += score;
        SaveManager.Instance.CurrentSave.moneyEarned += earned;
        SaveManager.Instance.CurrentSave.moneyLost += lost;
        
        // Advance to the next day (Capped at 8 based on your game design)
        if (SaveManager.Instance.CurrentSave.currentDay < 8)
        {
            SaveManager.Instance.CurrentSave.currentDay++;
        }
        else
        {
            // Optional: Game beaten logic goes here!
            nextDayButton.gameObject.SetActive(false); 
        }

        // 5. WRITE JSON TO DISK!
        SaveManager.Instance.SaveGame();
    }

    private void StartNextDay()
    {
        Time.timeScale = 1f;
        // Reload the scene to start the fresh day
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene"); // Make sure this matches your Main Menu scene name
    }
}