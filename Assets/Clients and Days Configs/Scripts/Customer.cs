using UnityEngine;

public class CustomerPill : MonoBehaviour
{
    public float moveSpeed = 3f;[HideInInspector] public CustomerProfile profile;

    private SittingSpot targetSeat;
    private Transform exitPoint;
    private Transform currentQueueSpot;
    
    private enum State { MovingToSeat, Seated, Leaving, MovingToQueue, WaitingInQueue }
    private State currentState;

    public void Initialize(CustomerProfile p, SittingSpot seat, Transform exit)
    {
        profile = p;
        targetSeat = seat;
        exitPoint = exit;
        currentState = State.MovingToSeat;
        seat.ReserveSeat(this); 
    }

    public void InitializeQueue(CustomerProfile p, Transform queueSpot, Transform exit)
    {
        profile = p;
        currentQueueSpot = queueSpot;
        exitPoint = exit;
        currentState = State.MovingToQueue;
    }

    void Update()
    {
        if (currentState == State.MovingToSeat)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetSeat.transform.position, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetSeat.transform.position) < 0.1f) SitDown();
        }
        else if (currentState == State.Leaving)
        {
            transform.position = Vector3.MoveTowards(transform.position, exitPoint.position, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, exitPoint.position) < 0.1f) Destroy(gameObject);
        }
        else if (currentState == State.MovingToQueue)
        {
            transform.position = Vector3.MoveTowards(transform.position, currentQueueSpot.position, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, currentQueueSpot.position) < 0.1f)
            {
                currentState = State.WaitingInQueue;
            }
        }
    }

    public void PromoteToSeat(SittingSpot seat)
    {
        targetSeat = seat;
        currentState = State.MovingToSeat;
        seat.ReserveSeat(this);
    }

    public void UpdateQueueSpot(Transform newSpot)
    {
        if (currentState == State.MovingToQueue || currentState == State.WaitingInQueue)
        {
            currentQueueSpot = newSpot;
            currentState = State.MovingToQueue;
        }
    }

    public bool IsLeaving() => currentState == State.Leaving;

    private void SitDown()
    {
        currentState = State.Seated;
        transform.position = targetSeat.transform.position; 
        targetSeat.OccupySpot();
        OrderManager.Instance.GenerateOrderForTable(targetSeat, profile);
    }

    public void Leave()
    {
        currentState = State.Leaving;
        if (targetSeat != null) targetSeat.FreeSeat(); 
    }
}