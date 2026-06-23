using UnityEngine;
using Yarn.Unity;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks; 

public class RestaurantYarnView : DialoguePresenterBase
{
    [Header("Yarn References")]
    public InMemoryVariableStorage? variableStorage;
    
    [HideInInspector] 
    public CustomerFaceController? currentSpeakerFace; 
    
    private string lastNodeName = "";

    private void Start()
    {
        // FIX: Safely track the current node name using the built-in UnityEvent.
        // This avoids any API version conflicts with CurrentNodeName!
        DialogueRunner runner = FindAnyObjectByType<DialogueRunner>();
        if (runner != null)
        {
            runner.onNodeStart.AddListener((nodeName) => lastNodeName = nodeName);
        }
    }

    public override async YarnTask OnDialogueStartedAsync()
    {
        await Task.CompletedTask;
    }

    public override async YarnTask OnDialogueCompleteAsync()
    {
        // Detect if the node that just finished is meant to stay on screen for gameplay
        bool isGameplayNode = lastNodeName.StartsWith("TutorialStep") || 
                              lastNodeName == "TutorialStart" || 
                              lastNodeName.StartsWith("TutorialFuckUp") || 
                              lastNodeName == "TutorialBellReminder";

        // ONLY hide the panel if it's a normal conversation
        if (!isGameplayNode)
        {
            RestaurantUIManager.Instance.HideDialoguePanel();
        }

        // ALWAYS ensure the player is unlocked when dialogue finishes
        InteractionController ic = FindAnyObjectByType<InteractionController>();
        if (ic != null) ic.ToggleDialogueMode(false);

        currentSpeakerFace = null;
        await Task.CompletedTask;
    }

    public override async YarnTask RunLineAsync(LocalizedLine dialogueLine, LineCancellationToken cancellationToken)
    {
        // Auto-dismiss Don Julio when the dark screen manipulation starts
        if (lastNodeName == "NarratorManipulation" && StoryFlowManager.Instance != null)
        {
            StoryFlowManager.Instance.DismissDonJulio();
        }

        CustomerFaceController activeFace = currentSpeakerFace;
        Transform headTarget = null;
        string cleanName = dialogueLine.CharacterName;

        if (activeFace == null && !string.IsNullOrEmpty(cleanName))
        {
            GameObject sceneNPC = GameObject.Find(cleanName) 
                ?? GameObject.Find(cleanName + "(Clone)") 
                ?? GameObject.Find(cleanName.Replace(" ", ""));

            if (sceneNPC != null)
            {
                activeFace = sceneNPC.GetComponentInChildren<CustomerFaceController>();
                
                Customer cust = sceneNPC.GetComponent<Customer>();
                if (cust != null && cust.headBone != null) headTarget = cust.headBone;
                else headTarget = sceneNPC.transform.Find("Head") ?? sceneNPC.transform;
            }
        }
        else if (activeFace != null)
        {
            Customer cust = activeFace.GetComponent<Customer>();
            if (cust != null && cust.headBone != null) headTarget = cust.headBone;
            else headTarget = activeFace.transform.Find("Head") ?? activeFace.transform;
        }

        // Detect if the node that just finished is meant to stay on screen for gameplay
        bool isGameplayNode = lastNodeName.StartsWith("TutorialStep") || 
                              lastNodeName == "TutorialStart" || 
                              lastNodeName.StartsWith("TutorialFuckUp") || 
                              lastNodeName == "TutorialBellReminder";

        InteractionController ic = FindAnyObjectByType<InteractionController>();
        if (ic != null)
        {
            if (!isGameplayNode) 
            {
                ic.ToggleDialogueMode(true, headTarget);
            }
            else
            {
                // FIX: Only unlock if it was actually locked, preventing it from snapping back to empty rot data!
                if (ic.isDialogueLocked) ic.ToggleDialogueMode(false);
            }
        }

        bool hasIrritabilidad = false;
        if (variableStorage != null)
        {
            variableStorage.TryGetValue("$hasIrritabilidad", out hasIrritabilidad);
        }

        if (hasIrritabilidad && dialogueLine.Metadata != null && Array.IndexOf(dialogueLine.Metadata, "choice") >= 0)
        {
            return; 
        }

        CustomerFaceController.Mood mood = CustomerFaceController.Mood.Neutral;
        if (dialogueLine.Metadata != null)
        {
            if (Array.IndexOf(dialogueLine.Metadata, "happy") >= 0) mood = CustomerFaceController.Mood.Happy;
            else if (Array.IndexOf(dialogueLine.Metadata, "angry") >= 0) mood = CustomerFaceController.Mood.Angry;
            else if (Array.IndexOf(dialogueLine.Metadata, "sad") >= 0) mood = CustomerFaceController.Mood.Sad;
            else if (Array.IndexOf(dialogueLine.Metadata, "afraid") >= 0) mood = CustomerFaceController.Mood.Scared; 
            else if (Array.IndexOf(dialogueLine.Metadata, "dead") >= 0) mood = CustomerFaceController.Mood.Dead;
        }

        var lineTask = new TaskCompletionSource<bool>();

        RestaurantUIManager.Instance.ShowDialogue(
            dialogueLine.CharacterName, 
            dialogueLine.TextWithoutCharacterName.Text, 
            mood, 
            activeFace, 
            isGameplayNode, // Pass the boolean we created!
            () => lineTask.SetResult(true) 
        );

        await lineTask.Task;
    }

    [Obsolete]
    public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, CancellationToken cancellationToken)
    {
        List<string> optionsTexts = new List<string>();
        foreach (var opt in dialogueOptions) 
        {
            optionsTexts.Add(opt.Line.Text.Text);
        }

        var optionTask = new TaskCompletionSource<DialogueOption?>();

        RestaurantUIManager.Instance.DisplayDialogueOptions(
            optionsTexts, 
            (index) => optionTask.SetResult(dialogueOptions[index]) 
        );

        return await optionTask.Task;
    }
}