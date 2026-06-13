using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]
    public Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1.05f); // 5% bigger
    public float scaleSpeed = 15f;
    
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void OnEnable()
    {
        // Reset scale instantly if the button is turned off and back on
        transform.localScale = originalScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smoothly scale towards the target
        if (Mathf.Abs(transform.localScale.x - targetScale.x) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }
}