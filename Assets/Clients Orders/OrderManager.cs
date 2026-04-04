using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// --- DATA CLASSES ---
[System.Serializable]
public class ScoreSettings
{
    public int baseScore = 100;
    public int unrequestedIngredient = -20;
    public int missingIngredient = -20;
    public int raw = -75;
    public int cooked = 0;
    public int burnt = -40;
    public int onFire = -100;

    [Header("Time Penalties")]
    public int maxWaitTime = 60; // Seconds before customer gets angry
    public int waitPenaltyPerSecond = -1; // Score lost per second waiting over max
    public int maxFreshness = 30; // Seconds before a built burger goes stale
    public int stalePenaltyPerSecond = -1; // Score lost per second over max freshness
}

[System.Serializable]
public class CustomerReactions
{
    public string success = "Gracias!";
    public string wrongOrder = "Esto no es lo que pedi, yo pedi {ORDER} y esto es {SERVED}.";
    public string foodRaw = "¡Esto esta crudo!";
    public string foodCooked = "¡Está perfecto!";
    public string foodBurnt = "¡Esto está quemado!";
    public string foodOnFire = "¡AHHH FUEGO!";
    public List<CustomReaction> customReactions = new List<CustomReaction>();
}[System.Serializable]
public class CustomReaction
{
    public string conditionName;
    public List<string> requiredIngredients; 
    public string reaction;
    public int scoreModifier; 
}

[System.Serializable]
public class CustomerProfile
{
    public string profileName;
    public float idealTemp; 
    public int minTotalItems;
    public int maxTotalItems;
    public ScoreSettings scoreSettings; 
    public List<IngredientRule> ingredients;
    public List<ItemGroup> groupedItems;
    public CustomerReactions reactions; 
}

[System.Serializable]
public class IngredientRule
{
    public string name;
    public int min;
    public int max;
    public int weight;
}[System.Serializable]
public class ItemGroup
{
    public List<string> items; 
}

public class ActiveOrder
{
    public CustomerProfile profile;
    public List<string> expectedIngredients;
    public float orderStartTime; // NEW: Track wait time
}

public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance;

    [Header("Score System")]
    public int totalSavedScore = 0;

    private Dictionary<TableSpot, ActiveOrder> activeOrders = new Dictionary<TableSpot, ActiveOrder>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        totalSavedScore = PlayerPrefs.GetInt("RestaurantTotalScore", 0);
    }

    public void GenerateOrderForTable(SittingSpot sittingSpot, CustomerProfile profile)
    {
        if (sittingSpot.linkedTableSpot == null) return;

        List<string> generatedOrder = CreateOrderList(profile);

        ActiveOrder newOrder = new ActiveOrder
        {
            profile = profile,
            expectedIngredients = generatedOrder,
            orderStartTime = Time.time // RECORD ORDER TIME
        };

        activeOrders[sittingSpot.linkedTableSpot] = newOrder;
        Debug.Log($"[OrderManager] {profile.profileName} sat down. Order: {string.Join(", ", generatedOrder)}");
    }

    public bool TryServeFood(TableSpot table, AssembledBurger burger)
    {
        if (!activeOrders.ContainsKey(table)) return false; 

        ActiveOrder order = activeOrders[table];
        List<string> servedNames = burger.GetIngredientNames();
        List<string> expectedNames = new List<string>(order.expectedIngredients);

        int score = order.profile.scoreSettings.baseScore;

        // --- NEW: TIME CALCULATIONS ---
        float waitTime = Time.time - order.orderStartTime;
        float freshness = Time.time - burger.assemblyTime;

        if (waitTime > order.profile.scoreSettings.maxWaitTime)
        {
            int waitPenalty = Mathf.RoundToInt((waitTime - order.profile.scoreSettings.maxWaitTime) * order.profile.scoreSettings.waitPenaltyPerSecond);
            score += waitPenalty;
            Debug.Log($"[Time Penalty] Client waited {waitTime:F1}s! Penalty: {waitPenalty} pts.");
        }

        if (freshness > order.profile.scoreSettings.maxFreshness)
        {
            int freshPenalty = Mathf.RoundToInt((freshness - order.profile.scoreSettings.maxFreshness) * order.profile.scoreSettings.stalePenaltyPerSecond);
            score += freshPenalty;
            Debug.Log($"[Freshness Penalty] Burger sat out for {freshness:F1}s! Penalty: {freshPenalty} pts.");
        }
        else
        {
            Debug.Log($"[Freshness] Nice! Burger was served fresh in {freshness:F1}s.");
        }

        // 1. MATCH INGREDIENTS
        List<string> unrequestedItems = new List<string>();
        foreach (string served in servedNames)
        {
            if (expectedNames.Contains(served)) expectedNames.Remove(served);
            else unrequestedItems.Add(served);
        }
        int missingCount = expectedNames.Count;

        // 2. CHECK CUSTOM REACTIONS
        string customReactText = "";
        if (order.profile.reactions.customReactions != null)
        {
            foreach (var custom in order.profile.reactions.customReactions)
            {
                List<string> tempServed = new List<string>(servedNames);
                bool hasAll = true;
                foreach (string req in custom.requiredIngredients)
                {
                    if (tempServed.Contains(req)) tempServed.Remove(req);
                    else { hasAll = false; break; }
                }

                if (hasAll && custom.requiredIngredients.Count > 0)
                {
                    customReactText = custom.reaction;
                    score += custom.scoreModifier;

                    foreach (string req in custom.requiredIngredients)
                    {
                        if (unrequestedItems.Contains(req)) unrequestedItems.Remove(req);
                    }
                }
            }
        }

        // 3. APPLY GENERIC INGREDIENT PENALTIES
        score += unrequestedItems.Count * order.profile.scoreSettings.unrequestedIngredient;
        score += missingCount * order.profile.scoreSettings.missingIngredient;

        // 4. CALCULATE INDIVIDUAL COOKING STATES
        CookableItem masterCookable = burger.GetComponent<CookableItem>();
        float addedHeat = masterCookable != null ? masterCookable.currentHeatProgress : 0f;
        bool isOnFire = masterCookable != null ? masterCookable.isOnFire : false;

        bool anyRaw = false;
        bool anyBurnt = false;

        foreach (var ing in burger.ingredients)
        {
            if (ing.isCookable)
            {
                float finalHeat = ing.heatProgressWhenAssembled + addedHeat;

                if (isOnFire) {
                    score += order.profile.scoreSettings.onFire;
                }
                else if (finalHeat >= ing.timeToBurn) {
                    score += order.profile.scoreSettings.burnt;
                    anyBurnt = true;
                }
                else if (finalHeat >= ing.timeToCook) {
                    score += order.profile.scoreSettings.cooked; 
                }
                else {
                    score += order.profile.scoreSettings.raw;
                    anyRaw = true;
                }
            }
        }

        if (masterCookable != null)
        {
            float tempDiff = Mathf.Abs(masterCookable.currentTemperature - order.profile.idealTemp);
            if (tempDiff > 20f) { score -= 20; }
        }

        // 5. DETERMINE FINAL CUSTOMER DIALOGUE
        string finalDialogue = "";
        if (isOnFire) finalDialogue = order.profile.reactions.foodOnFire;
        else if (!string.IsNullOrEmpty(customReactText)) finalDialogue = customReactText;
        else if (anyBurnt) finalDialogue = order.profile.reactions.foodBurnt;
        else if (anyRaw) finalDialogue = order.profile.reactions.foodRaw;
        else if (missingCount > 0 || unrequestedItems.Count > 0)
        {
            string expectedStr = string.Join(", ", order.expectedIngredients);
            string servedStr = string.Join(", ", servedNames);
            finalDialogue = order.profile.reactions.wrongOrder.Replace("{ORDER}", expectedStr).Replace("{SERVED}", servedStr);
        }
        else finalDialogue = order.profile.reactions.success;

        Debug.Log($"[Customer {order.profile.profileName}]: {finalDialogue} | Score Change: {score}");

        // 6. SAVE AND ACCEPT FOOD
        totalSavedScore += score;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();

        activeOrders.Remove(table);
        return true; 
    }

    // ---- CreateOrderList and TryAddItemWithGroup remain exactly the same as previously generated ----
    private List<string> CreateOrderList(CustomerProfile profile)
    {
        List<string> order = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (var rule in profile.ingredients) counts[rule.name] = 0;
        foreach (var rule in profile.ingredients)
        {
            for (int i = 0; i < rule.min; i++) { order.Add(rule.name); counts[rule.name]++; }
        }

        int targetTotal = Mathf.Max(Random.Range(profile.minTotalItems, profile.maxTotalItems + 1), order.Count); 
        int failsafe = 0; 

        while (order.Count < targetTotal && failsafe < 100)
        {
            failsafe++;
            List<IngredientRule> validPool = new List<IngredientRule>();
            int totalWeight = 0;

            foreach (var rule in profile.ingredients)
            {
                if (counts[rule.name] < rule.max && rule.weight > 0)
                {
                    validPool.Add(rule);
                    totalWeight += rule.weight;
                }
            }

            if (validPool.Count == 0) break; 

            int randomRoll = Random.Range(0, totalWeight);
            string chosenItem = "";

            foreach (var rule in validPool)
            {
                randomRoll -= rule.weight;
                if (randomRoll < 0) { chosenItem = rule.name; break; }
            }

            TryAddItemWithGroup(chosenItem, profile, order, counts);
        }

        return order;
    }

    private void TryAddItemWithGroup(string itemToAdd, CustomerProfile profile, List<string> order, Dictionary<string, int> counts)
    {
        ItemGroup group = profile.groupedItems.FirstOrDefault(g => g.items.Contains(itemToAdd));
        if (group != null)
        {
            foreach (string gItem in group.items)
            {
                var rule = profile.ingredients.FirstOrDefault(i => i.name == gItem);
                if (rule != null && counts[gItem] < rule.max) { order.Add(gItem); counts[gItem]++; }
            }
        }
        else
        {
            var rule = profile.ingredients.FirstOrDefault(i => i.name == itemToAdd);
            if (rule != null && counts[itemToAdd] < rule.max) { order.Add(itemToAdd); counts[itemToAdd]++; }
        }
    }
}