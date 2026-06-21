using UnityEngine;
using Yarn.Unity;
using System.Collections;

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
    public Transform donJulioExitPoint; 
    private GameObject activeDonJulio;

    [Header("Lucas Event (Day 1)")]
    public GameObject lucasPrefab;
    public Transform lucasSpawnPoint;
    private GameObject activeLucas;
    
    public GameObject vegetablesBagPrefab; 
    public Transform vegetableDropPoint;

    void Awake()
    {
        if (Instance == null) Instance = this;
        dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    void Start()
    {
        int currentDay = SaveManager.Instance.HasSave() ? SaveManager.Instance.CurrentSave.currentDay : 1;
        if (overrideSaveDay) currentDay = debugForceDay;
        
        if (currentDay == 1)
        {
            if (donJulioPrefab && donJulioSpawnPoint)
            {
                activeDonJulio = Instantiate(donJulioPrefab, donJulioSpawnPoint.position, donJulioSpawnPoint.rotation);
                
                activeDonJulio.name = "Don Julio"; 
                
                Animator anim = activeDonJulio.GetComponent<Animator>();
                if (anim != null) 
                { 
                    anim.SetBool("Sitting", false); 
                    anim.SetBool("Walking", false); 
                }

                if (Camera.main != null)
                {
                    Vector3 lookTarget = new Vector3(Camera.main.transform.position.x, activeDonJulio.transform.position.y, Camera.main.transform.position.z);
                    activeDonJulio.transform.LookAt(lookTarget);
                }
            }

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
        if (activeDonJulio != null)
        {
            if (donJulioExitPoint != null)
            {
                UnityEngine.AI.NavMeshAgent agent = activeDonJulio.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = true; 
                    agent.SetDestination(donJulioExitPoint.position);
                }
                
                Animator anim = activeDonJulio.GetComponent<Animator>();
                if (anim != null) 
                { 
                    anim.SetBool("Walking", true); 
                    anim.SetBool("Sitting", false); 
                }
                
                // FIX: Instead of a hard 5-second limit, wait until he actually arrives!
                StartCoroutine(WaitUntilArrivedThenDestroy(activeDonJulio));
            }
            else
            {
                Debug.LogWarning("[StoryFlowManager] donJulioExitPoint is not assigned! Destroying him instantly.");
                Destroy(activeDonJulio);
            }
            
            // Unassign him so we don't accidentally run this multiple times
            activeDonJulio = null;
        }
    }

    // NEW: Coroutine to track Don Julio's walk so he doesn't despawn early
    private IEnumerator WaitUntilArrivedThenDestroy(GameObject npc)
    {
        UnityEngine.AI.NavMeshAgent agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
        float maxWaitTime = 25f; // Failsafe: if he gets stuck on a wall, destroy him after 25s anyway

        while (npc != null && maxWaitTime > 0f)
        {
            maxWaitTime -= Time.deltaTime;
            
            // Check if he reached the end of his path
            if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                break;
            }
            
            yield return null;
        }

        if (npc != null)
        {
            Destroy(npc);
        }
    }

    public void ReportAction(string actionName)
    {
        if (!isTutorialActive && actionName == "RingBell")
        {
            int currentDay = overrideSaveDay ? debugForceDay : SaveManager.Instance.CurrentSave.currentDay;
            if (currentDay == 1 && !hasLucasAppeared)
            {
                hasLucasAppeared = true; 
                
                if (lucasPrefab && lucasSpawnPoint)
                {
                    activeLucas = Instantiate(lucasPrefab, lucasSpawnPoint.position, lucasSpawnPoint.rotation);
                    activeLucas.name = "Lucas"; 
                }
                dialogueRunner.StartDialogue("LucasBringsVegetables");
            }
            return;
        }

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
            isTutorialActive = false; 
        }
    }

    private void AdvanceTutorial(string yarnNode, int nextStep)
    {
        currentTutorialStep = nextStep;

        // FIX: If Yarn Spinner is currently typing or waiting for input on the old instruction...
        if (dialogueRunner.IsDialogueRunning) 
        {
            dialogueRunner.Stop();
            
            // We MUST yield 1 frame before starting the new dialogue so Yarn can clear its internal state!
            StartCoroutine(RestartDialogueNextFrame(yarnNode));
        }
        else
        {
            dialogueRunner.StartDialogue(yarnNode);
        }
    }

    // NEW: Safely handles the frame gap required by Yarn Spinner when interrupting dialogue
    private IEnumerator RestartDialogueNextFrame(string yarnNode)
    {
        yield return null; 
        dialogueRunner.StartDialogue(yarnNode);
    }

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