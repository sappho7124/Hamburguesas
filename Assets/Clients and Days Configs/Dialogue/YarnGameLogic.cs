using UnityEngine;
using Yarn.Unity;
using System.Collections;

public class YarnGameLogic : MonoBehaviour
{
    public static YarnGameLogic Instance;
    public DialogueRunner dialogueRunner;

    [HideInInspector] 
    public Customer currentInteractingCustomer;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    // YARN COMMAND: <<set_order "Pan, Carne, Queso, Tomate">>
    [YarnCommand("set_order")]
    public void SetOrder(string ingredientsList)
    {
        if (currentInteractingCustomer == null)
        {
            Debug.LogError("[YarnGameLogic] Tried to set order, but no customer is currently interacting!");
            return;
        }

        TableSpot table = currentInteractingCustomer.GetComponentInParent<SittingSpot>()?.linkedTableSpot;
        if (table != null)
        {
            OrderManager.Instance.SetManualOrder(table, currentInteractingCustomer.profile, ingredientsList);
        }
    }

    // YARN COMMAND: <<set_status_effect "Irritabilidad" true>>
    [YarnCommand("set_status_effect")]
    public void SetStatusEffect(string effectName, bool value)
    {
        dialogueRunner.VariableStorage.SetValue("$hasIrritabilidad", value);
        Debug.Log($"[Yarn] Status Effect {effectName} set to {value}");
    }

    [YarnCommand("lucas_leave")]
    public void LucasLeave()
    {
        StoryFlowManager.Instance.DismissLucasAndDropVegetables();
    }

    [YarnCommand("start_shift")]
    public void StartShift()
    {
        int day = StoryFlowManager.Instance.overrideSaveDay ? StoryFlowManager.Instance.debugForceDay : SaveManager.Instance.CurrentSave.currentDay;
        CustomerSpawner.Instance.StartShift(day);
    }

    [YarnCommand("customer_leave")]
    public void CustomerLeave()
    {
        if (currentInteractingCustomer != null)
        {
            currentInteractingCustomer.Leave();
            currentInteractingCustomer = null;
        }
    }

    [YarnCommand("julio_leave")]
    public void JulioLeave()
    {
        StoryFlowManager.Instance.DismissDonJulio();
    }
}