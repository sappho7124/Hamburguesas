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
    public List<GroupSizeWeight> groupSizeWeights; // NEW: Probabilities for this specific time block
}[System.Serializable]
public class GroupSizeWeight { 
    public int groupSize; 
    public int weight; 
}
[System.Serializable]
public class ScheduledNPC { public float time; public List<string> profileNames; [HideInInspector] public bool hasSpawned; }

// --- GROUP CLASS ---
public class CustomerGroup
{
    public List<CustomerPill> members = new List<CustomerPill>();
    public float waitTimer = 0f;
    public float maxWaitTime = 999f;
    public bool isLeaving = false;
}

// --- MANAGER CLASS ---
public class CustomerSpawner : MonoBehaviour
{
    public static CustomerSpawner Instance;

    [Header("Day & Profile JSONs")]
    public TextAsset currentDayConfigJSON;
    public TextAsset[] allCustomerProfiles;[Header("Spawn Logic")]
    public GameObject customerPillPrefab; 
    public Transform entrancePoint;
    public Transform exitPoint;

    [Header("Restaurant Setup")]
    public List<SittingSpot> allSittingSpots;
    
    [Header("Queue System")]
    public List<Transform> queueSpots; 

    private Dictionary<string, string> profileJsonMap = new Dictionary<string, string>();
    private List<CustomerGroup> queueGroups = new List<CustomerGroup>();

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
        if (bracket.groupSizeWeights == null || bracket.groupSizeWeights.Count == 0) return 1; // Failsafe

        int totalWeight = bracket.groupSizeWeights.Sum(w => w.weight);
        int randomRoll = Random.Range(0, totalWeight);

        foreach (var w in bracket.groupSizeWeights)
        {
            randomRoll -= w.weight;
            if (randomRoll < 0) return w.groupSize;
        }

        return 1; // Default failsafe
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
            GameObject pillObj = Instantiate(customerPillPrefab, entrancePoint.position, Quaternion.identity);
            CustomerPill pill = pillObj.GetComponent<CustomerPill>();
            
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
                newGroup.members[i].InitializeQueue(newGroup.members[i].profile, qSpot, exitPoint);
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

        // The line skipping logic is right here:
        // It checks group 0. If it doesn't fit, it moves to group 1, then group 2.
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
                i--; // Step back to evaluate the new group that took this index
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

    private List<SittingSpot> FindAvailableCluster(int requiredSize)
    {
        List<SittingSpot> availableSeats = allSittingSpots.Where(s => !s.isOccupied && !s.isReserved).ToList();
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
        return null; 
    }
}