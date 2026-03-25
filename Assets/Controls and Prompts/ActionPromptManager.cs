// Location: ActionPromptManager.cs (Full Update)
using UnityEngine;
using System.Collections.Generic;

public class ActionPromptManager : MonoBehaviour
{
    public static ActionPromptManager Instance;

    [Header("Hierarchy References")]
    public GameObject cardPrefab;
    public Transform container; 
    public float verticalSpacing = 70f;

    [Header("Entrance Settings")]
    public float entranceSpeed = 15f;

    [Header("Exit Settings (Dynamic)")]
    public float baseExitSpeed = 7f;
    public float bonusExitSpeedPerCard = 3f;
    [Tooltip("Multiplier applied when 'fast' mode is triggered for a prompt.")]
    public float fastExitMultiplier = 2.5f;

    [Header("Visual Options")]
    public bool disableExitFade = false;

    private List<ActionPromptCard> cardStack = new List<ActionPromptCard>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public float GetDynamicExitSpeed(bool isFast)
    {
        int closingCount = 0;
        foreach (var c in cardStack) if (c.isClosing) closingCount++;
        
        float speed = baseExitSpeed + (Mathf.Max(0, closingCount - 1) * bonusExitSpeedPerCard);
        return isFast ? speed * fastExitMultiplier : speed;
    }

    public void ShowPrompt(string actionID, string mapName, string actionName, string description)
    {
        if (cardStack.Exists(c => c.ActionID == actionID)) return;

        GameObject go = Instantiate(cardPrefab, container);
        ActionPromptCard card = go.GetComponent<ActionPromptCard>();
        
        Sprite icon = InputPromptManager.Instance.GetIconForAction(mapName, actionName);
        
        float startY = cardStack.Count * verticalSpacing;
        card.Setup(actionID, icon, description, startY);
        
        cardStack.Add(card);
        UpdateStackTargets();
    }

    public void HidePrompt(string actionID, bool fast = false)
    {
        ActionPromptCard card = cardStack.Find(c => c.ActionID == actionID);
        if (card != null) card.Close(fast);
    }

    public bool CanCardSlideOut(ActionPromptCard caller)
    {
        int myIndex = cardStack.IndexOf(caller);
        if (myIndex == 0) return true;

        ActionPromptCard below = cardStack[myIndex - 1];
        if (!below.isClosing) return true;

        return false;
    }

    public void UnregisterCard(ActionPromptCard card)
    {
        if (cardStack.Contains(card))
        {
            cardStack.Remove(card);
            UpdateStackTargets();
        }
    }

    private void UpdateStackTargets()
    {
        for (int i = 0; i < cardStack.Count; i++)
        {
            cardStack[i].SetTargetY(i * verticalSpacing);
        }
    }

    public void ClearAll(bool fast = false)
    {
        // Copy list to avoid modification errors during loop
        List<ActionPromptCard> toClose = new List<ActionPromptCard>(cardStack);
        foreach (var card in toClose) card.Close(fast);
    }
}