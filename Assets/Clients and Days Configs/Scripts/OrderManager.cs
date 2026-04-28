using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class CookingPreference { public string ingredientName; public string desiredState; public int bonusPoints; }

[System.Serializable]
public class TipThreshold { public float scorePercentage; public int tipAmount; }

[System.Serializable]
public class PaymentSettings { public int pricePerIngredient = 5; public List<TipThreshold> tipThresholds; }

[System.Serializable]
public class ScoreSettings { 
    public int baseScore = 100; 
    public int unrequestedIngredient = -20; 
    public int missingIngredient = -20; 
    public int raw = -75; 
    public int cooked = 0; 
    public int burnt = -40; 
    public int onFire = -100; 
    public int maxWaitTime = 60; 
    public int waitPenaltyPerSecond = -1; 
    public int maxFreshness = 30; 
    public int stalePenaltyPerSecond = -1;
    public int dirtyPlate = -50; // <--- NEW: Dirty plate penalty!
}

[System.Serializable]
public class CustomerReactions { 
    public string success = "Gracias!"; 
    public string wrongOrder = "Esto no es lo que pedi."; 
    public string foodRaw = "Crudo!"; 
    public string foodCooked = "Perfecto!"; 
    public string foodBurnt = "Quemado!"; 
    public string foodOnFire = "FUEGO!"; 
    public string dirtyPlate = "¡Qué asco, este plato está sucio!"; 
    public string walkout = "¡Me voy!"; 
    public List<CustomReaction> customReactions = new List<CustomReaction>(); 
}

[System.Serializable]
public class CustomReaction { public string conditionName; public List<string> requiredIngredients; public string reaction; public int scoreModifier; }

[System.Serializable]
public class ItemSizeWeight { public int size; public int weight; } // <--- NEW: Balances burger sizes

[System.Serializable]
public class CustomerProfile { 
    public string profileName; 
    public float idealTemp; 
    public float walkoutTime = 120f; 
    public float queueWaitTime = 45f; 
    
    public ScoreSettings scoreSettings; 
    public PaymentSettings paymentSettings; 
    
    public List<ItemSizeWeight> burgerSizeWeights; // <--- NEW: Replaces min/max items
    public List<IngredientRule> ingredients; // For the Burger
    public List<IngredientRule> sideItems;   // For Drinks and Fries (Evaluated independently)
    
    public List<ItemGroup> groupedItems; 
    public List<CookingPreference> cookingPreferences; 
    public CustomerReactions reactions; 
}

[System.Serializable]
public class IngredientRule { public string name; public int min; public int max; public int weight; }
[System.Serializable]
public class ItemGroup { public List<string> items; }

public class ActiveOrder { public CustomerProfile profile; public List<string> expectedIngredients; public float orderStartTime; }

// --- THE MANAGER ---
public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance;

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
            if (Time.time - kvp.Value.orderStartTime > kvp.Value.profile.walkoutTime) walkouts.Add(kvp.Key);
        }
        foreach (var table in walkouts) HandleWalkout(table);
    }
    
    public void HandleQueueWalkout(CustomerProfile profile)
    {
        totalSavedScore += walkoutPenalty;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();
        RestaurantUIManager.Instance.UpdateScore(totalSavedScore);
        RestaurantUIManager.Instance.ShowDialogue(profile.profileName, profile.reactions.walkout);
    }

    private void HandleWalkout(TableSpot table)
    {
        ActiveOrder order = activeOrders[table];
        totalSavedScore += walkoutPenalty;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();
        RestaurantUIManager.Instance.UpdateScore(totalSavedScore);
        RestaurantUIManager.Instance.ShowDialogue(order.profile.profileName, order.profile.reactions.walkout);

        if (table.linkedSittingSpot.currentCustomer != null) table.linkedSittingSpot.currentCustomer.Leave();
        activeOrders.Remove(table);
    }

    public void GenerateOrderForTable(SittingSpot sittingSpot, CustomerProfile profile)
    {
        if (sittingSpot.linkedTableSpot == null) return;
        List<string> generatedOrder = CreateOrderList(profile);

        ActiveOrder newOrder = new ActiveOrder { 
            profile = profile, 
            expectedIngredients = generatedOrder, 
            orderStartTime = Time.time 
        };
        activeOrders[sittingSpot.linkedTableSpot] = newOrder;
    }

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

        string breadText = "Sin pan";
        string breadName = "Pan"; 
        int breadCount = 0;
        List<string> breadKeys = new List<string>();

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

        if (breadCount == 1) breadText = $"Solo un pedazo de {breadName.ToLower()}";
        else if (breadCount == 2) breadText = "Pan normal";
        else if (breadCount > 2) breadText = $"{GetMultiplier(breadCount)} {breadName.ToLower()}";

        foreach (var bKey in breadKeys) counts.Remove(bKey);

        List<string> orderParts = new List<string>();
        if (breadCount > 0 || counts.Count == 0) orderParts.Add(breadText); 

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
        switch (count) {
            case 2: return "Doble"; case 3: return "Triple"; case 4: return "Cuádruple"; case 5: return "Quíntuple";
            default: return count.ToString() + "x";
        }
    }

    public float GetWaitTimePercent(TableSpot table)
    {
        if (activeOrders.ContainsKey(table)) {
            ActiveOrder order = activeOrders[table];
            return Mathf.Clamp01((Time.time - order.orderStartTime) / order.profile.walkoutTime);
        }
        return 0f;
    }

    // --- NEW: Helper to extract EVERYTHING off the plate ---
    private List<string> GetEverythingOnPlate(PlateItem plate, out List<SavedIngredient> cookableItemsToEval, out float masterBurgerTemp)
    {
        List<string> servedNames = new List<string>();
        cookableItemsToEval = new List<SavedIngredient>();
        masterBurgerTemp = -1f;

        foreach (Rigidbody rb in plate.GetAttachedItems())
        {
            if (rb == null) continue;

            // 1. Is it a Burger?
            AssembledBurger burger = rb.GetComponent<AssembledBurger>();
            if (burger != null)
            {
                servedNames.AddRange(burger.GetIngredientNames());
                cookableItemsToEval.AddRange(burger.ingredients);
                
                CookableItem masterC = rb.GetComponent<CookableItem>();
                if (masterC != null) masterBurgerTemp = masterC.currentTemperature;
                continue;
            }

            // 2. Is it a Fries Box? (Evaluated as an average)
            VesselBase vessel = rb.GetComponent<VesselBase>();
            if (vessel != null)
            {
                GrabbableItem grab = rb.GetComponent<GrabbableItem>();
                if (grab != null && grab.itemDefinition != null) servedNames.Add(grab.itemDefinition.itemName);
                
                if (vessel.spawnedItems.Count > 0)
                {
                    float avgHeat = 0;
                    bool anyFire = false;
                    CookableItem.FoodCategory cat = CookableItem.FoodCategory.Veggie;
                    float tCook = 10, tBurn = 20;

                    foreach(GameObject fry in vessel.spawnedItems)
                    {
                        CookableItem ci = fry.GetComponent<CookableItem>();
                        if (ci) {
                            avgHeat += ci.currentHeatProgress;
                            if (ci.isOnFire) anyFire = true;
                            cat = ci.category; tCook = ci.timeToCook; tBurn = ci.timeToBurn;
                        }
                    }
                    avgHeat /= vessel.spawnedItems.Count;
                    
                    SavedIngredient simFry = new SavedIngredient {
                        name = grab != null && grab.itemDefinition != null ? grab.itemDefinition.itemName : "Papas Fritas",
                        isCookable = true,
                        category = cat,
                        heatProgressWhenAssembled = avgHeat,
                        timeToCook = tCook,
                        timeToBurn = tBurn
                    };
                    if (anyFire) simFry.heatProgressWhenAssembled = 9999f; // Force fire flag
                    cookableItemsToEval.Add(simFry);
                }
                continue;
            }

            // 3. Is it a Drink or simple side?
            GrabbableItem simpleItem = rb.GetComponent<GrabbableItem>();
            if (simpleItem != null && simpleItem.itemDefinition != null)
            {
                servedNames.Add(simpleItem.itemDefinition.itemName);
            }
        }
        return servedNames;
    }

    public bool WouldAcceptOrder(TableSpot table, PlateItem plate)
    {
        if (!activeOrders.ContainsKey(table)) return false; 
        ActiveOrder order = activeOrders[table];

        List<string> servedNames = GetEverythingOnPlate(plate, out _, out _);
        List<string> expectedNames = new List<string>(order.expectedIngredients);

        List<string> unrequestedItems = new List<string>();
        foreach (string served in servedNames)
        {
            if (expectedNames.Contains(served)) expectedNames.Remove(served);
            else unrequestedItems.Add(served);
        }
        
        int missingCount = expectedNames.Count;

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
        return missingCount == 0 && unrequestedItems.Count == 0;
    }

    public string GetActiveProfileName(TableSpot table) => activeOrders.ContainsKey(table) ? activeOrders[table].profile.profileName : "Unknown";
    public void AddMoney(int amount) { totalSavedMoney += amount; PlayerPrefs.SetInt("RestaurantTotalMoney", totalSavedMoney); PlayerPrefs.Save(); RestaurantUIManager.Instance.UpdateMoney(totalSavedMoney); }

    public bool TryServeFood(TableSpot table, PlateItem plate, out int moneyToSpawn, out string customerDialogue)
    {
        moneyToSpawn = 0; customerDialogue = "";
        if (!activeOrders.ContainsKey(table)) return false; 

        ActiveOrder order = activeOrders[table];
        List<SavedIngredient> cookableItems;
        float masterBurgerTemp;
        
        List<string> servedNames = GetEverythingOnPlate(plate, out cookableItems, out masterBurgerTemp);
        List<string> expectedNames = new List<string>(order.expectedIngredients);

        int score = order.profile.scoreSettings.baseScore;

        // DIRTY PLATE PENALTY
        bool wasDirty = false;
        if (plate.isDirty)
        {
            score += order.profile.scoreSettings.dirtyPlate;
            wasDirty = true;
        }

        float waitTime = Time.time - order.orderStartTime;
        if (waitTime > order.profile.scoreSettings.maxWaitTime) score += Mathf.RoundToInt((waitTime - order.profile.scoreSettings.maxWaitTime) * order.profile.scoreSettings.waitPenaltyPerSecond);

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
                    foreach (string req in custom.requiredIngredients) if (unrequestedItems.Contains(req)) unrequestedItems.Remove(req);
                }
            }
        }

        if (unrequestedItems.Count > 0) score += unrequestedItems.Count * order.profile.scoreSettings.unrequestedIngredient;
        if (missingCount > 0) score += missingCount * order.profile.scoreSettings.missingIngredient;

        bool isOnFire = false;
        bool unwantedRaw = false; 
        bool unwantedBurnt = false;

        foreach (var ing in cookableItems)
        {
            if (ing.isCookable)
            {
                float finalHeat = ing.heatProgressWhenAssembled; // Assume heat progress stopped/saved
                string actualState = "Raw";
                if (finalHeat >= 9999f) { actualState = "OnFire"; isOnFire = true; } // Hack for fires
                else if (finalHeat >= ing.timeToBurn) actualState = "Burnt";
                else if (finalHeat >= ing.timeToCook) actualState = "Cooked";

                CookingPreference pref = order.profile.cookingPreferences?.FirstOrDefault(p => p.ingredientName == ing.name);

                if (actualState == "OnFire") score += order.profile.scoreSettings.onFire; 
                else if (pref != null && pref.desiredState == actualState) score += pref.bonusPoints;
                else
                {
                    if (actualState == "Burnt") { score += order.profile.scoreSettings.burnt; unwantedBurnt = true; }
                    else if (actualState == "Cooked") { score += order.profile.scoreSettings.cooked; }
                    else 
                    { 
                        if (ing.category == CookableItem.FoodCategory.Meat || ing.category == CookableItem.FoodCategory.AssembledBurger)
                        {
                            score += order.profile.scoreSettings.raw; unwantedRaw = true; 
                        }
                    }
                }
            }
        }

        if (masterBurgerTemp > 0)
        {
            float tempDiff = Mathf.Abs(masterBurgerTemp - order.profile.idealTemp);
            if (tempDiff > 20f) score -= 20; 
        }

        // --- DIALOGUE ---
        if (isOnFire) customerDialogue = order.profile.reactions.foodOnFire; 
        else if (wasDirty) customerDialogue = order.profile.reactions.dirtyPlate;
        else if (!string.IsNullOrEmpty(customReactText)) customerDialogue = customReactText; 
        else if (unwantedBurnt) customerDialogue = order.profile.reactions.foodBurnt; 
        else if (unwantedRaw) customerDialogue = order.profile.reactions.foodRaw; 
        else if (missingCount > 0 || unrequestedItems.Count > 0)
        {
            string expectedStr = string.Join(", ", order.expectedIngredients);
            string servedStr = string.Join(", ", servedNames);
            customerDialogue = order.profile.reactions.wrongOrder.Replace("{ORDER}", expectedStr).Replace("{SERVED}", servedStr);
            return false; // Rejected!
        }
        else customerDialogue = order.profile.reactions.success; 

        // --- PAYMENT ---
        float scorePercent = (float)(score - order.profile.scoreSettings.baseScore) / order.profile.scoreSettings.baseScore;
        int flatMoney = order.expectedIngredients.Count * order.profile.paymentSettings.pricePerIngredient;
        int tip = 0;

        var sortedTips = order.profile.paymentSettings.tipThresholds.OrderByDescending(t => t.scorePercentage).ToList();
        foreach (var t in sortedTips) if (scorePercent >= t.scorePercentage) { tip = t.tipAmount; break; }

        moneyToSpawn = Mathf.Max(0, flatMoney + tip); 

        totalSavedScore += score;
        PlayerPrefs.SetInt("RestaurantTotalScore", totalSavedScore);
        PlayerPrefs.Save();
        RestaurantUIManager.Instance.UpdateScore(totalSavedScore);

        activeOrders.Remove(table);
        return true; 
    }

    private List<string> CreateOrderList(CustomerProfile profile)
    {
        List<string> order = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>();

        // 1. Minimums for Burger
        if (profile.ingredients != null) {
            foreach (var rule in profile.ingredients) counts[rule.name] = 0;
            foreach (var rule in profile.ingredients)
                for (int i = 0; i < rule.min; i++) { order.Add(rule.name); counts[rule.name]++; }
        }

        // 2. Determine Burger Target Size (Weighted)
        int targetTotal = order.Count; 
        if (profile.burgerSizeWeights != null && profile.burgerSizeWeights.Count > 0)
        {
            int totalWeight = profile.burgerSizeWeights.Sum(w => w.weight);
            int roll = Random.Range(0, totalWeight);
            foreach (var w in profile.burgerSizeWeights) {
                roll -= w.weight;
                if (roll < 0) { targetTotal = Mathf.Max(targetTotal, w.size); break; }
            }
        }

        // 3. Fill Burger to Target Size
        int failsafe = 0; 
        while (order.Count < targetTotal && failsafe < 100)
        {
            failsafe++;
            List<IngredientRule> validPool = new List<IngredientRule>();
            int tWeight = 0;

            if (profile.ingredients != null) {
                foreach (var rule in profile.ingredients) {
                    if (counts[rule.name] < rule.max && rule.weight > 0) { validPool.Add(rule); tWeight += rule.weight; }
                }
            }

            if (validPool.Count == 0) break; 
            int randomRoll = Random.Range(0, tWeight);
            string chosenItem = "";

            foreach (var rule in validPool) {
                randomRoll -= rule.weight;
                if (randomRoll < 0) { chosenItem = rule.name; break; }
            }

            TryAddItemWithGroup(chosenItem, profile, order, counts);
        }

        // 4. Roll for Independent Side Items (Drinks & Fries)
        if (profile.sideItems != null)
        {
            foreach (var side in profile.sideItems)
            {
                // Rolls 1-100. If the side weight is 30, it has a 30% chance to be added!
                if (Random.Range(0, 100) < side.weight)
                {
                    // Add it as many times as 'min' (usually 1)
                    int amountToAdd = Mathf.Max(1, side.min);
                    for(int i = 0; i < amountToAdd; i++) order.Add(side.name);
                }
            }
        }

        return order;
    }

    private void TryAddItemWithGroup(string itemToAdd, CustomerProfile profile, List<string> order, Dictionary<string, int> counts)
    {
        ItemGroup group = profile.groupedItems?.FirstOrDefault(g => g.items.Contains(itemToAdd));
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
            var rule = profile.ingredients?.FirstOrDefault(i => i.name == itemToAdd);
            if (rule != null && counts[itemToAdd] < rule.max) { order.Add(itemToAdd); counts[itemToAdd]++; }
        }
    }
}