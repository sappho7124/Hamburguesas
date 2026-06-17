#nullable enable
using UnityEngine;
using Yarn.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks; 

public class RestaurantYarnView : DialoguePresenterBase
{
    [Header("Yarn References")]
    public InMemoryVariableStorage? variableStorage;
    
    [HideInInspector] 
    public CustomerFaceController? currentSpeakerFace; 

    // 1. Updated Return Type to YarnTask
    public override async YarnTask OnDialogueStartedAsync()
    {
        Time.timeScale = 0f; 

        // If there is a speaker (a customer/NPC), lock the camera to them!
        if (currentSpeakerFace != null)
        {
            InteractionController ic = FindAnyObjectByType<InteractionController>();
            // Look for a child object named "Head", otherwise just look at the character
            Transform headTarget = currentSpeakerFace.transform.Find("Head");
            if (headTarget == null) headTarget = currentSpeakerFace.transform;

            if (ic != null) ic.ToggleDialogueMode(true, headTarget);
        }

        await Task.CompletedTask;
    }

    // 2. Updated Return Type to YarnTask
    public override async YarnTask OnDialogueCompleteAsync()
    {
        Time.timeScale = 1f; 
        RestaurantUIManager.Instance.HideDialoguePanel();

        // Unlock the camera and cursor
        InteractionController ic = FindAnyObjectByType<InteractionController>();
        if (ic != null) ic.ToggleDialogueMode(false);

        currentSpeakerFace = null;
        await Task.CompletedTask;
    }

    // 3. Updated Token to LineCancellationToken and Return Type to YarnTask
    public override async YarnTask RunLineAsync(LocalizedLine dialogueLine, LineCancellationToken cancellationToken)
    {
        bool hasIrritabilidad = false;
        if (variableStorage != null)
        {
            variableStorage.TryGetValue("$hasIrritabilidad", out hasIrritabilidad);
        }

        if (hasIrritabilidad && dialogueLine.Metadata != null && dialogueLine.Metadata.Contains("choice"))
        {
            return; 
        }

        CustomerFaceController.Mood mood = CustomerFaceController.Mood.Neutral;
        if (dialogueLine.Metadata != null)
        {
            if (dialogueLine.Metadata.Contains("happy")) mood = CustomerFaceController.Mood.Happy;
            else if (dialogueLine.Metadata.Contains("angry")) mood = CustomerFaceController.Mood.Angry;
            else if (dialogueLine.Metadata.Contains("sad")) mood = CustomerFaceController.Mood.Sad;
            else if (dialogueLine.Metadata.Contains("afraid")) mood = CustomerFaceController.Mood.Scared; 
            else if (dialogueLine.Metadata.Contains("dead")) mood = CustomerFaceController.Mood.Dead;
        }

        var lineTask = new TaskCompletionSource<bool>();

        RestaurantUIManager.Instance.ShowDialogue(
            dialogueLine.CharacterName, 
            dialogueLine.TextWithoutCharacterName.Text, 
            mood, 
            currentSpeakerFace, 
            () => lineTask.SetResult(true) 
        );

        await lineTask.Task;
    }

    // 4. Updated Return Type to YarnTask<DialogueOption?>
    // (We also add the [Obsolete] tag to satisfy Unity's compiler warning)
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
            (index) => optionTask.SetResult(dialogueOptions[index]) // Pass back the actual Option, not just the index
        );

        // Wait for the player to click a button
        return await optionTask.Task;
    }
}