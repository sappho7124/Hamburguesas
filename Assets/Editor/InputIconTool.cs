using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class InputIconTool : EditorWindow
{
    private InputIconDatabase targetDatabase;
    private Texture2D spriteSheetTexture;

    [MenuItem("Tools/Robot Factory/Update Input Icons")]
    public static void ShowWindow()
    {
        GetWindow<InputIconTool>("Input Icons");
    }

    void OnGUI()
    {
        GUILayout.Label("Input Icon Database Populator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 1. Reference Fields
        targetDatabase = (InputIconDatabase)EditorGUILayout.ObjectField("Target Database", targetDatabase, typeof(InputIconDatabase), false);
        spriteSheetTexture = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", spriteSheetTexture, typeof(Texture2D), false);

        EditorGUILayout.Space();

        // 2. Action Button
        if (GUILayout.Button("Populate Database"))
        {
            if (targetDatabase == null || spriteSheetTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign both the Database and the Sprite Sheet.", "OK");
                return;
            }
            
            PopulateDatabase();
        }

        // Instructions
        EditorGUILayout.HelpBox("This tool reads 'Prompts_PC_Black_*' sprites from the texture and maps them to Unity Input System paths automatically.", MessageType.Info);
    }

    private void PopulateDatabase()
    {
        // 1. Get all assets inside the texture (The sliced sprites)
        string path = AssetDatabase.GetAssetPath(spriteSheetTexture);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

        // Filter only Sprites
        List<Sprite> sprites = allAssets.OfType<Sprite>().ToList();

        if (sprites.Count == 0)
        {
            Debug.LogError("No sprites found! Check Texture Import Settings (Sprite Mode: Multiple).");
            return;
        }

        // Record Undo so you can Ctrl+Z if it messes up
        Undo.RecordObject(targetDatabase, "Populate Input Icons");

        targetDatabase.icons.Clear();
        int count = 0;

        foreach (Sprite sprite in sprites)
        {
            string spriteName = sprite.name;

            // --- PARSING LOGIC ---
            
            // Only want "Prompts_PC_Black_" items
            if (!spriteName.Contains("Prompts_PC_Black_")) continue;

            // Skip variants (_2, _3, _Alt)
            if (spriteName.EndsWith("_2") || spriteName.EndsWith("_3") || spriteName.EndsWith("_Alt")) continue;

            // Remove prefix
            string keyName = spriteName.Replace("Prompts_PC_Black_", "");

            // Handle the "_1" variant
            if (keyName.EndsWith("_1"))
            {
                keyName = keyName.Substring(0, keyName.Length - 2);
            }

            // Convert to Path
            string inputPath = ConvertNameToInputPath(keyName);

            if (!string.IsNullOrEmpty(inputPath))
            {
                targetDatabase.icons.Add(new InputIconDatabase.IconMapping
                {
                    inputPath = inputPath,
                    icon = sprite
                });
                count++;
            }
        }

        // Save changes
        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets(); // Ensure it writes to disk
        
        EditorUtility.DisplayDialog("Success", $"Successfully mapped {count} icons to the database!", "OK");
    }

    private string ConvertNameToInputPath(string suffix)
    {
        // 1. Mouse
        if (suffix.StartsWith("Mouse_"))
        {
            string btn = suffix.Replace("Mouse_", ""); 
            return $"<Mouse>/{btn.ToLower()}Button"; 
        }

        // 2. Arrows
        if (suffix.StartsWith("Arrow_"))
        {
            string dir = suffix.Replace("Arrow_", "").ToLower();
            return $"<Keyboard>/{dir}Arrow"; 
        }

        // 3. F-Keys
        if (suffix.StartsWith("F") && suffix.Length > 1 && char.IsDigit(suffix[1]))
        {
             return $"<Keyboard>/{suffix.ToLower()}";
        }

        // 4. Special Keys
        switch (suffix)
        {
            case "Space": return "<Keyboard>/space";
            case "Enter": return "<Keyboard>/enter";
            case "Esc": return "<Keyboard>/escape";
            case "Tab": return "<Keyboard>/tab";
            case "Shift": return "<Keyboard>/leftShift"; 
            case "Ctrl": return "<Keyboard>/leftCtrl";
            case "Command": return "<Keyboard>/leftCommand";
            case "Windows": return "<Keyboard>/leftMeta"; 
            case "Del": return "<Keyboard>/delete";
            case "Ins": return "<Keyboard>/insert";
            case "Home": return "<Keyboard>/home";
            case "End": return "<Keyboard>/end";
            case "PageUp": return "<Keyboard>/pageUp";
            case "PageDown": return "<Keyboard>/pageDown";
            case "BackSpace": return "<Keyboard>/backspace";
            case "CapsLock": return "<Keyboard>/capsLock";
            case "NumLock": return "<Keyboard>/numLock";
            case "PrtScrn": return "<Keyboard>/printScreen";
            
            // Symbols
            case "Minus": return "<Keyboard>/minus";
            case "Plus": return "<Keyboard>/equals"; 
            case "Bracket_Left": return "<Keyboard>/leftBracket";
            case "Bracket_Right": return "<Keyboard>/rightBracket";
            case "SemiColon": return "<Keyboard>/semicolon";
            case "Quotes": return "<Keyboard>/quote";
            case "Comma": return "<Keyboard>/comma";
            case "Period": return "<Keyboard>/period";
            case "Slash": return "<Keyboard>/slash";
            case "Backslash": return "<Keyboard>/backslash";
            case "Tilde": return "<Keyboard>/backquote";
            case "Question": return "<Keyboard>/slash"; 
            case "Asterisk": return "<Keyboard>/numpadMultiply";
            case "Less-than": return "<Keyboard>/comma";
            case "Greater-than": return "<Keyboard>/period"; 
            case "Colon": return "<Keyboard>/semicolon"; 
            case "Mouse_Empty": return "<Mouse>/delta"; 
        }

        // 5. Single Letters/Numbers
        if (suffix.Length == 1)
        {
            return $"<Keyboard>/{suffix.ToLower()}";
        }

        return "";
    }
}