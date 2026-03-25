// Location: ActionPromptCard.cs (Full Update)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class ActionPromptCard : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI actionText;
    public CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private float targetX = 0f;
    private float targetY = 0f;
    private float currentX = -300f; 
    private float dynamicOffScreenX = -300f;
    private bool isFastExit = false;
    public bool isClosing { get; private set; } = false;

    public string ActionID { get; private set; }

    void Awake() => rectTransform = GetComponent<RectTransform>();

    public void Setup(string id, Sprite icon, string text, float startY)
    {
        ActionID = id;
        if (iconImage) iconImage.sprite = icon;
        if (actionText) actionText.text = text;
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        dynamicOffScreenX = -(rectTransform.rect.width + 50f);

        targetY = startY;
        currentX = dynamicOffScreenX; 
        
        transform.localPosition = new Vector3(currentX, startY, 0); 
        canvasGroup.alpha = 0;
    }

    void Update()
    {
        float curY = transform.localPosition.y;
        ActionPromptManager manager = ActionPromptManager.Instance;
        
        if (!isClosing)
        {
            currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * manager.entranceSpeed);
            float nextY = Mathf.Lerp(curY, targetY, Time.deltaTime * manager.entranceSpeed);
            transform.localPosition = new Vector3(currentX, nextY, 0);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1, Time.deltaTime * manager.entranceSpeed);
        }
        else
        {
            float exitSpeed = manager.GetDynamicExitSpeed(isFastExit);
            bool atTargetHeight = Mathf.Abs(curY - targetY) < 1.5f;
            bool canSlideOut = manager.CanCardSlideOut(this);

            if (!atTargetHeight)
            {
                float nextY = Mathf.Lerp(curY, targetY, Time.deltaTime * exitSpeed);
                transform.localPosition = new Vector3(currentX, nextY, 0);
            }
            else if (canSlideOut)
            {
                currentX = Mathf.Lerp(currentX, dynamicOffScreenX, Time.deltaTime * exitSpeed);
                transform.localPosition = new Vector3(currentX, curY, 0);
                if (!manager.disableExitFade) canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0, Time.deltaTime * exitSpeed);
            }

            if (currentX < (dynamicOffScreenX + 5f))
            {
                if (manager.disableExitFade || canvasGroup.alpha < 0.05f)
                {
                    manager.UnregisterCard(this);
                    Destroy(gameObject);
                }
            }
        }
    }

    public void SetTargetY(float y) => targetY = y;
    public void Close(bool fast) { isClosing = true; isFastExit = fast; }
}