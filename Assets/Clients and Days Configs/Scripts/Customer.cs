using UnityEngine;
using UnityEngine.AI; 
using System;

public class Customer : MonoBehaviour
{
    private Animator animator;
    [HideInInspector] public CustomerProfile profile;
    [HideInInspector] public DialogueOverrideConfig overrides;

    [Header("Dialogue & Seating")]
    [Tooltip("Place an empty GameObject near the face and assign it here so the camera focuses perfectly.")]
    public Transform dialogueCameraTarget; 
    public Vector3 personalSeatOffset = Vector3.zero;

    private SittingSpot targetSeat;
    private Transform exitPoint;
    private Transform currentQueueSpot;
    private CustomerGroup myGroup; 
    
    // NEW: Added "Idle" as the first state so special NPCs don't walk away when spawned
    private enum State { Idle, MovingToSeat, Seated, Leaving, MovingToQueue, WaitingInQueue, FinishedEating, SpecialEvent }
    private State currentState = State.Idle;

    private bool hasOrdered = false;
    private Renderer bodyRenderer;
    private NavMeshAgent agent;

    public CustomerFaceController faceController;

    [Header("UI & Feedback")]
    public GameObject exclamationMarkPrefab;
    private GameObject activeExclamationMark;

    [Header("Head Tracking Animation")]
    public Transform headBone; // Map your Mixamo head bone here!
    public float maxHeadTurnAngle = 75f;
    public float headTurnSpeed = 6f;
    public float lookRadius = 4f;
    private float currentHeadWeight = 0f;

    public SittingSpot TargetSeat => targetSeat; // <--- NEW GETTER for YarnGameLogic!
    private float preOrderTimer = 0f; // <--- NEW TIMER for pre-order patience

    void Awake()
    {
        // FIX: Add a Kinematic Rigidbody. This prevents the physics engine from pushing the customer 
        // up into the air when their interaction BoxCollider overlaps with the chair/bench!
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(1f / transform.localScale.x, 2.2f / transform.localScale.y, 1f / transform.localScale.z);
        col.center = new Vector3(0, 1.1f / transform.localScale.y, 0);
        col.isTrigger = false; 

        if (faceController == null) faceController = GetComponent<CustomerFaceController>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        SetInteractable(false, "");
    }

    public void SetInteractable(bool active, string verb)
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

    public void ShowExclamationMark()
    {
        if (exclamationMarkPrefab != null && activeExclamationMark == null)
        {
            Vector3 pos = transform.position + new Vector3(0, 2.2f, 0); // Default safely above head
            if (headBone != null && headBone.position.y > transform.position.y + 0.5f) 
            {
                pos = headBone.position + new Vector3(0, 0.6f, 0);
            }
            
            // Setting parent true keeps it moving with him without warping the scale terribly
            activeExclamationMark = Instantiate(exclamationMarkPrefab, pos, Quaternion.identity);
            activeExclamationMark.transform.SetParent(transform, true);
        }
    }

    public void HideExclamationMark()
    {
        if (activeExclamationMark != null) Destroy(activeExclamationMark);
    }

    private void MoveToClosestNavPoint(Vector3 targetPos)
    {
        agent.enabled = true;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        else agent.SetDestination(targetPos);
    }

    private bool HasArrived()
    {
        if (agent == null || !agent.enabled || agent.pathPending) return false;
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
        if (currentState == State.SpecialEvent) return; 

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
        // FIX: Ignore uninitialized/story characters (Like Don Julio & Lucas)
        if (currentState == State.Idle) return; 

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
            if (!hasOrdered)
            {
                preOrderTimer += Time.deltaTime;
                if (faceController != null) faceController.UpdateWaitMood(preOrderTimer / profile.walkoutTime);

                bool isStoryCharacter = !string.IsNullOrEmpty(profile.yarnPostMealNodeName);
                if (preOrderTimer >= profile.walkoutTime && !isStoryCharacter)
                {
                    OrderManager.Instance.HandleQueueWalkout(profile, faceController);
                    Leave();
                }
            }
            else if (targetSeat != null && targetSeat.linkedTableSpot != null && faceController != null)
            {
                // Post-order patience (Food Wait)
                float patience = OrderManager.Instance.GetWaitTimePercent(targetSeat.linkedTableSpot);
                faceController.UpdateWaitMood(patience);
            }
        }
        UpdateAnimations();
    }

    void LateUpdate()
    {
        if (headBone == null || Camera.main == null) return;
        
        Transform camTransform = Camera.main.transform;
        Vector3 lookDir = camTransform.position - headBone.position;
        
        float angleToPlayer = Vector3.Angle(transform.forward, lookDir);
        bool shouldLook = (angleToPlayer <= maxHeadTurnAngle) && 
                          (Vector3.Distance(transform.position, camTransform.position) <= lookRadius) &&
                          (currentState != State.Leaving);

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
        if (targetSeat == null)
        {
            Leave();
            return;
        }

        currentState = State.Seated;
        agent.enabled = false; 
        
        animator.SetBool("Sitting", true);
        animator.SetBool("Walking", false);
        
        transform.position = targetSeat.transform.TransformPoint(targetSeat.customerOffset) + targetSeat.transform.TransformDirection(personalSeatOffset); 
        transform.rotation = targetSeat.transform.rotation * Quaternion.Euler(targetSeat.customerRotationOffset);
        
        targetSeat.OccupySpot();
        SetInteractable(true, "Tomar Orden");
    }

    public void Leave()
    {
        currentState = State.Leaving;
        if (targetSeat != null) targetSeat.FreeSeat(); 
        SetInteractable(false, "");

        // FIX: Force the highlight to shut down immediately
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null)
        {
            highlight.ToggleHighlight(false);
            highlight.enabled = false;
        }

        if (faceController != null && faceController.CurrentMood == CustomerFaceController.Mood.Angry)
            faceController.SetMood(CustomerFaceController.Mood.ReallyAngry);

        if (exitPoint != null)
        {
            Vector3 groundExit = new Vector3(exitPoint.position.x, transform.position.y, exitPoint.position.z);
            if (UnityEngine.AI.NavMesh.SamplePosition(groundExit, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
                MoveToClosestNavPoint(hit.position);
            else
                MoveToClosestNavPoint(groundExit);
        }
        else Destroy(gameObject);
    }

    public void HandleInteraction()
    {
        string triggerState = DetermineCurrentTriggerState();
        if (string.IsNullOrEmpty(triggerState)) return; 

        if (triggerState == "BeforeOrder" || triggerState == "PostMeal" || triggerState == "SpecialEvent")
        {
            RestaurantYarnView yarnView = FindAnyObjectByType<RestaurantYarnView>();
            if (yarnView != null) yarnView.currentSpeakerFace = faceController;

            Yarn.Unity.DialogueRunner runner = FindAnyObjectByType<Yarn.Unity.DialogueRunner>();
            string targetNode = triggerState == "PostMeal" ? profile.yarnPostMealNodeName : profile.yarnNodeName; 

            if (runner != null && !string.IsNullOrEmpty(targetNode))
            {
                try 
                {
                    YarnGameLogic.Instance.currentInteractingCustomer = this;
                    HideExclamationMark(); // <--- NEW: Hides mark!
                    
                    runner.StartDialogue(targetNode);
                    
                    if (triggerState == "BeforeOrder")
                    {
                        hasOrdered = true;
                        SetInteractable(true, "Repetir Orden");
                    }
                    else if (triggerState == "PostMeal" || triggerState == "SpecialEvent")
                    {
                        SetInteractable(false, "");
                    }
                    return; 
                }
                catch (System.Exception)
                {
                    Debug.LogWarning($"[Yarn] Node '{targetNode}' missing!");
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
        if (currentState == State.FinishedEating) return "PostMeal"; 
        if (currentState == State.SpecialEvent) return "SpecialEvent";
        if (currentState == State.WaitingInQueue) return "Queue";
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
        ShowExclamationMark();
    }

    public void TriggerSpecialWalkAndWait(Transform destination, string floatingText, string yarnNode, string promptVerb)
    {
        StartCoroutine(SpecialWalkRoutine(destination, floatingText, yarnNode, promptVerb));
    }

    private System.Collections.IEnumerator SpecialWalkRoutine(Transform destination, string floatingText, string yarnNode, string promptVerb)
    {
        currentState = State.SpecialEvent;
        SetInteractable(false, "");
        agent.enabled = true;
        
        if (UnityEngine.AI.NavMesh.SamplePosition(destination.position, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else 
            agent.SetDestination(destination.position);
            
        animator.SetBool("Walking", true);

        while (agent.pathPending || agent.remainingDistance > 0.3f) yield return null;

        animator.SetBool("Walking", false);
        
        if (profile == null)
        {
            profile = new CustomerProfile();
            profile.profileName = gameObject.name;
        }
        profile.yarnNodeName = yarnNode;

        SetInteractable(true, promptVerb);
        ShowExclamationMark();
    }

    private void StopFloatingWords()
    {
        if (FloatingTextManager.Instance != null) FloatingTextManager.Instance.StopWords();
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