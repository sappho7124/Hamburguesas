using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// --- DAY JSON DATA CLASSES ---
[System.Serializable]
public class DayConfig
{
    public float shiftDuration;
    public List<ScheduledNPC> scheduledNPCs;
}

[System.Serializable]
public class DialogueOverride
{
    public int day;
    public string triggerState;
    public string yarnNode;
    public string fallbackText;
    public string moodName;
}

[System.Serializable]
public class DialogueOverrideConfig
{
    public List<DialogueOverride> overrides = new List<DialogueOverride>();
}

[System.Serializable]
public class ScheduledNPC
{
    public float time;
    public List<string> profileNames;
    [HideInInspector] public bool hasSpawned;
}

// Emotions
[System.Serializable]
public class EmotionSet
{
    public Sprite closedMouth;
    public Sprite openMouth;
}

[System.Serializable]
public class CharacterFaceSet
{
    public EmotionSet neutral;
    public EmotionSet happy;
    public EmotionSet sad;
    public EmotionSet scared;
    public EmotionSet puking;
    public EmotionSet dead;
    public EmotionSet angry;
    public EmotionSet reallyAngry;

    public EmotionSet GetEmotion(CustomerFaceController.Mood mood)
    {
        switch (mood)
        {
            case CustomerFaceController.Mood.Happy: return happy;
            case CustomerFaceController.Mood.Sad: return sad;
            case CustomerFaceController.Mood.Scared: return scared;
            case CustomerFaceController.Mood.Puking: return puking;
            case CustomerFaceController.Mood.Dead: return dead;
            case CustomerFaceController.Mood.Angry: return angry;
            case CustomerFaceController.Mood.ReallyAngry: return reallyAngry;
            default: return neutral;
        }
    }
}

public class CustomerGroup
{
    public List<Customer> members = new List<Customer>();
    public float waitTimer = 0f;
    public float maxWaitTime = 999f;
    public bool isLeaving = false;
}

public class TableIsland
{
    public List<SittingSpot> spots = new List<SittingSpot>();
    public bool isClosedLoop;

    public bool IsEmpty()
    {
        foreach (var spot in spots)
        {
            if (spot.isOccupied || spot.isReserved)
                return false;
        }

        return true;
    }
}

[System.Serializable]
public class CharacterSetup
{
    [Tooltip("The 3D Prefab for this character")]
    public GameObject characterPrefab;

    [Tooltip("The JSON order/profile for this character")]
    public TextAsset profileJSON;

    [Tooltip("The 6 emotion sprites for UI Dialogue")]
    public CharacterFaceSet faceSet;

    [Tooltip("Optional JSON for Dialogue Overrides (Yarn Nodes)")]
    public TextAsset overridesJSON;
}

public class CustomerSpawner : MonoBehaviour
{
    public static CustomerSpawner Instance;

    [Header("Day Configs (Index 0 = Day 1)")]
    public List<TextAsset> weeklyDayConfigs;

    [Header("Character Roster")]
    public List<CharacterSetup> characterRoster;

    private Dictionary<string, string> profileJsonMap = new Dictionary<string, string>();
    private Dictionary<string, GameObject> prefabMap = new Dictionary<string, GameObject>();
    public Dictionary<string, CharacterFaceSet> faceMap = new Dictionary<string, CharacterFaceSet>();
    public Dictionary<string, DialogueOverrideConfig> overridesMap = new Dictionary<string, DialogueOverrideConfig>();

    [Header("Spawn Logic")]
    public Transform entrancePoint;
    public Transform exitPoint;

    private List<SittingSpot> allSittingSpots = new List<SittingSpot>();

    [Header("Queue System")]
    public List<Transform> queueSpots;

    private List<CustomerGroup> queueGroups = new List<CustomerGroup>();
    private List<TableIsland> tableIslands = new List<TableIsland>();

    private DayConfig currentDay;
    private bool isShiftActive = false;
    private float shiftTimer = 0f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        foreach (var setup in characterRoster)
        {
            if (setup.profileJSON == null || setup.characterPrefab == null)
                continue;

            try
            {
                CustomerProfile p =
                    JsonUtility.FromJson<CustomerProfile>(setup.profileJSON.text);

                if (p != null && !string.IsNullOrEmpty(p.profileName))
                {
                    profileJsonMap[p.profileName] = setup.profileJSON.text;
                    prefabMap[p.profileName] = setup.characterPrefab;
                    faceMap[p.profileName] = setup.faceSet;

                    if (setup.overridesJSON != null)
                    {
                        DialogueOverrideConfig overConfig =
                            JsonUtility.FromJson<DialogueOverrideConfig>(
                                setup.overridesJSON.text);

                        overridesMap[p.profileName] = overConfig;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    $"[JSON CRASH] '{setup.profileJSON.name}': {e.Message}");
            }
        }
    }

    void Start()
    {
        allSittingSpots =
            new List<SittingSpot>(
                FindObjectsByType<SittingSpot>(FindObjectsSortMode.None));

        if (allSittingSpots.Count == 0)
        {
            Debug.LogWarning(
                "[CustomerSpawner] No Sitting Spots found in the scene! Customers will have nowhere to sit.");
        }

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

            bool hasEnds =
                newIsland.spots.Any(s => s.connectedSpots.Count <= 1) &&
                newIsland.spots.Count > 2;

            newIsland.isClosedLoop = !hasEnds;

            tableIslands.Add(newIsland);
        }

        Debug.Log(
            $"[Seating System] Initialization complete. Detected {tableIslands.Count} total isolated seating groups. " +
            $"({tableIslands.Count(t => t.isClosedLoop)} Private Tables, {tableIslands.Count(t => !t.isClosedLoop)} Open Chains).");
    }

    public void StartShift(int dayNumber)
    {
        if (isShiftActive || weeklyDayConfigs.Count == 0)
            return;

        int dayIndex =
            Mathf.Clamp(dayNumber - 1, 0, weeklyDayConfigs.Count - 1);

        TextAsset dayJson = weeklyDayConfigs[dayIndex];

        try
        {
            currentDay = JsonUtility.FromJson<DayConfig>(dayJson.text);

            if (currentDay.scheduledNPCs == null)
                currentDay.scheduledNPCs = new List<ScheduledNPC>();

            foreach (var npc in currentDay.scheduledNPCs)
                npc.hasSpawned = false;

            isShiftActive = true;
            shiftTimer = 0f;

            Debug.Log(
                $"[CustomerSpawner] Shift Started for Day {dayNumber}. Loading: {dayJson.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JSON CRASH] Day Config: {e.Message}");
        }
    }

    void Update()
    {
        ManageQueueFlow();

        bool isPaused =
            !isShiftActive ||
            Time.timeScale == 0f ||
            RestaurantUIManager.Instance.IsDialogueActive;

        float maxDuration =
            (currentDay != null && currentDay.shiftDuration > 0)
                ? currentDay.shiftDuration
                : 100f;

        if (RestaurantUIManager.Instance != null)
        {
            RestaurantUIManager.Instance.UpdateShiftTimer(
                shiftTimer,
                maxDuration,
                isPaused);
        }

        if (!isShiftActive) return;

        if (RestaurantUIManager.Instance.IsDialogueActive) return;

        shiftTimer += Time.deltaTime;

        if (shiftTimer >= currentDay.shiftDuration)
        {
            EndShift();
            return;
        }

        foreach (var npc in currentDay.scheduledNPCs)
        {
            if (!npc.hasSpawned && shiftTimer >= npc.time)
            {
                npc.hasSpawned = true;
                TrySpawnCustomerGroup(npc.profileNames);
            }
        }
    }
        private void EndShift()
    {
        if (!isShiftActive)
            return;

        isShiftActive = false;

        foreach (var group in queueGroups)
        {
            foreach (var member in group.members)
            {
                if (member != null)
                    member.Leave();
            }
        }

        queueGroups.Clear();

        if (EndOfDayManager.Instance != null)
        {
            EndOfDayManager.Instance.ShowEndOfDaySummary();
        }
    }

    public void TrySpawnCustomerGroup(List<string> profileNames)
    {
        if (profileNames == null || profileNames.Count == 0)
            return;

        int currentQueueSize = queueGroups.Sum(g => g.members.Count);

        if (currentQueueSize + profileNames.Count > queueSpots.Count)
        {
            Debug.LogWarning(
                $"<color=#FF00FF>[REJECTED]</color> Group of {profileNames.Count} arrived, but the line is too long!");
            return;
        }

        CustomerGroup newGroup = new CustomerGroup();

        Vector3 safeSpawnPos = entrancePoint.position;

        if (UnityEngine.AI.NavMesh.SamplePosition(
            entrancePoint.position,
            out UnityEngine.AI.NavMeshHit hit,
            5.0f,
            UnityEngine.AI.NavMesh.AllAreas))
        {
            safeSpawnPos = hit.position;
        }

        foreach (string pName in profileNames)
        {
            if (!profileJsonMap.ContainsKey(pName))
                continue;

            CustomerProfile profile =
                JsonUtility.FromJson<CustomerProfile>(
                    profileJsonMap[pName]);

            GameObject specificPrefab = prefabMap[pName];

            GameObject customerObj =
                Instantiate(
                    specificPrefab,
                    safeSpawnPos,
                    Quaternion.identity);

            Customer customer =
                customerObj.GetComponent<Customer>();

            customer.profile = profile;
            newGroup.members.Add(customer);

            if (profile.queueWaitTime < newGroup.maxWaitTime)
                newGroup.maxWaitTime = profile.queueWaitTime;
        }

        if (newGroup.members.Count == 0)
            return;

        List<SittingSpot> cluster =
            FindAvailableCluster(newGroup.members.Count);

        if (cluster != null)
        {
            for (int i = 0; i < newGroup.members.Count; i++)
            {
                newGroup.members[i].Initialize(
                    newGroup.members[i].profile,
                    cluster[i],
                    exitPoint);
            }
        }
        else
        {
            int queueIndexStart = currentQueueSize;

            for (int i = 0; i < newGroup.members.Count; i++)
            {
                Transform qSpot =
                    queueSpots[queueIndexStart + i];

                newGroup.members[i].InitializeQueue(
                    newGroup.members[i].profile,
                    qSpot,
                    exitPoint,
                    newGroup);
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

                    OrderManager.Instance.HandleQueueWalkout(
                        group.members[0].profile,
                        group.members[0].faceController);

                    foreach (var member in group.members)
                    {
                        member.Leave();
                    }
                }
            }

            if (group.isLeaving ||
                group.members.All(m => m == null || m.IsLeaving()))
            {
                queueGroups.RemoveAt(i);
                queueShifted = true;
            }
        }

        for (int i = 0; i < queueGroups.Count; i++)
        {
            CustomerGroup group = queueGroups[i];

            List<SittingSpot> cluster =
                FindAvailableCluster(group.members.Count);

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
                        member.UpdateQueueSpot(
                            queueSpots[spotIndex]);

                        spotIndex++;
                    }
                }
            }
        }
    }

    private List<SittingSpot> FindAvailableCluster(
        int requiredSize)
    {
        foreach (var table in tableIslands)
        {
            if (table.isClosedLoop && !table.IsEmpty())
                continue;

            List<SittingSpot> availableSeats =
                table.spots
                    .Where(s => !s.isOccupied && !s.isReserved)
                    .ToList();

            if (availableSeats.Count < requiredSize)
                continue;

            HashSet<SittingSpot> globalVisited =
                new HashSet<SittingSpot>();

            foreach (var startSeat in availableSeats)
            {
                if (globalVisited.Contains(startSeat))
                    continue;

                List<SittingSpot> currentCluster =
                    new List<SittingSpot>();

                Queue<SittingSpot> queue =
                    new Queue<SittingSpot>();

                HashSet<SittingSpot> localVisited =
                    new HashSet<SittingSpot>();

                queue.Enqueue(startSeat);
                localVisited.Add(startSeat);
                globalVisited.Add(startSeat);

                while (queue.Count > 0 &&
                       currentCluster.Count < requiredSize)
                {
                    SittingSpot current = queue.Dequeue();
                    currentCluster.Add(current);

                    if (currentCluster.Count == requiredSize)
                        return currentCluster;

                    foreach (var neighbor in current.connectedSpots)
                    {
                        if (neighbor != null &&
                            availableSeats.Contains(neighbor) &&
                            !localVisited.Contains(neighbor))
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

    public CharacterFaceSet GetCustomerFaceSet(
        string profileName)
    {
        if (faceMap.ContainsKey(profileName))
            return faceMap[profileName];

        return null;
    }
}