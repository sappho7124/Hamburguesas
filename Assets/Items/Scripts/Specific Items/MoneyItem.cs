using UnityEngine;
using System.Collections;

public class MoneyPickup : MonoBehaviour
{
    [Header("Settings")][Tooltip("How much money this pickup is worth. Can be set manually if placed in the scene.")]
    public int moneyValue = 15;[Tooltip("How long it takes to fly into the camera. (Guarantees it finishes)")]
    public float flyDuration = 0.5f; 
    public float offset = -0.5f; // How far below the camera it flies before disappearing
    
    private bool isCollected = false;

    void Start()
    {
        // If placed manually in the scene, ensure the UI is updated immediately
        UpdateHighlightText();
    }

    // Called by TableSpot when spawned dynamically by a paying customer
    public void Initialize(int value)
    {
        moneyValue = value;
        UpdateHighlightText();
    }

    private void UpdateHighlightText()
    {
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null)
        {
            highlight.objectName = ""; // Leave blank so only the verb shows
            highlight.interactionVerb = $"Agarrar ${moneyValue}";
        }
    }

    // Link this function to your InteractableObject UnityEvent!
    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        // Disable highlights and colliders instantly
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null) Destroy(highlight);

        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;

        StartCoroutine(FlyToPlayerRoutine());
    }

    private IEnumerator FlyToPlayerRoutine()
    {
        Transform targetCamera = Camera.main.transform;
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        
        float elapsed = 0f;

        // Guaranteed to finish after 'flyDuration' seconds
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            
            // Smooth easing for a polished feel
            t = t * t * (3f - 2f * t);

            // Calculate target relative to the camera
            Vector3 targetPos = targetCamera.position + (targetCamera.forward * 0.5f) + (targetCamera.up * offset);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            
            yield return null;
        }

        // Failsafe: Ensure it reaches scale 0
        transform.localScale = Vector3.zero;

        // Add to Order Manager and destroy
        if (OrderManager.Instance != null)
        {
            OrderManager.Instance.AddMoney(moneyValue);
        }
        else
        {
            Debug.LogWarning("[MoneyPickup] OrderManager instance not found! Money couldn't be added.");
        }
        
        Destroy(gameObject);
    }
}