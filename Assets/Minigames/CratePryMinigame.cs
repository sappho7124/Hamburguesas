using UnityEngine;

public class CratePryMinigame : MinigameBase
{
    [Header("Crate Parts")]
    public GameObject lidObject;
    public Transform pryPoint; 

    [Header("Parameters")]
    public float forceRequired = 100f;
    public float mashPower = 12f;
    public float friction = 8f; 

    [Header("Visual Crowbar Animation")]
    public Transform crowbarVisual; 
    public Vector3 rotationAxis = Vector3.right;
    public float maxAngle = 35f;

    private float currentForce = 0f;
    private GameObject realCrowbar; // The one in the player's hand

    protected override void OnMinigameStarted()
    {
        currentForce = 0f;

        // 1. Hide real crowbar
        EquipmentController ec = FindAnyObjectByType<EquipmentController>();
        if (ec && ec.handMount.childCount > 0)
        {
            realCrowbar = ec.handMount.GetChild(0).gameObject;
            realCrowbar.SetActive(false);
        }

        // 2. Setup Visual Crowbar
        if (crowbarVisual)
        {
            crowbarVisual.gameObject.SetActive(true);
            crowbarVisual.position = pryPoint.position;
            crowbarVisual.rotation = pryPoint.rotation;
        }

        // 3. UI
        ActionPromptManager.Instance.ClearAll(true);
        ActionPromptManager.Instance.ShowPrompt("MashPry", "Minigames", "Action", "Pry Lid");
        ActionPromptManager.Instance.ShowPrompt("CancelPry", "Minigames", "Cancel", "Release");
    }

    protected override void Update()
    {
        base.Update();
        if (!isActive) return;

        // Mashing
        if (controls.Minigames.Action.triggered)
        {
            currentForce += mashPower;
        }

        // Decay
        currentForce = Mathf.Max(0, currentForce - (friction * Time.deltaTime));

        // Animate Visuals
        if (crowbarVisual)
        {
            float progress = currentForce / forceRequired;
            crowbarVisual.localRotation = pryPoint.localRotation * Quaternion.Euler(rotationAxis * (progress * maxAngle));
        }

        if (currentForce >= forceRequired)
        {
            EndMinigame(true);
        }
    }

    protected override void OnMinigameEnded(bool success)
    {
        // Restore real crowbar
        if (realCrowbar) realCrowbar.SetActive(true);
        
        // Hide animation crowbar
        if (crowbarVisual) crowbarVisual.gameObject.SetActive(false);

        ActionPromptManager.Instance.ClearAll(true);
        
        // If we have a tool, put the tool prompts back
        EquipmentController ec = FindAnyObjectByType<EquipmentController>();
        if (ec) ec.RefreshPrompts();

        if (success) OpenCrate();
    }

    void OpenCrate()
    {
        if (lidObject)
        {
            Rigidbody rb = lidObject.GetComponent<Rigidbody>();
            if (rb == null) rb = lidObject.AddComponent<Rigidbody>();
            
            rb.isKinematic = false;
            // Pop the lid up and away
            rb.AddForce(transform.up * 6f + transform.forward * 2f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
            
            // Remove interaction so we don't pry an open box
            InteractableObject io = GetComponent<InteractableObject>();
            if (io) io.enabled = false;
        }
    }
}