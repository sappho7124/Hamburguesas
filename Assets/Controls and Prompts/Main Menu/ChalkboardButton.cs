using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using UnityEngine.Events;

public class ChalkboardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    [Header("References")]
    public TextMeshProUGUI buttonText;
    
    [Header("Settings")]
    public float popScaleMultiplier = 1.15f;
    public float popDuration = 0.1f;
    
    [Header("Events")]
    public UnityEvent onClick;

    private Vector3 originalScale;
    private Coroutine popCoroutine;

    void Start()
    {
        originalScale = transform.localScale;
        if (buttonText == null) buttonText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Add underline
        if (buttonText != null) 
            buttonText.fontStyle |= FontStyles.Underline;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Remove underline
        if (buttonText != null) 
            buttonText.fontStyle &= ~FontStyles.Underline;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Do the pop effect
        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(PopAnimation());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Fire the event (like a normal button)
        onClick?.Invoke();
    }

    private IEnumerator PopAnimation()
    {
        Vector3 targetScale = originalScale * popScaleMultiplier;
        
        // Scale Up
        for (float t = 0; t < popDuration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t / popDuration);
            yield return null;
        }

        // Scale Down
        for (float t = 0; t < popDuration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t / popDuration);
            yield return null;
        }

        transform.localScale = originalScale;
    }
}