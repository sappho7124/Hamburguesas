using UnityEngine;
using System.Collections.Generic;

public class CookableItem : MonoBehaviour
{
    [HideInInspector] public float currentHeatMultiplier = 1f;
    
    // NEW: Added "Bread" to the categories
    public enum FoodCategory { Meat, Veggie, Bread, AssembledBurger }
    public FoodCategory category = FoodCategory.Meat;

    [Header("1. Temperature Simulation")]
    public float currentTemperature = 20f; 
    public float targetEnvironmentTemperature = 20f;
    public float ambientTemperature = 20f;
    public float tempChangeRate = 15f;
    public float coolingMultiplier = 3f;
    
    [Header("2. Cooking Progress (Heat)")]
    public float cookingTempThreshold = 70f; 
    public float timeToCook = 10f;
    public float timeToBurn = 20f;
    
    [Header("3. Fire State (Disaster)")]
    public float fireTempThreshold = 250f;
    public float fireBurnSpeedMultiplier = 3f; 
    public bool isOnFire = false;
    public GameObject fireParticlePrefab; 
    private GameObject activeFire;

    [Header("Colors")]
    public Color cookedColor = new Color(0.4f, 0.2f, 0.05f); 
    public Color burnedColor = new Color(0.1f, 0.1f, 0.1f); 

    [HideInInspector] public float currentHeatProgress = 0f;

    private struct RendererData { public Renderer renderer; public Color originalColor; }
    private List<RendererData> rendererDataList = new List<RendererData>();

    // We cache the property ID for performance
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        InitializeRenderers();
    }

    private static MaterialPropertyBlock propBlock;

    public void InitializeRenderers()
    {
        rendererDataList.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterial == null) continue;

            Color startColor = Color.white;

            // Check sharedMaterial so we don't accidentally create a clone on Awake!
            if (r.sharedMaterial.HasProperty(BaseColorID))
            {
                startColor = r.sharedMaterial.GetColor(BaseColorID);
            }
            else if (r.sharedMaterial.HasProperty("_Color"))
            {
                startColor = r.sharedMaterial.color;
            }

            rendererDataList.Add(new RendererData { renderer = r, originalColor = startColor });
        }
    }

    void Update()
    {
        if (currentTemperature != targetEnvironmentTemperature)
        {
            float currentRate = tempChangeRate * currentHeatMultiplier;
            if (currentTemperature > targetEnvironmentTemperature && currentTemperature > cookingTempThreshold)
            {
                currentRate *= coolingMultiplier;
            }
            currentTemperature = Mathf.MoveTowards(currentTemperature, targetEnvironmentTemperature, currentRate * Time.deltaTime);
        }

        if (currentTemperature >= cookingTempThreshold)
        {
            currentHeatProgress += Time.deltaTime;
            UpdateVisuals();
        }

        if (currentTemperature >= fireTempThreshold && !isOnFire) CatchFire();

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
        if (StoryFlowManager.Instance != null) StoryFlowManager.Instance.ReportAction("BurnSomething");
        
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

        // Initialize block once per session
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        foreach (var data in rendererDataList)
        {
            Color targetColor = data.originalColor;

            // Bread and Meat visual progression (Normal -> Golden/Cooked -> Black)
            if (category == FoodCategory.Meat || category == FoodCategory.Bread || category == FoodCategory.AssembledBurger)
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

            // Fetch current properties, inject the new color, and apply to the GPU
            data.renderer.GetPropertyBlock(propBlock);

            if (data.renderer.sharedMaterial.HasProperty(BaseColorID))
            {
                propBlock.SetColor(BaseColorID, targetColor);
            }
            else
            {
                propBlock.SetColor("_Color", targetColor);
            }

            data.renderer.SetPropertyBlock(propBlock);
        }
    }
}