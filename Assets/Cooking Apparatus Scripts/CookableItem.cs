using UnityEngine;
using System.Collections.Generic;

public class CookableItem : MonoBehaviour
{
    [HideInInspector] public float currentHeatMultiplier = 1f;
    public enum FoodCategory { Meat, Veggie, AssembledBurger }
    public FoodCategory category = FoodCategory.Meat;

    [Header("1. Temperature Simulation")]
    public float currentTemperature = 20f; 
    public float targetEnvironmentTemperature = 20f;
    public float ambientTemperature = 20f;
    public float tempChangeRate = 15f;[Tooltip("How much faster the item cools down when removed from a heat source.")]
    public float coolingMultiplier = 3f;[Header("2. Cooking Progress (Heat)")]
    public float cookingTempThreshold = 70f; // The temperature where it changes states
    public float timeToCook = 10f;
    public float timeToBurn = 20f;

    [Header("3. Fire State (Disaster)")]
    public float fireTempThreshold = 250f;
    public float fireBurnSpeedMultiplier = 3f; 
    public bool isOnFire = false;
    public GameObject fireParticlePrefab; 
    private GameObject activeFire;[Header("Colors")]
    public Color cookedColor = new Color(0.4f, 0.2f, 0.05f); 
    public Color burnedColor = new Color(0.1f, 0.1f, 0.1f); 

    [HideInInspector] public float currentHeatProgress = 0f;

    private struct RendererData { public Renderer renderer; public Color originalColor; }
    private List<RendererData> rendererDataList = new List<RendererData>();

    void Awake()
    {
        InitializeRenderers();
    }

    public void InitializeRenderers()
    {
        rendererDataList.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            rendererDataList.Add(new RendererData { renderer = r, originalColor = r.material.color });
        }
    }

    void Update()
    {
        // 1. Temperature moves towards the environment
        if (currentTemperature != targetEnvironmentTemperature)
        {
            float currentRate = tempChangeRate * currentHeatMultiplier;
            
            if (currentTemperature > targetEnvironmentTemperature && currentTemperature > cookingTempThreshold)
            {
                currentRate *= coolingMultiplier;
            }

            currentTemperature = Mathf.MoveTowards(currentTemperature, targetEnvironmentTemperature, currentRate * Time.deltaTime);
        }

        // 2. Heat Progress (Cooking -> Burning)
        if (currentTemperature >= cookingTempThreshold)
        {
            currentHeatProgress += Time.deltaTime;
            UpdateVisuals();
        }

        // 3. Catch Fire naturally from extreme heat
        if (currentTemperature >= fireTempThreshold && !isOnFire) CatchFire();

        // 4. If on fire, it burns MUCH faster and stays at max temperature
        if (isOnFire)
        {
            currentHeatProgress += Time.deltaTime * fireBurnSpeedMultiplier; 
            UpdateVisuals();
            if (currentTemperature < fireTempThreshold) currentTemperature = fireTempThreshold; 
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isOnFire) return; 
        CookableItem otherFood = collision.gameObject.GetComponentInParent<CookableItem>();
        if (otherFood != null && otherFood.isOnFire) CatchFire();
    }

    private void CatchFire()
    {
        isOnFire = true;
        if (fireParticlePrefab != null && activeFire == null)
        {
            activeFire = Instantiate(fireParticlePrefab, transform.position, Quaternion.identity, transform);
        }
    }

    public void Extinguish()
    {
        if (isOnFire)
        {
            isOnFire = false;
            if (activeFire != null) Destroy(activeFire);
        }
    }

    private void UpdateVisuals()
    {
        float safeProgress = Mathf.Clamp(currentHeatProgress, 0, timeToBurn);

        foreach (var data in rendererDataList)
        {
            Color targetColor = data.originalColor;

            if (category == FoodCategory.Meat || category == FoodCategory.AssembledBurger)
            {
                if (safeProgress <= timeToCook)
                    targetColor = Color.Lerp(data.originalColor, cookedColor, safeProgress / timeToCook);
                else
                    targetColor = Color.Lerp(cookedColor, burnedColor, (safeProgress - timeToCook) / (timeToBurn - timeToCook));
            }
            else // Veggies: Original -> Burned
            {
                if (safeProgress > timeToCook)
                    targetColor = Color.Lerp(data.originalColor, burnedColor, (safeProgress - timeToCook) / (timeToBurn - timeToCook));
            }

            data.renderer.material.color = targetColor;
        }
    }
}