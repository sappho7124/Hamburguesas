using UnityEngine;
using Yarn.Unity;

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

        if (dialogueRunner != null)
        {
            dialogueRunner.AddCommandHandler<string>("set_order", SetOrder);
            dialogueRunner.AddCommandHandler<string, bool>("set_status_effect", SetStatusEffect);
            dialogueRunner.AddCommandHandler("lucas_leave", LucasLeave);
            dialogueRunner.AddCommandHandler("start_shift", StartShift);
            dialogueRunner.AddCommandHandler("customer_leave", CustomerLeave);
            dialogueRunner.AddCommandHandler("julio_leave", JulioLeave);
            dialogueRunner.AddCommandHandler("spawn_julio", SpawnJulio);
            dialogueRunner.AddCommandHandler<float>("skippable_wait", SkippableWait);

            dialogueRunner.AddCommandHandler("spawn_lucas", SpawnLucas);
            
            // NEW: Floating words and Audio Commands
            dialogueRunner.AddCommandHandler<string>("start_floating_words", StartFloatingWords);
            dialogueRunner.AddCommandHandler("stop_floating_words", StopFloatingWords);
            dialogueRunner.AddCommandHandler<string, float>("play_sound", PlaySound);
            dialogueRunner.AddCommandHandler<string>("stop_sound", StopSound);
            
            // NEW: Darkness control
            dialogueRunner.AddCommandHandler<float>("set_darkness", SetDarkness);
        }
    }

    public void SetDarkness(float alpha)
    {
        if (RestaurantUIManager.Instance != null)
        {
            RestaurantUIManager.Instance.SetDarkness(alpha);
        }
    }

    public void SetOrder(string ingredientsList)
    {
        if (currentInteractingCustomer == null) return;

        // FIX: Fetch the target seat directly instead of relying on the hierarchy
        SittingSpot spot = currentInteractingCustomer.TargetSeat;
        TableSpot table = spot != null ? spot.linkedTableSpot : null;
        
        if (table != null)
        {
            OrderManager.Instance.SetManualOrder(table, currentInteractingCustomer.profile, ingredientsList);
            Debug.Log($"[YarnGameLogic] SUCCESS: Added Yarn order for {currentInteractingCustomer.profile.profileName} at table '{table.name}'");
        }
        else
        {
            Debug.LogError("[YarnGameLogic] FAILED to set order: Customer is seated, but linked TableSpot is missing!");
        }
    }

    public void SetStatusEffect(string effectName, bool value)
    {
        dialogueRunner.VariableStorage.SetValue("$has" + effectName, value);
        
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SetDecision("Status_" + effectName, value.ToString());
        }

        if (RestaurantUIManager.Instance != null)
        {
            RestaurantUIManager.Instance.ToggleStatusEffectUI(effectName, value);
        }
    }

    public void LucasLeave()
    {
        StoryFlowManager.Instance.DismissLucasAndDropVegetables();
        
        KitchenBell bell = FindAnyObjectByType<KitchenBell>();
        if (bell != null) bell.ShowExclamationMark();
    }

    public void StartShift()
    {
        int day = StoryFlowManager.Instance.overrideSaveDay ? StoryFlowManager.Instance.debugForceDay : SaveManager.Instance.CurrentSave.currentDay;
        CustomerSpawner.Instance.StartShift(day);
    }

    public void CustomerLeave()
    {
        if (currentInteractingCustomer != null)
        {
            currentInteractingCustomer.Leave();
            currentInteractingCustomer = null;
        }
    }

    public void JulioLeave()
    {
        StoryFlowManager.Instance.DismissDonJulio();
    }

    public void StartFloatingWords(string words)
    {
        if (FloatingTextManager.Instance != null) FloatingTextManager.Instance.StartWords(words);
    }

    public void StopFloatingWords()
    {
        if (FloatingTextManager.Instance != null) FloatingTextManager.Instance.StopWords();
    }

    public void PlaySound(string soundName, float intensity)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(soundName, intensity);
    }

    public void StopSound(string soundName)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopSound(soundName);
    }

    public void SpawnJulio()
    {
        if (StoryFlowManager.Instance != null)
            StoryFlowManager.Instance.SpawnDonJulio();
    }

    public Coroutine SkippableWait(float duration)
    {
        return StartCoroutine(SkippableWaitRoutine(duration));
    }

    private System.Collections.IEnumerator SkippableWaitRoutine(float duration)
    {
        float timer = 0f;
        var controls = new Player_Controls();
        controls.Enable();

        while (timer < duration)
        {
            // Speeds up the wait time 5x if holding the fast forward button
            if (controls.UI.FastForward.IsPressed()) timer += Time.deltaTime * 5f;
            else timer += Time.deltaTime;
            
            yield return null;
        }
        controls.Disable();
    }

    public void SpawnLucas()
    {
        if (StoryFlowManager.Instance != null)
        {
            if (StoryFlowManager.Instance.lucasPrefab != null && StoryFlowManager.Instance.lucasSpawnPoint != null)
            {
                StoryFlowManager.Instance.activeLucas = Instantiate(StoryFlowManager.Instance.lucasPrefab, StoryFlowManager.Instance.lucasSpawnPoint.position, StoryFlowManager.Instance.lucasSpawnPoint.rotation);
                StoryFlowManager.Instance.activeLucas.name = "Lucas"; 
                            
                Customer lucasCustomer = StoryFlowManager.Instance.activeLucas.GetComponent<Customer>();
                if (lucasCustomer != null)
                {
                    // Pass empty string "" to avoid the weird madness word cloud
                    lucasCustomer.TriggerSpecialWalkAndWait(StoryFlowManager.Instance.lucasWaitPoint, "", "LucasBringsVegetables", "Hablar");
                }
            }
        }
    }
}