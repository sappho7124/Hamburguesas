using UnityEngine;
using UnityEngine.InputSystem;

public class InputPromptManager : MonoBehaviour
{
    public static InputPromptManager Instance;

    [Header("References")]
    public InputIconDatabase iconDatabase;
    
    private Player_Controls controls;

    void Awake()
    {
        if (Instance == null) Instance = this;
        controls = new Player_Controls();
    }

    public Sprite GetIconForAction(string mapName, string actionName)
    {
        // 1. Find Map
        InputActionMap map = controls.asset.FindActionMap(mapName);
        if (map == null) 
        {
            Debug.LogError($"[InputPromptManager] Could not find Action Map: '{mapName}'");
            return null;
        }

        // 2. Find Action
        InputAction action = map.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"[InputPromptManager] Could not find Action: '{actionName}' in map '{mapName}'");
            return null;
        }

        // 3. Find Binding Index (Active Control Scheme)
        // We check Keyboard first for PC testing
        int bindingIndex = action.GetBindingIndexForControl(Keyboard.current);
        
        // If keyboard isn't active/found, just grab the first valid binding
        if (bindingIndex == -1) 
        {
             // Debug.LogWarning($"[InputPromptManager] No keyboard binding for {actionName}, using default.");
             bindingIndex = 0; 
        }

        // 4. Get Path
        string rawPath = action.bindings[bindingIndex].effectivePath;
        
        // Debug Log to see what Unity is giving us vs what is in your database
        // Debug.Log($"[InputPromptManager] Searching for path: '{rawPath}' for action '{actionName}'");

        // 5. Database Lookup
        Sprite icon = iconDatabase.GetSprite(rawPath);
        
        if (icon == null)
        {
            Debug.LogWarning($"[InputPromptManager] Icon missing for path: '{rawPath}' (Action: {actionName})");
        }

        return icon;
    }
}