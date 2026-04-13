using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class CookingPreference
{
    public string ingredientName;
    public string desiredState; 
    public int bonusPoints;     
}

[System.Serializable]
public class TipThreshold { public float scorePercentage; public int tipAmount; }

[System.Serializable]
public class PaymentSettings { public int pricePerIngredient = 5; public List<TipThreshold> tipThresholds; }[System.Serializable]
public class ScoreSettings { public int baseScore=100; public int unrequestedIngredient=-20; public int missingIngredient=-20; public int raw=-75; public int cooked=0; public int burnt=-40; public int onFire=-100; public int maxWaitTime=60; public int waitPenaltyPerSecond=-1; public int maxFreshness=30; public int stalePenaltyPerSecond=-1; }

[System.Serializable]
public class CustomerReactions { public string success="Gracias!"; public string wrongOrder="Esto no es lo que pedi."; public string foodRaw="Crudo!"; public string foodCooked="Perfecto!"; public string foodBurnt="Quemado!"; public string foodOnFire="FUEGO!"; public string walkout = "¡Me voy!"; public List<CustomReaction> customReactions = new List<CustomReaction>(); }

[System.Serializable]
public class CustomReaction { public string conditionName; public List<string> requiredIngredients; public string reaction; public int scoreModifier; }

[System.Serializable]
public class CustomerProfile { 
    public string profileName; 
    public float idealTemp; 
    public int minTotalItems; 
    public int maxTotalItems; 
    public float walkoutTime = 120f; 
    public float queueWaitTime = 45f; // <--- NEW: How long they wait in line
    public ScoreSettings scoreSettings; 
    public PaymentSettings paymentSettings; 
    public List<IngredientRule> ingredients; 
    public List<ItemGroup> groupedItems; 
    public List<CookingPreference> cookingPreferences; 
    public CustomerReactions reactions; 
}

[System.Serializable]
public class IngredientRule { public string name; public int min; public int max; public int weight; }[System.Serializable]
public class ItemGroup { public List<string> items; }

public class ActiveOrder { public CustomerProfile profile; public List<string> expectedIngredients; public float orderStartTime; }
// --- THE MANAGER ---
public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance;[Header("Debug Testing")]
    [Tooltip("If true, deletes saved score and money every time you hit play.")]
    public bool resetDataOnStart = true; 

    public int totalSavedScore = 0;
    public int totalSavedMoney = 0; 
    public int walkoutPenalty = -150; 

    private Dictionary<TableSpot, ActiveOrder> activeOrders = new Dictionary<TableSpot, ActiveOrder>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // --- NEW: Clears old testing data so you start at 0 ---
        if (resetDataOnStart)
        {
            PlayerPrefs.DeleteKey("RestaurantTotalScore");
            PlayerPrefs.DeleteKey("RestaurantTotalMoney");
        }

        totalSavedScore = PlayerPrefs.GetInt("RestaurantTotalScore", 0);
        totalSavedMoney = PlayerPrefs.GetInt("RestaurantTotalMoney", 0);

        if (RestaurantUIManager.Instance != null)
        {
            RestaurantUIManager.Instance.UpdateScore(totalSavedScore);
            RestaurantUIManager.Instance.UpdateMoney(totalSavedMoney);
        }
    }

    void Update()
    {
        List<TableSpot> walkouts = new List<TableSpot>();
        
        foreach (var kvp in activeOrders)
        {
            if (Time.time - kvp.Value.orderStartTime > kvp.Value.profile.walkoutTime)
            {
                walkouts.Add(kvp.Key);
            }
        }

        foreach (var table in walkouts)
        {
            HandleWalkout(table);
        }
    }
    
        public void HandleQueueWalkout(CustomerProfile profile)
    {
        totalSavedScore += walkoutPenalty;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();

        RestaurantUIManager.Instance.UpdateScore(totalSavedScore);
        RestaurantUIManager.Instance.ShowDialogue(profile.profileName, profile.reactions.walkout);

        Debug.LogWarning($"[Walkout] {profile.profileName} left the queue! Penalty: {walkoutPenalty}");
    }

    private void HandleWalkout(TableSpot table)
    {
        ActiveOrder order = activeOrders[table];
        
        totalSavedScore += walkoutPenalty;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();

        RestaurantUIManager.Instance.UpdateScore(totalSavedScore);
        RestaurantUIManager.Instance.ShowDialogue(order.profile.profileName, order.profile.reactions.walkout);

        Debug.LogWarning($"[Walkout] {order.profile.profileName} left! Penalty: {walkoutPenalty}");

        if (table.linkedSittingSpot.currentCustomer != null)
        {
            table.linkedSittingSpot.currentCustomer.Leave(); // Tell the angry pill to walk away
        }
        activeOrders.Remove(table);
    }

    public void GenerateOrderForTable(SittingSpot sittingSpot, CustomerProfile profile)
    {
        if (sittingSpot.linkedTableSpot == null) return;
        List<string> generatedOrder = CreateOrderList(profile);

        ActiveOrder newOrder = new ActiveOrder 
        { 
            profile = profile, 
            expectedIngredients = generatedOrder, 
            orderStartTime = Time.time 
        };
        
        activeOrders[sittingSpot.linkedTableSpot] = newOrder;
        Debug.Log($"[OrderManager] {profile.profileName} sat down. Order: {string.Join(", ", generatedOrder)}");
    }

        // --- ADD THESE NEW METHODS ANYWHERE INSIDE THE CLASS ---
    public bool HasActiveOrder(TableSpot table) => activeOrders.ContainsKey(table);

    public string GetOrderText(TableSpot table)
    {
        if (!activeOrders.ContainsKey(table)) return "Nada";

        List<string> ingredients = activeOrders[table].expectedIngredients;
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (string item in ingredients)
        {
            if (counts.ContainsKey(item)) counts[item]++;
            else counts[item] = 1;
        }

        string breadText = "";
        string breadName = "Pan"; 
        int breadCount = 0;
        List<string> breadKeys = new List<string>();

        // Find anything that counts as bread
        foreach (var key in counts.Keys)
        {
            if (key.IndexOf("Pan", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                key.IndexOf("Bun", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (breadCount == 0) breadName = key; 
                breadCount += counts[key];
                breadKeys.Add(key);
            }
        }

        // Determine Bread Text (Always comes first)
        if (breadCount == 0) 
        {
            breadText = "Sin pan";
        }
        else if (breadCount == 1) 
        {
            breadText = $"Solo un pedazo de {breadName.ToLower()}";
        }
        else if (breadCount == 2) 
        {
            // If it's standard bread, say "Pan normal". If it's a special bread, just say the name!
            if (breadName.Equals("Pan", System.StringComparison.OrdinalIgnoreCase) || 
                breadName.Equals("Bun", System.StringComparison.OrdinalIgnoreCase))
            {
                breadText = "Pan normal";
            }
            else
            {
                breadText = breadName; // e.g. "Pan asqueroso" or "Pan integral"
            }
        }
        else 
        {
            breadText = $"{GetMultiplier(breadCount)} {breadName.ToLower()}";
        }

        foreach (var bKey in breadKeys) counts.Remove(bKey);

        List<string> orderParts = new List<string>();
        orderParts.Add(breadText); 

        foreach (var kvp in counts)
        {
            if (kvp.Value == 1) orderParts.Add(kvp.Key);
            else orderParts.Add($"{GetMultiplier(kvp.Value)} {kvp.Key}");
        }

        if (orderParts.Count == 1) return orderParts[0];

        string lastPart = orderParts[orderParts.Count - 1];
        orderParts.RemoveAt(orderParts.Count - 1);
        
        return string.Join(", ", orderParts) + " y " + lastPart;
    }

        private string GetMultiplier(int count)
    {
        switch (count)
        {
            case 2: return "Doble";
            case 3: return "Triple";
            case 4: return "Cuádruple";
            case 5: return "Quíntuple";
            case 6: return "Séxtuple";
            default: return count.ToString() + "x";
        }
    }

    public float GetWaitTimePercent(TableSpot table)
    {
        if (activeOrders.ContainsKey(table))
        {
            ActiveOrder order = activeOrders[table];
            float waitTime = Time.time - order.orderStartTime;
            return Mathf.Clamp01(waitTime / order.profile.walkoutTime);
        }
        return 0f;
    }

    // Dry-run simulation to see if a table perfectly accepts a burger without triggering side effects
    public bool WouldAcceptBurger(TableSpot table, AssembledBurger burger)
    {
        if (!activeOrders.ContainsKey(table)) return false; 
        
        ActiveOrder order = activeOrders[table];
        List<string> servedNames = burger.GetIngredientNames();
        List<string> expectedNames = new List<string>(order.expectedIngredients);

        List<string> unrequestedItems = new List<string>();
        foreach (string served in servedNames)
        {
            if (expectedNames.Contains(served)) expectedNames.Remove(served);
            else unrequestedItems.Add(served);
        }
        
        int missingCount = expectedNames.Count;

        // Check custom reactions that might override standard rules
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
                    foreach (string req in custom.requiredIngredients) 
                        if (unrequestedItems.Contains(req)) unrequestedItems.Remove(req);
                }
            }
        }
        // If there are no missing or unrequested items, it's a perfect match
        return missingCount == 0 && unrequestedItems.Count == 0;
    }

    public string GetActiveProfileName(TableSpot table)
    {
        if (activeOrders.ContainsKey(table)) return activeOrders[table].profile.profileName;
        return "Unknown";
    }

    public void AddMoney(int amount)
    {
        totalSavedMoney += amount;
        PlayerPrefs.SetInt("RestaurantTotalMoney", totalSavedMoney);
        PlayerPrefs.Save();
        RestaurantUIManager.Instance.UpdateMoney(totalSavedMoney);
    }

public bool TryServeFood(TableSpot table, AssembledBurger burger, out int moneyToSpawn, out string customerDialogue)
    {
        moneyToSpawn = 0;
        customerDialogue = "";
        if (!activeOrders.ContainsKey(table)) return false; 

        ActiveOrder order = activeOrders[table];
        List<string> servedNames = burger.GetIngredientNames();
        List<string> expectedNames = new List<string>(order.expectedIngredients);

        System.Text.StringBuilder evalLog = new System.Text.StringBuilder();
        evalLog.AppendLine($"\n--- EVALUATION LOG: {order.profile.profileName} ---");
        
        int score = order.profile.scoreSettings.baseScore;
        evalLog.AppendLine($"Base Score: {score}");

        float waitTime = Time.time - order.orderStartTime;
        float freshness = Time.time - burger.assemblyTime;

        if (waitTime > order.profile.scoreSettings.maxWaitTime) 
        {
            int penalty = Mathf.RoundToInt((waitTime - order.profile.scoreSettings.maxWaitTime) * order.profile.scoreSettings.waitPenaltyPerSecond);
            score += penalty;
            evalLog.AppendLine($"Wait Time Penalty: {penalty} ({waitTime:F1}s total wait)");
        }
        if (freshness > order.profile.scoreSettings.maxFreshness) 
        {
            int penalty = Mathf.RoundToInt((freshness - order.profile.scoreSettings.maxFreshness) * order.profile.scoreSettings.stalePenaltyPerSecond);
            score += penalty;
            evalLog.AppendLine($"Stale Food Penalty: {penalty} ({freshness:F1}s sitting out)");
        }

        List<string> unrequestedItems = new List<string>();
        foreach (string served in servedNames)
        {
            if (expectedNames.Contains(served)) expectedNames.Remove(served);
            else unrequestedItems.Add(served);
        }
        int missingCount = expectedNames.Count;

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
                    evalLog.AppendLine($"Custom Reaction '{custom.conditionName}': {custom.scoreModifier}");
                    
                    foreach (string req in custom.requiredIngredients) 
                        if (unrequestedItems.Contains(req)) unrequestedItems.Remove(req);
                }
            }
        }

        if (unrequestedItems.Count > 0) 
        {
            int penalty = unrequestedItems.Count * order.profile.scoreSettings.unrequestedIngredient;
            score += penalty;
            evalLog.AppendLine($"Unrequested Items Penalty: {penalty} ({string.Join(", ", unrequestedItems)})");
        }
        if (missingCount > 0) 
        {
            int penalty = missingCount * order.profile.scoreSettings.missingIngredient;
            score += penalty;
            evalLog.AppendLine($"Missing Items Penalty: {penalty} ({string.Join(", ", expectedNames)})");
        }

        CookableItem masterCookable = burger.GetComponent<CookableItem>();
        float addedHeat = masterCookable != null ? masterCookable.currentHeatProgress : 0f;
        bool isOnFire = masterCookable != null ? masterCookable.isOnFire : false;
        
        bool unwantedRaw = false; 
        bool unwantedBurnt = false;

        foreach (var ing in burger.ingredients)
        {
            if (ing.isCookable)
            {
                float finalHeat = ing.heatProgressWhenAssembled + addedHeat;
                
                // Determine the actual state of the ingredient
                string actualState = "Raw";
                if (isOnFire) actualState = "OnFire";
                else if (finalHeat >= ing.timeToBurn) actualState = "Burnt";
                else if (finalHeat >= ing.timeToCook) actualState = "Cooked";

                // Check if the customer specifically asked for it this way
                CookingPreference pref = order.profile.cookingPreferences?.FirstOrDefault(p => p.ingredientName == ing.name);

                if (actualState == "OnFire") 
                { 
                    score += order.profile.scoreSettings.onFire; 
                    evalLog.AppendLine($"Fire Penalty: {order.profile.scoreSettings.onFire} (Ingredient: {ing.name})"); 
                }
                else if (pref != null && pref.desiredState == actualState)
                {
                    // They wanted it like this! Give bonus and skip penalties.
                    score += pref.bonusPoints;
                    evalLog.AppendLine($"Preference Met: +{pref.bonusPoints} (Wanted {pref.desiredState} {ing.name})");
                }
                else
                {
                    // Apply Standard Rules
                    if (actualState == "Burnt") 
                    { 
                        score += order.profile.scoreSettings.burnt; 
                        unwantedBurnt = true; 
                        evalLog.AppendLine($"Burnt Penalty: {order.profile.scoreSettings.burnt} (Ingredient: {ing.name})"); 
                    }
                    else if (actualState == "Cooked") 
                    { 
                        score += order.profile.scoreSettings.cooked; 
                        evalLog.AppendLine($"Cooked/Toasted: {order.profile.scoreSettings.cooked} (Ingredient: {ing.name})"); 
                    }
                    else // Raw
                    { 
                        // ONLY penalize Meat if it is raw. Veggies and Bread are naturally fine raw!
                        if (ing.category == CookableItem.FoodCategory.Meat || ing.category == CookableItem.FoodCategory.AssembledBurger)
                        {
                            score += order.profile.scoreSettings.raw; 
                            unwantedRaw = true; 
                            evalLog.AppendLine($"Raw Meat Penalty: {order.profile.scoreSettings.raw} (Ingredient: {ing.name})"); 
                        }
                        else
                        {
                            evalLog.AppendLine($"Raw Normal: 0 (Ingredient: {ing.name} is {ing.category})");
                        }
                    }
                }
            }
        }

        if (masterCookable != null)
        {
            float tempDiff = Mathf.Abs(masterCookable.currentTemperature - order.profile.idealTemp);
            if (tempDiff > 20f) 
            {
                score -= 20; 
                evalLog.AppendLine($"Bad Temp Penalty: -20 (Target {order.profile.idealTemp}°, was {masterCookable.currentTemperature:F1}°)");
            }
        }

        evalLog.AppendLine($"FINAL CALCULATED SCORE: {score}");

        // --- DIALOGUE DETERMINATION AND LOGGING ---
        evalLog.AppendLine("\n--- DIALOGUE DETERMINATION ---");
        
        if (isOnFire) 
        { 
            customerDialogue = order.profile.reactions.foodOnFire; 
            evalLog.AppendLine("Reason: Food was literally on fire."); 
        }
        else if (!string.IsNullOrEmpty(customReactText)) 
        { 
            customerDialogue = customReactText; 
            evalLog.AppendLine("Reason: A Custom Reaction condition was met."); 
        }
        else if (unwantedBurnt) 
        { 
            customerDialogue = order.profile.reactions.foodBurnt; 
            evalLog.AppendLine("Reason: At least one cookable ingredient was burnt (and they didn't ask for it!)."); 
        }
        else if (unwantedRaw) 
        { 
            customerDialogue = order.profile.reactions.foodRaw; 
            evalLog.AppendLine("Reason: Meat was raw (and they didn't ask for it!)."); 
        }
        else if (missingCount > 0 || unrequestedItems.Count > 0)
        {
            string expectedStr = string.Join(", ", order.expectedIngredients);
            string servedStr = string.Join(", ", servedNames);
            customerDialogue = order.profile.reactions.wrongOrder.Replace("{ORDER}", expectedStr).Replace("{SERVED}", servedStr);
            evalLog.AppendLine("Reason: Wrong ingredients. (Burger Rejected!)");
            
            Debug.Log(evalLog.ToString());
            return false; 
        }
        else 
        { 
            customerDialogue = order.profile.reactions.success; 
            evalLog.AppendLine("Reason: Perfect match! No major complaints."); 
        }

        // --- MONEY CALCULATION ---
        float scorePercent = (float)(score - order.profile.scoreSettings.baseScore) / order.profile.scoreSettings.baseScore;
        int flatMoney = order.expectedIngredients.Count * order.profile.paymentSettings.pricePerIngredient;
        int tip = 0;

        var sortedTips = order.profile.paymentSettings.tipThresholds.OrderByDescending(t => t.scorePercentage).ToList();
        foreach (var t in sortedTips)
        {
            if (scorePercent >= t.scorePercentage)
            {
                tip = t.tipAmount;
                break;
            }
        }

        moneyToSpawn = Mathf.Max(0, flatMoney + tip); 
        
        evalLog.AppendLine("\n--- PAYMENT CALCULATION ---");
        evalLog.AppendLine($"Score Percentage vs Base: {(scorePercent*100):F0}%");
        evalLog.AppendLine($"Base Food Value: ${flatMoney}");
        evalLog.AppendLine($"Calculated Tip: ${tip}");
        evalLog.AppendLine($"Total Payout: ${moneyToSpawn}");
        evalLog.AppendLine("----------------------------------------");

        Debug.Log(evalLog.ToString());

        totalSavedScore += score;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();
        RestaurantUIManager.Instance.UpdateScore(totalSavedScore);

        activeOrders.Remove(table);
        return true; 
    }

    // --- (Keep CreateOrderList and TryAddItemWithGroup the same as previous) ---
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