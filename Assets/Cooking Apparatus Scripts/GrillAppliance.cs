using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrillAppliance : MonoBehaviour
{
    [Header("Grill Settings")]
    public bool isOn = false;
    public float heatPerSecond = 1f;

    [Header("UI Prompts")]
    public HighlightableObject switchHighlight; // Drag the HighlightableObject of your button/switch here
    public string turnOnVerb = "Turn On";
    public string turnOffVerb = "Turn Off";

    [Header("Visuals (Indicator Mesh)")]
    public MeshRenderer indicatorMesh; 
    public Material indicatorOnMaterial; 
    public Material indicatorOffMaterial; 
    
    [Header("Visuals (Knob)")]
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

    // Link this to the OnInteract UnityEvent of your Grill Button/Switch
    public void ToggleGrill()
    {
        isOn = !isOn;
        UpdateStateVisuals();
        
        StopAllCoroutines();
        StartCoroutine(AnimateKnob(isOn ? knobOnRot : knobOffRot));
    }

    private void UpdateStateVisuals()
    {
        // Swap Light Material
        if (indicatorMesh != null)
        {
            indicatorMesh.material = isOn ? indicatorOnMaterial : indicatorOffMaterial;
        }

        // Swap UI Prompt Verb
        if (switchHighlight != null)
        {
            switchHighlight.interactionVerb = isOn ? turnOffVerb : turnOnVerb;
        }
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

    // Unity physics trigger detection
    void OnTriggerEnter(Collider other)
    {
        CookableItem food = other.GetComponentInParent<CookableItem>();
        if (food != null && !itemsOnGrill.Contains(food)) itemsOnGrill.Add(food);
    }

    void OnTriggerExit(Collider other)
    {
        CookableItem food = other.GetComponentInParent<CookableItem>();
        if (food != null && itemsOnGrill.Contains(food)) itemsOnGrill.Remove(food);
    }

    void Update()
    {
        if (!isOn) return;
        
        for (int i = itemsOnGrill.Count - 1; i >= 0; i--)
        {
            if (itemsOnGrill[i] != null) itemsOnGrill[i].ApplyHeat(heatPerSecond * Time.deltaTime);
            else itemsOnGrill.RemoveAt(i); 
        }
    }
}