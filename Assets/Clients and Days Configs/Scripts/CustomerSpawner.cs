using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// --- DAY JSON DATA CLASSES ---
[System.Serializable]
public class DayConfig {
    public float shiftDuration; 
    public List<TimeBracket> spawnRates;
    public List<ScheduledNPC> scheduledNPCs;
    public List<string> genericProfiles; 
}
[System.Serializable]
public class TimeBracket { 
    public float startTime; 
    public float endTime; 
    public float spawnInterval; 
    public List<GroupSizeWeight> groupSizeWeights;
}
[System.Serializable]
public class GroupSizeWeight { 
    public int groupSize; 
    public int weight; 
}
[System.Serializable]
public class ScheduledNPC { public float time; public List<string> profileNames; [HideInInspector] public bool hasSpawned; }

// --- GROUP CLASS ---
public class CustomerGroup
{
    public List<Customer> members = new List<Customer>();
    public float waitTimer = 0f;
    public float maxWaitTime = 999f;
    public bool isLeaving = false;
}

// --- NEW: TABLE ISLAND CLASS ---
public class TableIsland
{
    public List<SittingSpot> spots = new List<SittingSpot>();
    public bool isClosedLoop; // True if it's a private table, false if it's an open chain (like a Bar)

    public bool IsEmpty()
    {
        foreach (var spot in spots)
        {
            if (spot.isOccupied || spot.isReserved) return false;
        }
        return true;
    }
}

// --- MANAGER CLASS ---
public class CustomerSpawner : MonoBehaviour
{
    public static CustomerSpawner Instance;

    [Header("Day & Profile JSONs")]
    public TextAsset currentDayConfigJSON;
    public TextAsset[] allCustomerProfiles;

    [Header("Spawn Logic")]
    public GameObject CustomerPrefab; 
    public Transform entrancePoint;
    public Transform exitPoint;

    [Header("Restaurant Setup")]
    public List<SittingSpot> allSittingSpots;
    
    [Header("Queue System")]
    public List<Transform> queueSpots; 

    private Dictionary<string, string> profileJsonMap = new Dictionary<string, string>();
    private List<CustomerGroup> queueGroups = new List<CustomerGroup>();
    private List<TableIsland> tableIslands = new List<TableIsland>(); // NEW: Stores detected tables

    private DayConfig currentDay;
    private bool isShiftActive = false;
    private float shiftTimer = 0f;
    private float timeSinceLastGenericSpawn = 0f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        foreach (var textAsset in allCustomerProfiles)
        {
            if (textAsset == null) continue;
            try
            {
                CustomerProfile p = JsonUtility.FromJson<CustomerProfile>(textAsset.text);
                if (p != null && !string.IsNullOrEmpty(p.profileName))
                {
                    profileJsonMap[p.profileName] = textAsset.text;
                }
            }
            catch (System.Exception e) { Debug.LogError($"<color=red>[JSON CRASH]</color> '{textAsset.name}': {e.Message}"); }
        }
    }

    void Start()
    {
        // Detect and categorize tables as soon as the game starts
        DetectTableIslands();
    }

    private void DetectTableIslands()
    {
        tableIslands.Clear();
        HashSet<SittingSpot> unvisited = new HashSet<SittingSpot>(allSittingSpots);

        while (unvisited.Count > 0)
        {
            SittingSpot startSeat = unvisited.First();
            TableIsland newIsland = new TableIsland();

            Queue<SittingSpot> queue = new Queue<SittingSpot>();
            queue.Enqueue(startSeat);
            unvisited.Remove(startSeat);
            newIsland.spots.Add(startSeat);

            while (queue.Count > 0)
            {
                SittingSpot current = queue.Dequeue();
                foreach (var neighbor in current.connectedSpots)
                {
                    if (neighbor != null && unvisited.Contains(neighbor))
                    {
                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                        newIsland.spots.Add(neighbor);
                    }
                }
            }

            // Determine if it's a Closed Loop (Normal Table) or an Open Chain (Bar).
            // A group is an Open Chain if it has > 2 seats AND has end-points (seats with only 1 connection).
            // Otherwise, it's considered a Closed Loop (Private).
            bool hasEnds = newIsland.spots.Any(s => s.connectedSpots.Count <= 1) && newIsland.spots.Count > 2;
            newIsland.isClosedLoop = !hasEnds;

            tableIslands.Add(newIsland);
        }

        Debug.Log($"[Seating System] Initialization complete. Detected {tableIslands.Count} total isolated seating groups. " + 
                  $"({tableIslands.Count(t => t.isClosedLoop)} Private Tables, {tableIslands.Count(t => !t.isClosedLoop)} Open Chains).");
    }

    public void StartShift()
    {
        if (isShiftActive || currentDayConfigJSON == null) return;

        try
        {
            currentDay = JsonUtility.FromJson<DayConfig>(currentDayConfigJSON.text);
            if (currentDay.scheduledNPCs == null) currentDay.scheduledNPCs = new List<ScheduledNPC>();
            if (currentDay.genericProfiles == null) currentDay.genericProfiles = new List<string>();
            if (currentDay.spawnRates == null) currentDay.spawnRates = new List<TimeBracket>();

            isShiftActive = true;
            shiftTimer = 0f;
            timeSinceLastGenericSpawn = 0f;
        }
        catch (System.Exception e) { Debug.LogError($"<color=red>[JSON CRASH]</color> Day Config: {e.Message}"); }
    }

    void Update()
    {
        ManageQueueFlow();

        if (!isShiftActive) return;

        shiftTimer += Time.deltaTime;
        RestaurantUIManager.Instance.UpdateShiftTimer(shiftTimer, currentDay.shiftDuration);

        if (shiftTimer >= currentDay.shiftDuration)
        {
            EndShift();
            return;
        }

        // 1. Process Scheduled Spawns
        foreach (var npc in currentDay.scheduledNPCs)
        {
            if (!npc.hasSpawned && shiftTimer >= npc.time)
            {
                npc.hasSpawned = true;
                TrySpawnCustomerGroup(npc.profileNames);
            }
        }

        // 2. Process Generic Spawns
        TimeBracket currentBracket = GetCurrentTimeBracket();
        if (currentBracket != null && currentBracket.spawnInterval > 0 && currentDay.genericProfiles.Count > 0)
        {
            timeSinceLastGenericSpawn += Time.deltaTime;
            if (timeSinceLastGenericSpawn >= currentBracket.spawnInterval)
            {
                timeSinceLastGenericSpawn = 0f;
                
                // Determine group size based on weighted random
                int groupSize = DetermineGroupSize(currentBracket);
                
                List<string> groupProfiles = new List<string>();
                for (int i = 0; i < groupSize; i++)
                {
                    groupProfiles.Add(currentDay.genericProfiles[Random.Range(0, currentDay.genericProfiles.Count)]);
                }
                TrySpawnCustomerGroup(groupProfiles);
            }
        }
    }

    private void EndShift()
    {
        isShiftActive = false;
        foreach (var group in queueGroups)
        {
            foreach (var member in group.members) if (member != null) member.Leave();
        }
        queueGroups.Clear();
    }

    private TimeBracket GetCurrentTimeBracket()
    {
        foreach (var bracket in currentDay.spawnRates)
            if (shiftTimer >= bracket.startTime && shiftTimer <= bracket.endTime) return bracket;
        return null; 
    }

    // --- WEIGHTED RANDOM FOR GROUP SIZE ---
    private int DetermineGroupSize(TimeBracket bracket)
    {
        if (bracket.groupSizeWeights == null || bracket.groupSizeWeights.Count == 0) return 1;

        int totalWeight = bracket.groupSizeWeights.Sum(w => w.weight);
        int randomRoll = Random.Range(0, totalWeight);

        foreach (var w in bracket.groupSizeWeights)
        {
            randomRoll -= w.weight;
            if (randomRoll < 0) return w.groupSize;
        }

        return 1;
    }

    // --- GROUP SPAWNING & QUEUE BATCHING ---
    public void TrySpawnCustomerGroup(List<string> profileNames)
    {
        if (profileNames == null || profileNames.Count == 0) return;

        int currentQueueSize = queueGroups.Sum(g => g.members.Count);
        if (currentQueueSize + profileNames.Count > queueSpots.Count)
        {
            Debug.LogWarning($"<color=#FF00FF>[REJECTED]</color> Group of {profileNames.Count} arrived, but the line is too long!");
            return;
        }

        CustomerGroup newGroup = new CustomerGroup();
        
        foreach (string pName in profileNames)
        {
            if (!profileJsonMap.ContainsKey(pName)) continue;

            CustomerProfile profile = JsonUtility.FromJson<CustomerProfile>(profileJsonMap[pName]);
            GameObject pillObj = Instantiate(CustomerPrefab, entrancePoint.position, Quaternion.identity);
            Customer pill = pillObj.GetComponent<Customer>();
            
            pill.profile = profile;
            newGroup.members.Add(pill);

            if (profile.queueWaitTime < newGroup.maxWaitTime) newGroup.maxWaitTime = profile.queueWaitTime;
        }

        if (newGroup.members.Count == 0) return;

        List<SittingSpot> cluster = FindAvailableCluster(newGroup.members.Count);
        if (cluster != null)
        {
            for (int i = 0; i < newGroup.members.Count; i++)
                newGroup.members[i].Initialize(newGroup.members[i].profile, cluster[i], exitPoint);
        }
        else
        {
            int queueIndexStart = currentQueueSize;
            for (int i = 0; i < newGroup.members.Count; i++)
            {
                Transform qSpot = queueSpots[queueIndexStart + i];
                newGroup.members[i].InitializeQueue(newGroup.members[i].profile, qSpot, exitPoint, newGroup); 
            }
            queueGroups.Add(newGroup);
        }
    }

    private void ManageQueueFlow()
    {
        bool queueShifted = false;

        for (int i = queueGroups.Count - 1; i >= 0; i--)
        {
            CustomerGroup group = queueGroups[i];

            if (!group.isLeaving)
            {
                group.waitTimer += Time.deltaTime;
                if (group.waitTimer >= group.maxWaitTime)
                {
                    group.isLeaving = true;
                    OrderManager.Instance.HandleQueueWalkout(group.members[0].profile);
                    foreach (var member in group.members) member.Leave();
                }
            }

            if (group.isLeaving || group.members.All(m => m == null || m.IsLeaving()))
            {
                queueGroups.RemoveAt(i);
                queueShifted = true;
            }
        }

        for (int i = 0; i < queueGroups.Count; i++)
        {
            CustomerGroup group = queueGroups[i];
            List<SittingSpot> cluster = FindAvailableCluster(group.members.Count);

            if (cluster != null)
            {
                for (int j = 0; j < group.members.Count; j++)
                {
                    group.members[j].PromoteToSeat(cluster[j]);
                }
                
                queueGroups.RemoveAt(i);
                queueShifted = true;
                i--;
            }
        }

        if (queueShifted)
        {
            int spotIndex = 0;
            foreach (var group in queueGroups)
            {
                foreach (var member in group.members)
                {
                    if (spotIndex < queueSpots.Count)
                    {
                        member.UpdateQueueSpot(queueSpots[spotIndex]);
                        spotIndex++;
                    }
                }
            }
        }
    }

    // --- REWORKED: TABLE EXCLUSIVITY SEARCH ---
    private List<SittingSpot> FindAvailableCluster(int requiredSize)
    {
        foreach (var table in tableIslands)
        {
            // If it's a Closed Loop (Normal private table), it MUST be completely empty. Strangers don't share!
            if (table.isClosedLoop && !table.IsEmpty()) continue;

            // Grab available seats inside this specific island
            List<SittingSpot> availableSeats = table.spots.Where(s => !s.isOccupied && !s.isReserved).ToList();

            // Quick capacity check
            if (availableSeats.Count < requiredSize) continue;

            HashSet<SittingSpot> globalVisited = new HashSet<SittingSpot>();

            foreach (var startSeat in availableSeats)
            {
                if (globalVisited.Contains(startSeat)) continue;

                List<SittingSpot> currentCluster = new List<SittingSpot>();
                Queue<SittingSpot> queue = new Queue<SittingSpot>();
                HashSet<SittingSpot> localVisited = new HashSet<SittingSpot>();

                queue.Enqueue(startSeat);
                localVisited.Add(startSeat);
                globalVisited.Add(startSeat);

                while (queue.Count > 0 && currentCluster.Count < requiredSize)
                {
                    SittingSpot current = queue.Dequeue();
                    currentCluster.Add(current);

                    if (currentCluster.Count == requiredSize) return currentCluster; 

                    foreach (var neighbor in current.connectedSpots)
                    {
                        if (neighbor != null && availableSeats.Contains(neighbor) && !localVisited.Contains(neighbor))
                        {
                            localVisited.Add(neighbor);
                            globalVisited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
        return null; 
    }
}