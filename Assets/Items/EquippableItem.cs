using UnityEngine;
using System;
using System.Collections;

public class EquippableItem : MonoBehaviour 
{
    [Header("Equipment Data")]
    public ItemDefinition itemDef;
    
    [Header("Hand Positioning")]
    public Vector3 handPositionOffset = new Vector3(0.5f, -0.4f, 0.8f);
    public Vector3 handRotationOffset = new Vector3(0, 90, 0);

    [Header("Placement Settings")]
    public float placementOffset = 0f;


    [Header("Transition Settings")]
    public float transitionDuration = 0.25f; // How long it takes to equip/place

    private Rigidbody rb;
    private Collider col;
    private Coroutine transitionCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void Start()
    {
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null && string.IsNullOrEmpty(highlight.interactionVerb))
        {
            highlight.interactionVerb = "Equip";
        }
    }

    // --- NEW: Smooth Transition Logic ---
    public void StartTransition(Transform newParent, Vector3 targetPos, Quaternion targetRot, bool isLocal, Action onComplete)
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        
        transform.SetParent(newParent);
        transitionCoroutine = StartCoroutine(TransitionRoutine(targetPos, targetRot, isLocal, onComplete));
    }

    public void StopTransition()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }
    }

    private IEnumerator TransitionRoutine(Vector3 targetPos, Quaternion targetRot, bool isLocal, Action onComplete)
    {
        Vector3 startPos = isLocal ? transform.localPosition : transform.position;
        Quaternion startRot = isLocal ? transform.localRotation : transform.rotation;
        
        float time = 0;
        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / transitionDuration);
            t = Mathf.SmoothStep(0, 1, t); // Adds a nice ease-in, ease-out

            if (isLocal)
            {
                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            else
            {
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            yield return null;
        }

        // Snap to exact destination to prevent floating point inaccuracies
        if (isLocal)
        {
            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
        }

        transitionCoroutine = null;
        onComplete?.Invoke(); // Trigger the callback (which restores physics)
    }

    public void SetPhysics(bool enabled)
    {
        // If physics are abruptly turned back on (e.g. by Throwing), stop any ongoing movement
        if (enabled) StopTransition(); 

        if (rb) rb.isKinematic = !enabled;
        if (col) col.enabled = enabled;
        
        HighlightableObject highlight = GetComponent<HighlightableObject>();
        if (highlight != null)
        {
            if (!enabled)
            {
                highlight.ToggleHighlight(false);
                Outline o = highlight.OutlineComponent;
                if (o != null)
                {
                    o.OutlineWidth = 0f;
                    o.OutlineColor = Color.clear;
                }
                highlight.enabled = false; 
            }
            else
            {
                highlight.enabled = true;
            }
        }
    }

    public Rigidbody GetRigidbody() => rb;
}