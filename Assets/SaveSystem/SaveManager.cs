using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class PlayerDecision
{
    public string key;
    public string value;
}

[System.Serializable]
public class SaveData
{
    public int totalScore = 0;
    public int moneyEarned = 0;
    public int moneyLost = 0;
    public int currentDay = 1; // 1 to 8
    
    // Extensible list for future dialogue choices / flags
    public List<PlayerDecision> decisions = new List<PlayerDecision>();
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;
    
    public SaveData CurrentSave { get; private set; }
    
    // Saves to the device's persistent application path (Safe across updates)
    private string SavePath => Path.Combine(Application.persistentDataPath, "ChalkboardMenuSave.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreateSaveManager()
    {
        // Check if one already exists just in case
        if (Instance == null)
        {
            GameObject go = new GameObject("[AUTO] SaveManager");
            Instance = go.AddComponent<SaveManager>();
            DontDestroyOnLoad(go);
            
            // Auto-load the save file as soon as the game opens
            Instance.LoadGame(); 
        }
    }

    void Awake()
    {
        // Keep this just as a safety net
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public void LoadGame()
    {
        if (HasSave())
        {
            string json = File.ReadAllText(SavePath);
            CurrentSave = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("[SaveManager] Save loaded successfully.");
        }
        else
        {
            CreateNewSave();
        }
    }

    public void CreateNewSave()
    {
        CurrentSave = new SaveData();
        SaveGame();
        Debug.Log("[SaveManager] Created a new save file.");
    }

    public void SaveGame()
    {
        if (CurrentSave == null) CurrentSave = new SaveData();
        
        // true formats it to be readable so you can edit it manually for testing!
        string json = JsonUtility.ToJson(CurrentSave, true); 
        File.WriteAllText(SavePath, json);
    }

    // --- Helper Methods for Extensible Decisions ---
    public void SetDecision(string key, string value)
    {
        if (CurrentSave == null) return;
        
        var decision = CurrentSave.decisions.FirstOrDefault(d => d.key == key);
        if (decision != null)
        {
            decision.value = value;
        }
        else
        {
            CurrentSave.decisions.Add(new PlayerDecision { key = key, value = value });
        }
        SaveGame();
    }

    public string GetDecision(string key, string defaultValue = "")
    {
        if (CurrentSave == null) return defaultValue;
        
        var decision = CurrentSave.decisions.FirstOrDefault(d => d.key == key);
        return decision != null ? decision.value : defaultValue;
    }
}