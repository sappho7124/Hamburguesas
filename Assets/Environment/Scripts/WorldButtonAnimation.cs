using UnityEngine;
using System.Collections;

public class WorldButtonAnimation : MonoBehaviour
{
    [Header("Button Settings")]
    [SerializeField] private Transform buttonObject;

    [SerializeField] private float pressDistance = 0.05f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.12f;

    private Vector3 originalPosition;
    private Coroutine currentAnimation;

    private void Awake()
    {
        if (buttonObject == null)
            buttonObject = transform;

        originalPosition = buttonObject.localPosition;
    }

    /// <summary>
    /// Call this from your interaction script.
    /// </summary>
    public void Press()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(PressAnimation());
    }

    private IEnumerator PressAnimation()
    {
        Vector3 startPos = buttonObject.localPosition;
        Vector3 pressedPos = originalPosition + Vector3.down * pressDistance;

        // Press down
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pressDuration;

            float eased = EaseOutCubic(t);
            buttonObject.localPosition = Vector3.Lerp(startPos, pressedPos, eased);

            yield return null;
        }

        buttonObject.localPosition = pressedPos;

        // Release
        t = 0f;
        startPos = buttonObject.localPosition;

        while (t < 1f)
        {
            t += Time.deltaTime / releaseDuration;

            float eased = EaseOutBack(t);
            buttonObject.localPosition = Vector3.Lerp(startPos, originalPosition, eased);

            yield return null;
        }

        buttonObject.localPosition = originalPosition;
        currentAnimation = null;
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}