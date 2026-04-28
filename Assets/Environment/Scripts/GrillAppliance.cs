// Location: C:\Games\Unity\Hamburguesas\Assets\Environment\Scripts\GrillAppliance.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrillAppliance : MonoBehaviour
{
    [Header("Grill Settings")]
    public bool isOn = false;
    public float grillTemperature = 300f; 
    public float ambientTemperature = 20f;
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
    private List<VesselBase> vesselsOnGrill = new List<VesselBase>(); // NEW: Tracks Baskets!
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

        float newTemp = isOn ? grillTemperature : ambientTemperature;
        float newMultiplier = isOn ? heatTransferMultiplier : 1f; 
        
        foreach (var food in itemsOnGrill)
        {
            if (food != null) 
            {
                food.targetEnvironmentTemperature = newTemp;
                food.currentHeatMultiplier = newMultiplier;
            }
        }

        // Pass new temperature to any Baskets in the oil
        foreach (var vessel in vesselsOnGrill)
        {
            if (vessel != null) vessel.SetEnvironmentState(newTemp, newMultiplier);
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
        // 1. Direct Food Check
        CookableItem food = other.GetComponentInParent<CookableItem>();
        if (food != null && !itemsOnGrill.Contains(food))
        {
            itemsOnGrill.Add(food);
            food.targetEnvironmentTemperature = isOn ? grillTemperature : ambientTemperature;
            food.currentHeatMultiplier = isOn ? heatTransferMultiplier : 1f;
        }

        // 2. Vessel/Basket Check
        VesselBase vessel = other.GetComponentInParent<VesselBase>();
        if (vessel != null && !vesselsOnGrill.Contains(vessel))
        {
            vesselsOnGrill.Add(vessel);
            vessel.SetEnvironmentState(isOn ? grillTemperature : ambientTemperature, isOn ? heatTransferMultiplier : 1f);
        }
    }

    void OnTriggerExit(Collider other)
    {
        // 1. Direct Food Check
        CookableItem food = other.GetComponentInParent<CookableItem>();
        if (food != null && itemsOnGrill.Contains(food))
        {
            itemsOnGrill.Remove(food);
            food.targetEnvironmentTemperature = food.ambientTemperature;
            food.currentHeatMultiplier = 1f;
        }

        // 2. Vessel/Basket Check
        VesselBase vessel = other.GetComponentInParent<VesselBase>();
        if (vessel != null && vesselsOnGrill.Contains(vessel))
        {
            vesselsOnGrill.Remove(vessel);
            // Reset Basket back to room temperature
            vessel.SetEnvironmentState(ambientTemperature, 1f);
        }
    }
}