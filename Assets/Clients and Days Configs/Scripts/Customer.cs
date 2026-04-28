using UnityEngine;
using UnityEngine.AI; 

public class Customer : MonoBehaviour
{
    [HideInInspector] public CustomerProfile profile;

    private SittingSpot targetSeat;
    private Transform exitPoint;
    private Transform currentQueueSpot;
    private CustomerGroup myGroup; 
    
    private enum State { MovingToSeat, Seated, Leaving, MovingToQueue, WaitingInQueue }
    private State currentState;

    private bool hasOrdered = false;
    private Renderer bodyRenderer;
    private NavMeshAgent agent;

    void Awake()
    {
        bodyRenderer = GetComponentInChildren<Renderer>();
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

        // --- FIXED: Strip out the Y-axis so they don't try to float/fly to an elevated door marker ---
        Vector3 groundExit = new Vector3(exitPoint.position.x, transform.position.y, exitPoint.position.z);
        MoveToClosestNavPoint(groundExit);
    }

    public void InteractWithCustomer()
    {
        if (currentState == State.WaitingInQueue && myGroup != null)
        {
            int totalSeconds = Mathf.FloorToInt(myGroup.waitTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            
            string timeText = minutes > 0 ? $"{minutes} minutos y {seconds} segundos" : $"{seconds} segundos";
            RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Llevo esperando {timeText}. ¡Por favor apresúrate!");
        }
        else if (currentState == State.Seated && targetSeat != null && targetSeat.linkedTableSpot != null)
        {
            string orderText = OrderManager.Instance.GetOrderText(targetSeat.linkedTableSpot);
            
            if (!hasOrdered)
            {
                hasOrdered = true;
                RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"Hola, me gustaría pedir: {orderText}.");
                SetInteractable(true, "Repetir Orden");
            }
            else
            {
                RestaurantUIManager.Instance.ShowDialogue(profile.profileName, $"¿Otra vez? Yo pedí: {orderText}.");
            }
        }
    }
}