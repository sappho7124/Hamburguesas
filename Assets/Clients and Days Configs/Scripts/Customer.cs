// Location: C:\Games\Unity\Hamburguesas\Assets\Clients and Days Configs\Scripts\Customer.cs
using UnityEngine;
using UnityEngine.AI; 
using System;

public class Customer : MonoBehaviour
{
    private Animator animator;
    [HideInInspector] public CustomerProfile profile;
    [HideInInspector] public DialogueOverrideConfig overrides;

    private SittingSpot targetSeat;
    private Transform exitPoint;
    private Transform currentQueueSpot;
    private CustomerGroup myGroup; 
    
    private enum State { MovingToSeat, Seated, Leaving, MovingToQueue, WaitingInQueue, FinishedEating}
    private State currentState;

    private bool hasOrdered = false;
    private Renderer bodyRenderer;
    private NavMeshAgent agent;

    public CustomerFaceController faceController;

    [Header("Head Tracking Animation")]
    public Transform headBone; // Map your Mixamo head bone here!
    public float maxHeadTurnAngle = 75f;
    public float headTurnSpeed = 6f;
    public float lookRadius = 4f;
    private float currentHeadWeight = 0f;

    void Awake()
    {
        if (faceController == null) faceController = GetComponent<CustomerFaceController>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        SetInteractable(false, "");
    }

    private void SetInteractable(bool active, string verb)
    {
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null)
        {
            highlight.enabled = active;
            highlight.interactionVerb = verb;
            if (profile != null) highlight.objectName = profile.profileName;
        }
        InteractableObject interactable = GetComponent<InteractableObject>();
        if (interactable != null) interactable.enabled = active;
    }

    private void MoveToClosestNavPoint(Vector3 targetPos)
    {
        agent.enabled = true;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        else agent.SetDestination(targetPos);
    }

    private bool HasArrived()
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance + 0.4f;
    }

    public void Initialize(CustomerProfile p, SittingSpot seat, Transform exit)
    {
        profile = p;
        overrides = CustomerSpawner.Instance.overridesMap.ContainsKey(p.profileName) ? CustomerSpawner.Instance.overridesMap[p.profileName] : null;
        targetSeat = seat;
        exitPoint = exit;
        currentState = State.MovingToSeat;
        SetInteractable(false, "");
        seat.ReserveSeat(this); 
        MoveToClosestNavPoint(targetSeat.transform.position); 
    }

    private void UpdateAnimations()
    {
        bool walking = currentState == State.MovingToSeat || currentState == State.MovingToQueue || currentState == State.Leaving;
        bool sitting = currentState == State.Seated;

        animator.SetBool("Walking", walking);
        animator.SetBool("Sitting", sitting);
    }

    public void InitializeQueue(CustomerProfile p, Transform queueSpot, Transform exit, CustomerGroup group)
    {
        profile = p;
        overrides = CustomerSpawner.Instance.overridesMap.ContainsKey(p.profileName) ? CustomerSpawner.Instance.overridesMap[p.profileName] : null;
        currentQueueSpot = queueSpot;
        exitPoint = exit;
        myGroup = group;
        currentState = State.MovingToQueue;
        SetInteractable(false, "");
        MoveToClosestNavPoint(currentQueueSpot.position);
    }

    void Update()
    {
        if (currentState == State.MovingToSeat) { if (HasArrived()) SitDown(); }
        else if (currentState == State.Leaving) { if (HasArrived()) Destroy(gameObject); }
        else if (currentState == State.MovingToQueue) { 
            if (HasArrived()) {
                currentState = State.WaitingInQueue;
                SetInteractable(true, "Preguntar Tiempo"); 
            }
        }
        else if (currentState == State.WaitingInQueue)
        {
            if (myGroup != null && faceController != null)
            {
                float patience = Mathf.Clamp01(myGroup.waitTimer / myGroup.maxWaitTime);
                faceController.UpdateWaitMood(patience); 
            }
        }
        else if (currentState == State.Seated)
        {
            if (targetSeat != null && targetSeat.linkedTableSpot != null && faceController != null)
            {
                float patience = OrderManager.Instance.GetWaitTimePercent(targetSeat.linkedTableSpot);
                faceController.UpdateWaitMood(patience);
            }
        }
        UpdateAnimations();
    }

    // --- NEW: Procedural Head Look At ---
    void LateUpdate()
    {
        if (headBone == null || Camera.main == null) return;
        
        Transform camTransform = Camera.main.transform;
        Vector3 lookDir = camTransform.position - headBone.position;
        
        float angleToPlayer = Vector3.Angle(transform.forward, lookDir);
        bool shouldLook = (angleToPlayer <= maxHeadTurnAngle) && 
                          (Vector3.Distance(transform.position, camTransform.position) <= lookRadius) &&
                          (currentState != State.Leaving);

        // Smoothly blend the tracking weight on top of the animator
        currentHeadWeight = Mathf.MoveTowards(currentHeadWeight, shouldLook ? 1f : 0f, Time.deltaTime * headTurnSpeed);

        if (currentHeadWeight > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            headBone.rotation = Quaternion.Slerp(headBone.rotation, targetRot, currentHeadWeight);
        }
    }

    public void PromoteToSeat(SittingSpot seat)
    {
        targetSeat = seat;
        currentState = State.MovingToSeat;
        SetInteractable(false, ""); 
        seat.ReserveSeat(this);
        MoveToClosestNavPoint(targetSeat.transform.position);
    }

    public void UpdateQueueSpot(Transform newSpot)
    {
        if (currentState == State.MovingToQueue || currentState == State.WaitingInQueue)
        {
            currentQueueSpot = newSpot;
            currentState = State.MovingToQueue;
            SetInteractable(false, ""); 
            MoveToClosestNavPoint(currentQueueSpot.position);
        }
    }

    public bool IsLeaving() => currentState == State.Leaving;

    private void SitDown()
    {
        currentState = State.Seated;
        agent.enabled = false; 
        transform.position = targetSeat.transform.TransformPoint(targetSeat.customerOffset); 
        transform.rotation = targetSeat.transform.rotation; 
        targetSeat.OccupySpot();
        SetInteractable(true, "Tomar Orden");
    }

    public void Leave()
    {
        currentState = State.Leaving;
        if (targetSeat != null) targetSeat.FreeSeat(); 
        SetInteractable(false, "");

        if (faceController != null && faceController.CurrentMood == CustomerFaceController.Mood.Angry)
            faceController.SetMood(CustomerFaceController.Mood.ReallyAngry);

        Vector3 groundExit = new Vector3(exitPoint.position.x, transform.position.y, exitPoint.position.z);
        if (UnityEngine.AI.NavMesh.SamplePosition(groundExit, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
            MoveToClosestNavPoint(hit.position);
        else
            MoveToClosestNavPoint(groundExit);
    }

// --- MASTER INTERACTION HANDLER ---
    // Link this directly to your InteractableObject's "OnInteract" Unity Event!
    public void HandleInteraction()
    {
        string triggerState = DetermineCurrentTriggerState();
        if (string.IsNullOrEmpty(triggerState)) return; 

        // 1. MAIN EVENT: Taking the order OR Talking after eating!
        if (triggerState == "BeforeOrder" || triggerState == "PostMeal")
        {
            RestaurantYarnView yarnView = FindAnyObjectByType<RestaurantYarnView>();
            if (yarnView != null) yarnView.currentSpeakerFace = faceController;

            Yarn.Unity.DialogueRunner runner = FindAnyObjectByType<Yarn.Unity.DialogueRunner>();
            
            // Choose the correct Yarn Node based on the state!
            string targetNode = triggerState == "BeforeOrder" ? profile.yarnNodeName : profile.yarnPostMealNodeName; 

            if (runner != null && !string.IsNullOrEmpty(targetNode))
            {
                try 
                {
                    YarnGameLogic.Instance.currentInteractingCustomer = this;
                    runner.StartDialogue(targetNode);
                    
                    if (triggerState == "BeforeOrder")
                    {
                        hasOrdered = true;
                        SetInteractable(true, "Repetir Orden");
                    }
                    else if (triggerState == "PostMeal")
                    {
                        // Turn off interaction so you can't talk to them again while they are leaving
                        SetInteractable(false, "");
                    }
                    return; 
                }
                catch (System.Exception)
                {
                    Debug.LogWarning($"[Yarn] Node '{targetNode}' missing or invalid! Using generic C# fallback.");
                }
            }
        }

        ExecuteStandardInteraction(triggerState);
    }

    private string DetermineCurrentTriggerState()
    {
        if (currentState == State.WaitingInQueue) return "Queue";
        if (currentState == State.Seated && !hasOrdered) return "BeforeOrder";
        if (currentState == State.Seated && hasOrdered) return "AfterOrder";
        if (currentState == State.FinishedEating) return "PostMeal"; // Added this!
        return "";
    }

    private DialogueOverride GetOverrideForState(int day, string state)
    {
        if (overrides == null || overrides.overrides == null) return null;
        foreach (var ov in overrides.overrides) if (ov.day == day && ov.triggerState == state) return ov;
        return null;
    }

    public void MarkAsFinishedEating()
    {
        currentState = State.FinishedEating;
        SetInteractable(true, "Hablar");
    }

    private void ExecuteStandardInteraction(string state)
    {
        if (state == "Queue" && myGroup != null)
        {
            int totalSeconds = Mathf.FloorToInt(myGroup.waitTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            string timeText = minutes > 0 ? $"{minutes} minutos y {seconds} segundos" : $"{seconds} segundos";
            
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Llevo esperando {timeText}. ¡Por favor apresúrate!", faceController.CurrentMood, faceController);
        }
        else if (state == "BeforeOrder" && targetSeat != null && targetSeat.linkedTableSpot != null)
        {
            hasOrdered = true;
            string orderText = OrderManager.Instance.GetOrderText(targetSeat.linkedTableSpot);
            
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Hola, me gustaría pedir: {orderText}.", faceController.CurrentMood, faceController);
            SetInteractable(true, "Repetir Orden");
        }
        else if (state == "AfterOrder" && targetSeat != null && targetSeat.linkedTableSpot != null)
        {
            string orderText = OrderManager.Instance.GetOrderText(targetSeat.linkedTableSpot);
            
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"¿Otra vez? Yo pedí: {orderText}.", faceController.CurrentMood, faceController);
        }
    }
}