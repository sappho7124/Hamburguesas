using UnityEngine;
using TMPro;

public class FloatingWord : MonoBehaviour
{
    public float transitionSpeed = 8f;
    
    private Vector3 targetScale;
    private TextMeshProUGUI textComponent;
    private bool isDespawning = false;
    private float currentAlpha = 1f;

    public void Initialize(float scale)
    {
        targetScale = new Vector3(scale, scale, 1f);
        transform.localScale = Vector3.zero; // Start at 0 for a pop-in effect
        textComponent = GetComponent<TextMeshProUGUI>();
        
        if (textComponent != null)
        {
            currentAlpha = textComponent.color.a;
        }
    }

    void Update()
    {
        if (isDespawning)
        {
            // Smoothly shrink and fade out
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * transitionSpeed);
            
            if (textComponent != null)
            {
                currentAlpha = Mathf.Lerp(currentAlpha, 0f, Time.deltaTime * transitionSpeed);
                Color c = textComponent.color;
                c.a = currentAlpha;
                textComponent.color = c;
            }

            // Destroy once it is practically invisible
            if (transform.localScale.x < 0.05f)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            // Smoothly pop in to target scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * transitionSpeed);
        }
    }

    public void Despawn()
    {
        isDespawning = true;
    }
}