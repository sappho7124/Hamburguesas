using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SavedIngredient
{
    public string name;
    public bool isCookable;
    public float heatProgressWhenAssembled;
    public float timeToCook;
    public float timeToBurn;
}

public class AssembledBurger : MonoBehaviour
{
    public float assemblyTime; // NEW: Tracks when this burger was put together
    public List<SavedIngredient> ingredients = new List<SavedIngredient>();

    public List<string> GetIngredientNames()
    {
        List<string> names = new List<string>();
        foreach (var ing in ingredients) names.Add(ing.name);
        return names;
    }
}