using UnityEngine;
using UnityEngine.AI; 
using System;

public class Customer : MonoBehaviour
{
    [HideInInspector] public CustomerProfile profile;
    [HideInInspector] public DialogueOverrideConfig overrides;

    private SittingSpot targetSeat;
    private Transform exitPoint;
    private Transform currentQueueSpot;
    private CustomerGroup myGroup; 
    
    private enum State { MovingToSeat, Seated, Leaving, MovingToQueue, WaitingInQueue }
    private State currentState;

    private bool hasOrdered = false;
    private Renderer bodyRenderer;
    private NavMeshAgent agent;

    public CustomerFaceController faceController;

    void Awake()
    {
        if (faceController == null) faceController = GetComponent<CustomerFaceController>();
        agent = GetComponent<NavMeshAgent>(); 
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

    // --- NEW: Smart Navigation Helper ---
    // Finds the closest valid spot on the floor so they don't try to walk inside solid tables
    private void MoveToClosestNavPoint(Vector3 targetPos)
    {
        agent.enabled = true;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(targetPos); // Fallback
        }
    }

    // --- NEW: Generous Arrival Check ---
    private bool HasArrived()
    {
        if (agent.pathPending) return false;
        // As long as they get within 0.5 units (or the stopping distance), count it as arrived!
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
        if (currentState == State.MovingToSeat)
        {
            if (HasArrived()) SitDown();
        }
        else if (currentState == State.Leaving)
        {
            if (HasArrived()) Destroy(gameObject);
        }
        else if (currentState == State.MovingToQueue)
        {
            if (HasArrived())
            {
                currentState = State.WaitingInQueue;
                SetInteractable(true, "Preguntar Tiempo"); 
            }
        }
        else if (currentState == State.WaitingInQueue)
        {
            if (myGroup != null && bodyRenderer != null)
            {
                float patience = Mathf.Clamp01(myGroup.waitTimer / myGroup.maxWaitTime);
                bodyRenderer.material.color = Color.Lerp(Color.green, Color.red, patience);
            }
        }
        else if (currentState == State.Seated)
        {
            if (targetSeat != null && targetSeat.linkedTableSpot != null && bodyRenderer != null)
            {
                float patience = OrderManager.Instance.GetWaitTimePercent(targetSeat.linkedTableSpot);
                bodyRenderer.material.color = Color.Lerp(Color.green, Color.red, patience);
            }
        }

        

        else if (currentState == State.WaitingInQueue)
        {
            if (myGroup != null && faceController != null)
            {
                float patience = Mathf.Clamp01(myGroup.waitTimer / myGroup.maxWaitTime);
                // REPLACE color lerp with Face update
                faceController.UpdateWaitMood(patience); 
            }
        }
        else if (currentState == State.Seated)
        {
            if (targetSeat != null && targetSeat.linkedTableSpot != null && faceController != null)
            {
                float patience = OrderManager.Instance.GetWaitTimePercent(targetSeat.linkedTableSpot);
                // REPLACE color lerp with Face update
                faceController.UpdateWaitMood(patience);
            }
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
        
        // Turn off AI physics so they snap cleanly into the chair coordinates
        agent.enabled = false; 
        
        transform.position = targetSeat.transform.TransformPoint(targetSeat.customerOffset); 
        
        transform.rotation = targetSeat.transform.rotation; 
        
        targetSeat.OccupySpot();
        OrderManager.Instance.GenerateOrderForTable(targetSeat, profile);

        SetInteractable(true, "Tomar Orden");
    }

    public void Leave()
    {
        currentState = State.Leaving;
        if (targetSeat != null) targetSeat.FreeSeat(); 
        SetInteractable(false, "");

        // If they left because of time (waitTimer is maxed out roughly), they are Really Angry
        if (faceController != null && faceController.CurrentMood == CustomerFaceController.Mood.Angry)
        {
            faceController.SetMood(CustomerFaceController.Mood.ReallyAngry);
        }

        // --- NAVMESH EXIT FIX ---
        Vector3 groundExit = new Vector3(exitPoint.position.x, transform.position.y, exitPoint.position.z);
        
        // Sample a wide area (5 units) around the exit point to find a valid spot
        if (UnityEngine.AI.NavMesh.SamplePosition(groundExit, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            MoveToClosestNavPoint(hit.position);
        }
        else
        {
            // Fallback if the exit is WAY off the map
            MoveToClosestNavPoint(groundExit);
        }
    }

    public void InteractWithCustomer()
    {
        if (currentState == State.WaitingInQueue && myGroup != null)
        {
            int totalSeconds = Mathf.FloorToInt(myGroup.waitTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            
            string timeText = minutes > 0 ? $"{minutes} minutos y {seconds} segundos" : $"{seconds} segundos";
            
            // FIXED: Pass current mood so they look angry/neutral while complaining
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Llevo esperando {timeText}. ¡Por favor apresúrate!", faceController.CurrentMood);
        }
        else if (currentState == State.Seated && targetSeat != null && targetSeat.linkedTableSpot != null)
        {
            string orderText = OrderManager.Instance.GetOrderText(targetSeat.linkedTableSpot);
            
            if (!hasOrdered)
            {
                hasOrdered = true;
                // FIXED: Pass current mood
                RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Hola, me gustaría pedir: {orderText}.", faceController.CurrentMood);
                SetInteractable(true, "Repetir Orden");
            }
            else
            {
                // FIXED: Pass current mood
                RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"¿Otra vez? Yo pedí: {orderText}.", faceController.CurrentMood);
            }
        }
    }

        // --- NEW: THE MASTER INTERACTION HANDLER ---
    // Link this directly to your InteractableObject's "OnInteract" Unity Event!
    public void HandleInteraction()
    {
        int currentDay = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave.currentDay : 1;
        string triggerState = DetermineCurrentTriggerState();

        if (string.IsNullOrEmpty(triggerState)) return; // Not ready to interact

        // 1. Check for Overrides!
        DialogueOverride currentOverride = GetOverrideForState(currentDay, triggerState);

        if (currentOverride != null)
        {
            // Parse the mood from string to Enum
            CustomerFaceController.Mood overrideMood = CustomerFaceController.Mood.Neutral;
            Enum.TryParse(currentOverride.moodName, out overrideMood);
            
            // --- FUTURE YARN INTEGRATION SPOT ---
            // If (YarnIsActive) { YarnRunner.StartDialogue(currentOverride.yarnNode); return; }
            
            // Fallback Text System
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, currentOverride.fallbackText, overrideMood);
            
            // If they were supposed to order, we mark it true so they progress to "AfterOrder" next time
            if (triggerState == "BeforeOrder") 
            {
                hasOrdered = true;
                SetInteractable(true, "Hablar");
            }
            return; // Stop here! Override handled it.
        }

        // 2. STANDARD BEHAVIOR (If no override was found)
        ExecuteStandardInteraction(triggerState);
    }

    private string DetermineCurrentTriggerState()
    {
        if (currentState == State.WaitingInQueue) return "Queue";
        if (currentState == State.Seated && !hasOrdered) return "BeforeOrder";
        if (currentState == State.Seated && hasOrdered) return "AfterOrder";
        return "";
    }

    private DialogueOverride GetOverrideForState(int day, string state)
    {
        if (overrides == null || overrides.overrides == null) return null;

        foreach (var ov in overrides.overrides)
        {
            if (ov.day == day && ov.triggerState == state)
            {
                return ov;
            }
        }
        return null;
    }

    // Moved the old hardcoded logic here to act as the default fallback
    private void ExecuteStandardInteraction(string state)
    {
        if (state == "Queue" && myGroup != null)
        {
            int totalSeconds = Mathf.FloorToInt(myGroup.waitTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            
            string timeText = minutes > 0 ? $"{minutes} minutos y {seconds} segundos" : $"{seconds} segundos";
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Llevo esperando {timeText}. ¡Por favor apresúrate!", faceController.CurrentMood);
        }
        else if (state == "BeforeOrder" && targetSeat != null && targetSeat.linkedTableSpot != null)
        {
            hasOrdered = true;
            string orderText = OrderManager.Instance.GetOrderText(targetSeat.linkedTableSpot);
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Hola, me gustaría pedir: {orderText}.", faceController.CurrentMood);
            SetInteractable(true, "Repetir Orden");
        }
        else if (state == "AfterOrder" && targetSeat != null && targetSeat.linkedTableSpot != null)
        {
            string orderText = OrderManager.Instance.GetOrderText(targetSeat.linkedTableSpot);
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"¿Otra vez? Yo pedí: {orderText}.", faceController.CurrentMood);
        }
    }
}