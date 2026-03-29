using UnityEngine;

public class CookableItem : MonoBehaviour
{
    public enum FoodCategory { Meat, Veggie }
    public FoodCategory category = FoodCategory.Meat;[Header("Cooking Times")]
    public float timeToCook = 10f;
    public float timeToBurn = 20f;
    
    [Header("Colors (Meat Only)")]
    public Color rawColor = new Color(1f, 0.6f, 0.6f); // Pink
    public Color cookedColor = new Color(0.4f, 0.2f, 0.05f); // Brown
    
    [Header("Colors (Universal)")]
    public Color burnedColor = new Color(0.1f, 0.1f, 0.1f); // Black

    public float currentHeatProgress = 0f;
    private Renderer[] renderers;
    private Color originalColor;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0) 
        {
            originalColor = renderers[0].material.color;
        }
    }

    public void ApplyHeat(float amount)
    {
        currentHeatProgress += amount;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        Color targetColor = originalColor;

        if (category == FoodCategory.Meat)
        {
            if (currentHeatProgress <= timeToCook)
                targetColor = Color.Lerp(rawColor, cookedColor, currentHeatProgress / timeToCook);
            else
                targetColor = Color.Lerp(cookedColor, burnedColor, (currentHeatProgress - timeToCook) / (timeToBurn - timeToCook));
        }
        else // Veggies: Original -> Burned
        {
            if (currentHeatProgress > timeToCook)
                targetColor = Color.Lerp(originalColor, burnedColor, (currentHeatProgress - timeToCook) / (timeToBurn - timeToCook));
        }

        // Using .material guarantees the color updates regardless of your URP/Built-In shader setup
        foreach (var rend in renderers)
        {
            rend.material.color = targetColor;
        }
    }
}