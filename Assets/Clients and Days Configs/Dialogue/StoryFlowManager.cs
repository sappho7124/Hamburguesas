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
    public GameObject activeLucas;
    public Transform lucasWaitPoint;
    
    public GameObject vegetablesBagPrefab; 
    public Transform vegetableDropPoint;

    [Header("Bell Reminder Event")]
    public float timeToWaitBeforeReminder = 20f;
    private float bellWaitTimer = 0f;
    private bool bellReminderPlayed = false;

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
            SpawnDonJulio(); 
            Invoke("StartMondayIntro", 5f);
        }
    }

    void Update()
    {
        if (!isTutorialActive && currentTutorialStep >= 11 && !hasLucasAppeared)
        {
            bellWaitTimer += Time.deltaTime;
            
            if (bellWaitTimer >= timeToWaitBeforeReminder && !bellReminderPlayed && !dialogueRunner.IsDialogueRunning)
            {
                bellReminderPlayed = true;
                dialogueRunner.StartDialogue("TutorialBellReminder");
            }
        }
    }

    public void SpawnDonJulio()
    {
        // 1. Destroy ANY existing Don Julios in the scene. 
        // We use FindObjectsByType because 'activeDonJulio' is set to null when he starts walking away!
        Customer[] existingCustomers = FindObjectsByType<Customer>(FindObjectsSortMode.None);
        foreach (Customer c in existingCustomers)
        {
            if (c.gameObject.name.Contains("Don Julio"))
            {
                Destroy(c.gameObject);
            }
        }
        
        activeDonJulio = null;

        // 2. Spawn a fresh Don Julio perfectly on the ground
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

            // 3. Force Yarn View to connect to THIS specific Don Julio's face and emotions
            // This stops Yarn from accidentally connecting to a ghost reference
            RestaurantYarnView yarnView = FindAnyObjectByType<RestaurantYarnView>();
            if (yarnView != null)
            {
                yarnView.currentSpeakerFace = activeDonJulio.GetComponentInChildren<CustomerFaceController>();
            }
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
                
                StartCoroutine(WaitUntilArrivedThenDestroy(activeDonJulio));
            }
            else
            {
                Debug.LogWarning("[StoryFlowManager] donJulioExitPoint is not assigned! Destroying him instantly.");
                Destroy(activeDonJulio);
            }
            
            // Unassign him so we don't accidentally run this multiple times
            // NOTE: If time resets while he's walking, our aggressive FindObjectsByType loop in SpawnDonJulio() will catch him anyway.
            activeDonJulio = null;
        }
    }

    private IEnumerator WaitUntilArrivedThenDestroy(GameObject npc)
    {
        yield return new WaitForSeconds(0.5f); 

        UnityEngine.AI.NavMeshAgent agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
        float maxWaitTime = 25f; // Failsafe

        // Added safe check in case SpawnDonJulio destroyed him mid-walk!
        while (npc != null && maxWaitTime > 0f)
        {
            maxWaitTime -= Time.deltaTime;
            
            if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                break;
            }
            
            yield return null;
        }

        if (npc != null) Destroy(npc);
    }

    public void ReportAction(string actionName)
    {
        if (!isTutorialActive) return;

        if (actionName == "ThrowSomething") { AdvanceTutorial("TutorialFuckUp_1_ThrowSomething", currentTutorialStep); return; }
        if (actionName == "DropSomething") { AdvanceTutorial("TutorialFuckUp_2_DropSomething", currentTutorialStep); return; }
        if (actionName == "BurnSomething") { AdvanceTutorial("TutorialFuckUp_3_BurnSomething", currentTutorialStep); return; }

        if (currentTutorialStep == 1 && actionName == "OpenFridge") AdvanceTutorial("TutorialStep_1_OpenFridge", 2);
        else if (currentTutorialStep == 2 && actionName == "GrabBread") AdvanceTutorial("TutorialStep_2_GrabBread", 3);
        else if (currentTutorialStep == 3 && (actionName == "PlaceBread" || actionName == "RotateBread")) AdvanceTutorial("TutorialStep_3_PlaceBread", 4);
        else if (currentTutorialStep == 4 && (actionName == "RotateBread" || actionName == "PlaceBread")) AdvanceTutorial("TutorialStep_4_RotateBread", 5);
        else if (currentTutorialStep == 5 && actionName == "GrabMeat") AdvanceTutorial("TutorialStep_5_GrabMeat", 6);
        else if (currentTutorialStep == 6 && actionName == "CookMeat") AdvanceTutorial("TutorialStep_6_CookMeat", 7);
        else if (currentTutorialStep == 7 && actionName == "PlaceMeat") AdvanceTutorial("TutorialStep_7_PlaceMeat", 8);
        else if (currentTutorialStep == 8 && actionName == "BuildBurger") AdvanceTutorial("TutorialStep_8_BuildBurger", 9);
        else if (currentTutorialStep == 9 && actionName == "PlaceBurger") AdvanceTutorial("TutorialStep_9_PlaceBurger", 10);
        else if (currentTutorialStep == 10 && actionName == "HoverTable") AdvanceTutorial("TutorialStep_10_GoToTable", 11);
        else if (currentTutorialStep == 11 && actionName == "ServeTable") 
        {
            AdvanceTutorial("TutorialEnd", 12);
            isTutorialActive = false; 
            KitchenBell bell = FindAnyObjectByType<KitchenBell>();
            if (bell != null) bell.ShowExclamationMark();
        }
    }

    public void SpawnLucas()
    {
        if (lucasPrefab != null && lucasSpawnPoint != null && lucasWaitPoint != null)
        {
            activeLucas = Instantiate(lucasPrefab, lucasSpawnPoint.position, lucasSpawnPoint.rotation);
            activeLucas.name = "Lucas"; 
                        
            Customer lucasCustomer = activeLucas.GetComponent<Customer>();
            if (lucasCustomer != null)
            {
                lucasCustomer.TriggerSpecialWalkAndWait(lucasWaitPoint, "¿Buenas? ¡Hola!", "LucasBringsVegetables", "Hablar");
            }
        }
    }

    private void AdvanceTutorial(string yarnNode, int nextStep)
    {
        currentTutorialStep = nextStep;

        if (dialogueRunner.IsDialogueRunning) 
        {
            dialogueRunner.Stop();
            StartCoroutine(RestartDialogueNextFrame(yarnNode));
        }
        else
        {
            dialogueRunner.StartDialogue(yarnNode);
        }
    }

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
            UnityEngine.AI.NavMeshAgent agent = activeLucas.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.SetDestination(lucasSpawnPoint.position);
            }
            
            Animator anim = activeLucas.GetComponent<Animator>();
            if (anim != null) anim.SetBool("Walking", true);
            
            StartCoroutine(WaitUntilArrivedThenDestroy(activeLucas));
            
            activeLucas = null; 
        }
    }

    public bool TryOverrideTableServe(PlateItem plate)
    {
        return isTutorialActive && currentTutorialStep == 11;
    }
    
    public int GetCurrentTutorialStep() => currentTutorialStep;
}