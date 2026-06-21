using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler
{
    [Header("Hover Settings")]
    public Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1.05f);
    public float scaleSpeed = 15f;
    
    private Vector3 originalScale;
    private Vector3 targetScale;
    
    [HideInInspector] public bool isSelected = false;
    public Action<UIButtonHoverScale> OnHovered;

    void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void OnEnable()
    {
        transform.localScale = originalScale;
        targetScale = originalScale;
        isSelected = false;
    }

    void Update()
    {
        targetScale = isSelected ? hoverScale : originalScale;
        
        if (Mathf.Abs(transform.localScale.x - targetScale.x) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Tell the manager that the mouse has hovered over this specific option
        OnHovered?.Invoke(this);
    }
}