// Location: C:\Games\Unity\Hamburguesas\Assets\Clients and Days Configs\Scripts\StoryFlowManager.cs
using UnityEngine;
using Yarn.Unity;

public class StoryFlowManager : MonoBehaviour
{
    public static StoryFlowManager Instance;
    private DialogueRunner dialogueRunner;

    [Header("Testing Override")]
    public bool overrideSaveDay = false;
    [Tooltip("If true, the game will ignore the save file and force this day number.")]
    public int debugForceDay = 1;

    [Header("Story State")]
    public bool isTutorialActive = false;
    private int currentTutorialStep = 0;
    [HideInInspector] public bool hasLucasAppeared = false;

    [Header("Don Julio Event (Day 0)")]
    public GameObject donJulioPrefab;
    public Transform donJulioSpawnPoint;
    public Transform donJulioExitPoint; // Where he walks when he leaves
    private GameObject activeDonJulio;

    [Header("Lucas Event (Day 1)")]
    public GameObject lucasPrefab;
    public Transform lucasSpawnPoint;
    private GameObject activeLucas;
    
    public GameObject vegetablesBagPrefab; // The bag he leaves behind
    public Transform vegetableDropPoint;

    void Awake()
    {
        if (Instance == null) Instance = this;
        dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    void Start()
    {
        // 1. Check for overrides
        int currentDay = SaveManager.Instance.HasSave() ? SaveManager.Instance.CurrentSave.currentDay : 1;
        if (overrideSaveDay) currentDay = debugForceDay;
        
        if (currentDay == 1)
        {
            // Spawn Don Julio FIRST
            if (donJulioPrefab && donJulioSpawnPoint)
            {
                activeDonJulio = Instantiate(donJulioPrefab, donJulioSpawnPoint.position, donJulioSpawnPoint.rotation);
            }

            // WAIT 5 SECONDS THEN START NARRATOR!
            Invoke("StartMondayIntro", 5f);
        }
    }

    private void StartMondayIntro()
    {
        dialogueRunner.StartDialogue("NarratorIntroduction");
        isTutorialActive = true;
        currentTutorialStep = 1;
    }

    public void DismissDonJulio()
    {
        if (activeDonJulio != null && donJulioExitPoint != null)
        {
            // Give him a NavMeshAgent so he can walk!
            UnityEngine.AI.NavMeshAgent agent = activeDonJulio.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.SetDestination(donJulioExitPoint.position);
            }
            // Destroy him after 5 seconds of walking
            Destroy(activeDonJulio, 5f);
        }
    }

    // Call this from other scripts when the player does something!
    public void ReportAction(string actionName)
    {
        // --- LUCAS / SHIFT START LOGIC ---
        if (!isTutorialActive && actionName == "RingBell")
        {
            int currentDay = overrideSaveDay ? debugForceDay : SaveManager.Instance.CurrentSave.currentDay;
            if (currentDay == 1 && !hasLucasAppeared)
            {
                hasLucasAppeared = true; 
                
                if (lucasPrefab && lucasSpawnPoint)
                {
                    activeLucas = Instantiate(lucasPrefab, lucasSpawnPoint.position, lucasSpawnPoint.rotation);
                }
                dialogueRunner.StartDialogue("LucasBringsVegetables");
            }
            return;
        }

        // --- TUTORIAL LOGIC ---
        if (!isTutorialActive) return;

        if (currentTutorialStep == 1 && actionName == "OpenFridge") AdvanceTutorial("TutorialStep_1_OpenFridge", 2);
        else if (currentTutorialStep == 2 && actionName == "GrabBread") AdvanceTutorial("TutorialStep_2_GrabBread", 3);
        else if (currentTutorialStep == 3 && actionName == "PlaceBread") AdvanceTutorial("TutorialStep_3_PlaceBread", 4);
        else if (currentTutorialStep == 4 && actionName == "RotateBread") AdvanceTutorial("TutorialStep_4_RotateBread", 5);
        else if (currentTutorialStep == 5 && actionName == "GrabMeat") AdvanceTutorial("TutorialStep_5_GrabMeat", 6);
        else if (currentTutorialStep == 6 && actionName == "CookMeat") AdvanceTutorial("TutorialStep_6_CookMeat", 7);
        else if (currentTutorialStep == 7 && actionName == "PlaceMeat") AdvanceTutorial("TutorialStep_7_PlaceMeat", 8);
        else if (currentTutorialStep == 8 && actionName == "BuildBurger") AdvanceTutorial("TutorialStep_8_BuildBurger", 9);
        else if (currentTutorialStep == 9 && actionName == "PlaceBurger") AdvanceTutorial("TutorialStep_9_PlaceBurger", 10);
        else if (currentTutorialStep == 10 && actionName == "ServeTable") 
        {
            AdvanceTutorial("TutorialStep_10_GoToTable", 11);
            isTutorialActive = false; // Tutorial over, wait for bell
        }
    }

    private void AdvanceTutorial(string yarnNode, int nextStep)
    {
        currentTutorialStep = nextStep;
        dialogueRunner.StartDialogue(yarnNode);
    }

    // Called via Yarn Command
    public void DismissLucasAndDropVegetables()
    {
        if (vegetablesBagPrefab && vegetableDropPoint)
        {
            Instantiate(vegetablesBagPrefab, vegetableDropPoint.position, vegetableDropPoint.rotation);
        }
        if (activeLucas != null)
        {
            Destroy(activeLucas);
        }
    }
}