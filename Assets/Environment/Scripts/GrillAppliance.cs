using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrillAppliance : MonoBehaviour
{[Header("Grill Settings")]
    public bool isOn = false;
    public float grillTemperature = 300f; 
    public float ambientTemperature = 20f;[Tooltip("How fast heat transfers to the food. 1 = Normal, 2 = Twice as fast, 0.5 = Half speed.")]
    public float heatTransferMultiplier = 1f;

    [Header("UI Prompts")]
    public HighlightableObject switchHighlight; 
    public string turnOnVerb = "Turn On";
    public string turnOffVerb = "Turn Off";

    [Header("Visuals")]
    public MeshRenderer indicatorMesh; 
    public Material indicatorOnMaterial; 
    public Material indicatorOffMaterial; 
    public Transform knob;
    public float knobTurnSpeed = 10f;

    private List<CookableItem> itemsOnGrill = new List<CookableItem>();
    private Quaternion knobOffRot;
    private Quaternion knobOnRot;

    void Awake()
    {
        if (knob)
        {
            knobOffRot = knob.localRotation;
            knobOnRot = knobOffRot * Quaternion.Euler(0, 0, 90f); 
        }
        UpdateStateVisuals();
    }

    public void ToggleGrill()
    {
        isOn = !isOn;
        UpdateStateVisuals();
        
        StopAllCoroutines();
        StartCoroutine(AnimateKnob(isOn ? knobOnRot : knobOffRot));

        // Update temperature and multiplier for all food currently sitting on the grill
        float newTemp = isOn ? grillTemperature : ambientTemperature;
        float newMultiplier = isOn ? heatTransferMultiplier : 1f; // Back to normal speed if turned off
        
        foreach (var food in itemsOnGrill)
        {
            if (food != null) 
            {
                food.targetEnvironmentTemperature = newTemp;
                food.currentHeatMultiplier = newMultiplier;
            }
        }
    }

    private void UpdateStateVisuals()
    {
        if (indicatorMesh != null) indicatorMesh.material = isOn ? indicatorOnMaterial : indicatorOffMaterial;
        if (switchHighlight != null) switchHighlight.interactionVerb = isOn ? turnOffVerb : turnOnVerb;
    }

    private IEnumerator AnimateKnob(Quaternion targetRot)
    {
        if (knob == null) yield break;
        while (Quaternion.Angle(knob.localRotation, targetRot) > 0.1f)
        {
            knob.localRotation = Quaternion.Lerp(knob.localRotation, targetRot, Time.deltaTime * knobTurnSpeed);
            yield return null;
        }
        knob.localRotation = targetRot;
    }

    void OnTriggerEnter(Collider other)
    {
        CookableItem food = other.GetComponentInParent<CookableItem>();
        if (food != null && !itemsOnGrill.Contains(food))
        {
            itemsOnGrill.Add(food);
            
            food.targetEnvironmentTemperature = isOn ? grillTemperature : ambientTemperature;
            food.currentHeatMultiplier = isOn ? heatTransferMultiplier : 1f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        CookableItem food = other.GetComponentInParent<CookableItem>();
        if (food != null && itemsOnGrill.Contains(food))
        {
            itemsOnGrill.Remove(food);
            
            // Reset back to room temperature and normal heating/cooling speed
            food.targetEnvironmentTemperature = food.ambientTemperature;
            food.currentHeatMultiplier = 1f;
        }
    }
}