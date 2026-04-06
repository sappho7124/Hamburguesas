using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class InteractionPromptUI : MonoBehaviour
{
    [Header("Crucial References")]
    public Camera uiCamera; 
    public RectTransform panelRoot; 
    public RectTransform panelRect; 
    public RectTransform maskRect; 
    
    [Header("Components")]
    public Image backgroundImage; 
    public UIBorderRenderer panelOutlineRenderer; 
    public Image iconImage; 
    public TextMeshProUGUI promptText; 
    public Image leftMaskImage; 

    [Header("Icons")]
    public Sprite blockedIcon; 

    [Header("Materials")]
    public Material baseTextMaterial;
    public Material baseIconMaterial;
    public Material baseBackgroundMaterial; 
    public Material baseOutlineMaterial; 

    [Header("Mask Settings")]
    public float maskWidth = 1000f;
    public float maskHeightMultiplier = 1.2f;

    [Header("Scale Settings")]
    public float baseScale = 0.01f; 
    public bool fixedScreenSize = true;

    [Header("Visual Options")]
    public bool disableTransparencyFade = false;
    
    private CanvasGroup canvasGroup;
    private float animationProgress = -1f; 
    private float targetProgress = -1f;
    private float currentFadeSpeed = 10f; 
    
    private InteractionController controller;
    private HighlightableObject currentTarget;
    private bool isCurrentStateBlocked = false;
    
    private Material instancedTextMat;
    private Material instancedIconMat;
    private Material instancedBgMat;
    private Material instancedOutlineMat;
    private Material instancedMaskMat;

    void Awake()
    {
        if (panelRect == null && backgroundImage != null) panelRect = backgroundImage.rectTransform;
        if (panelRect != null)
        {
            canvasGroup = panelRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panelRect.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }
        if (uiCamera == null) uiCamera = Camera.main;

        // Ensure Pivot is correct for the new sliding logic
        if (panelRect) panelRect.pivot = new Vector2(0f, 0.5f);

        if (baseTextMaterial) { instancedTextMat = new Material(baseTextMaterial); promptText.fontMaterial = instancedTextMat; }
        if (baseIconMaterial) { instancedIconMat = new Material(baseIconMaterial); iconImage.material = instancedIconMat; }
        if (baseBackgroundMaterial) { instancedBgMat = new Material(baseBackgroundMaterial); backgroundImage.material = instancedBgMat; }
        if (baseOutlineMaterial) { instancedOutlineMat = new Material(baseOutlineMaterial); panelOutlineRenderer.material = instancedOutlineMat; }
        if (baseBackgroundMaterial && leftMaskImage) { instancedMaskMat = new Material(baseBackgroundMaterial); leftMaskImage.material = instancedMaskMat; }
    }

    void Start()
    {
        controller = FindAnyObjectByType<InteractionController>();
        if (controller != null) controller.OnInteractableHover += UpdatePrompt;
    }

    void OnDestroy()
    {
        if (controller != null) controller.OnInteractableHover -= UpdatePrompt;
    }

    void Update()
    {
        if (currentTarget == null) targetProgress = -1f;

        if (Mathf.Abs(animationProgress - targetProgress) > 0.001f)
            animationProgress = Mathf.Lerp(animationProgress, targetProgress, Time.deltaTime * currentFadeSpeed);

        if (canvasGroup != null)
            canvasGroup.alpha = (disableTransparencyFade) ? ((animationProgress > -0.99f) ? 1f : 0f) : ((animationProgress >= 0) ? animationProgress : 1f);

        if (currentTarget != null && animationProgress > -0.99f)
        {
            TrackObject();
            SyncOutlineVisuals(); 
        }
    }

// Inside InteractionPromptUI.cs

// Helper to find the visual right edge of the object based on camera perspective
float CalculateDynamicEdgeOffset(HighlightableObject target, Vector3 camRight)
{
    // 1. Get all renderers (in case it's a complex object with children)
    // Ignore particle renderers so they don't stretch the UI distance
    Renderer[] renderers = target.GetComponentsInChildren<Renderer>()
        .Where(r => !(r is ParticleSystemRenderer))
        .ToArray();

    if (renderers.Length == 0) return 0f;

    float maxDot = 0f;
    Vector3 anchorPos = target.GetUIPosition();

    foreach (Renderer rend in renderers)
    {
        // We use the object's World-Axis-Aligned bounding box corners
        Bounds b = rend.bounds;
        Vector3[] corners = new Vector3[8];
        Vector3 center = b.center;
        Vector3 ext = b.extents;

        corners[0] = center + new Vector3(ext.x, ext.y, ext.z);
        corners[1] = center + new Vector3(ext.x, ext.y, -ext.z);
        corners[2] = center + new Vector3(ext.x, -ext.y, ext.z);
        corners[3] = center + new Vector3(ext.x, -ext.y, -ext.z);
        corners[4] = center + new Vector3(-ext.x, ext.y, ext.z);
        corners[5] = center + new Vector3(-ext.x, ext.y, -ext.z);
        corners[6] = center + new Vector3(-ext.x, -ext.y, ext.z);
        corners[7] = center + new Vector3(-ext.x, -ext.y, -ext.z);

        foreach (Vector3 corner in corners)
        {
            // Calculate vector from anchor to corner
            Vector3 rel = corner - anchorPos;
            // Project onto camera right
            float dot = Vector3.Dot(rel, camRight);
            if (dot > maxDot) maxDot = dot;
        }
    }

    return maxDot;
}

void TrackObject()
{
    if (uiCamera == null || panelRect == null || currentTarget == null) return;

    Vector3 anchorPos = currentTarget.GetUIPosition();
    float dist = Vector3.Distance(uiCamera.transform.position, anchorPos);
    Vector3 targetScale = Vector3.one * baseScale;
    if (fixedScreenSize) targetScale *= dist;

    LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    float worldWidth = panelRect.rect.width * targetScale.x;

    if (maskRect != null)
    {
        maskRect.position = anchorPos;
        maskRect.rotation = uiCamera.transform.rotation;
        maskRect.localScale = targetScale;
        maskRect.sizeDelta = new Vector2(maskWidth, panelRect.sizeDelta.y * maskHeightMultiplier);
    }

    // --- NEW DYNAMIC EDGE LOGIC ---
    Vector3 camRight = uiCamera.transform.right;
    float edgeOffset = CalculateDynamicEdgeOffset(currentTarget, camRight);

    float progress = Mathf.Clamp01(animationProgress);
    float easedProgress = Mathf.SmoothStep(0, 1, progress);
    
    // Hidden: Tucked behind the object center
    float hiddenX = -worldWidth;
    
    // Visible: Visual Edge + Buffer (slideOutDistance)
    float visibleX = edgeOffset + currentTarget.slideOutDistance;
    
    float slideX = Mathf.Lerp(hiddenX, visibleX, easedProgress);
    
    Vector3 slideOffset = camRight * slideX;

    panelRect.position = anchorPos + slideOffset;
    panelRect.rotation = uiCamera.transform.rotation;
    panelRect.localScale = targetScale;
}

    void SyncOutlineVisuals()
    {
        if (panelOutlineRenderer == null || currentTarget == null) return;

        Outline objOutline = currentTarget.OutlineComponent;
        if (objOutline != null)
        {
            // Thickness sync
            panelOutlineRenderer.Thickness = -(objOutline.OutlineWidth * currentTarget.thicknessMultiplier);
            
            // ALPHA FIX: 
            // Use the actual current width of the 3D outline to drive UI transparency.
            // This ensures they fade out together.
            float alpha = Mathf.Clamp01(objOutline.OutlineWidth / (currentTarget.maxOutlineWidth * 0.5f));
            
            Color c = objOutline.OutlineColor;
            c.a = alpha;
            panelOutlineRenderer.color = c;

            Color contentColor = isCurrentStateBlocked ? c : Color.white;
            contentColor.a = alpha; // Apply smooth fade to text/icon too

            if (iconImage) iconImage.color = (iconImage.sprite != null) ? contentColor : Color.clear;
            if (promptText) promptText.color = contentColor;
        }
    }

    private void UpdatePrompt(bool isVisible, HighlightableObject target, string map, string action, bool isBlocked)
    {
        if (isVisible && target != null)
        {
            if (animationProgress < 0 || currentTarget != target) animationProgress = 0f;
            currentTarget = target;
            currentFadeSpeed = target.animSpeed;
            isCurrentStateBlocked = isBlocked;

            string finalVerb = target.interactionVerb;
            if (isBlocked)
            {
                InteractableObject io = target.GetComponent<InteractableObject>();
                if (io != null && !string.IsNullOrEmpty(io.lockedVerb)) finalVerb = io.lockedVerb;
            }
            if (promptText != null) promptText.text = $"{finalVerb} {target.objectName}";

            if (isBlocked && blockedIcon != null)
            {
                iconImage.sprite = blockedIcon;
                iconImage.gameObject.SetActive(true);
            }
            else UpdateIcon(map, action);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            ApplyStencilMask(target);
            targetProgress = 1f; 
        }
        else targetProgress = -1f; 
    }

    void UpdateIcon(string map, string action)
    {
        if (iconImage == null || InputPromptManager.Instance == null) return;
        Sprite btnSprite = InputPromptManager.Instance.GetIconForAction(map, action);
        iconImage.sprite = btnSprite;
        iconImage.gameObject.SetActive(btnSprite != null);
    }

    void ApplyStencilMask(HighlightableObject target)
    {
        Outline outline = target.OutlineComponent;
        if (outline != null)
        {
            int id = outline.CurrentStencilID;
            ApplyMaterialProperties(instancedTextMat, id, 4000);
            ApplyMaterialProperties(instancedIconMat, id, 4000);
            ApplyMaterialProperties(instancedBgMat, id, 4000);
            ApplyMaterialProperties(instancedOutlineMat, id, 4000);
            if (instancedMaskMat != null)
            {
                instancedMaskMat.SetFloat("_Stencil", id);
                instancedMaskMat.SetFloat("_StencilComp", 8); 
                instancedMaskMat.SetFloat("_StencilOp", 2);   
                instancedMaskMat.SetFloat("_ColorMask", 0);   
                instancedMaskMat.renderQueue = 3500;          
            }
        }
    }

    void ApplyMaterialProperties(Material mat, int stencilID, int queue)
    {
        if (mat == null) return;
        mat.SetFloat("_Stencil", stencilID);
        mat.SetFloat("_StencilComp", 6); 
        mat.SetFloat("_ZTest", 8);       
        mat.renderQueue = queue;
    }
}