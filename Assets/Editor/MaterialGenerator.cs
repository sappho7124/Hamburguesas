using UnityEngine;
using UnityEditor;
using System.IO;

public class MaterialGenerator : Editor
{
    // --- FEATURE 1: RIGHT CLICK ANY SHADER TO CREATE MATERIAL ---
    [MenuItem("Assets/Create/Material from Shader", false, 10)]
    private static void CreateMaterialFromSelectedShader()
    {
        // Get selected object
        Object selectedObject = Selection.activeObject;

        // Check if it is a Shader
        if (selectedObject == null || !(selectedObject is Shader))
        {
            Debug.LogError("Selected object is not a Shader!");
            return;
        }

        Shader shader = (Shader)selectedObject;
        string shaderPath = AssetDatabase.GetAssetPath(shader);
        string folderPath = Path.GetDirectoryName(shaderPath);
        
        // Create the material
        Material newMat = new Material(shader);
        
        // Determine save path (Same name as shader, but .mat)
        string materialPath = Path.Combine(folderPath, shader.name.Replace("/", "_") + ".mat");
        materialPath = AssetDatabase.GenerateUniqueAssetPath(materialPath);

        // Save
        AssetDatabase.CreateAsset(newMat, materialPath);
        AssetDatabase.SaveAssets();

        // Highlight the new material
        Selection.activeObject = newMat;
        Debug.Log($"Created Material: {materialPath}");
    }

    // Validation: Only show this menu item if a Shader is selected
    [MenuItem("Assets/Create/Material from Shader", true)]
    private static bool ValidateCreateMaterial()
    {
        return Selection.activeObject is Shader;
    }


    // --- FEATURE 2: AUTO-GENERATE YOUR UI MATERIALS ---
    [MenuItem("Tools/Robot Factory/Generate UI Materials")]
    public static void GenerateInteractionMaterials()
    {
        // 1. Find the Shader
        Shader overlayShader = Shader.Find("Custom/UIOverlay");
        if (overlayShader == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find shader 'Custom/UIOverlay'. Did you create the shader file?", "OK");
            return;
        }

        // Ensure folder exists
        string folder = "Assets/Materials/UI";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/UI")) AssetDatabase.CreateFolder("Assets/Materials", "UI");

        // 2. Create ICON Material
        Material iconMat = new Material(overlayShader);
        iconMat.name = "Mat_UI_Icon_Overlay";
        iconMat.color = Color.white; // Icons usually white so they can be tinted
        SaveMaterial(iconMat, $"{folder}/Mat_UI_Icon_Overlay.mat");

        // 3. Create BACKGROUND Material
        Material bgMat = new Material(overlayShader);
        bgMat.name = "Mat_UI_Background";
        bgMat.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark Grey, Transparent
        SaveMaterial(bgMat, $"{folder}/Mat_UI_Background.mat");

        EditorUtility.DisplayDialog("Success", "Created 'Mat_UI_Icon_Overlay' and 'Mat_UI_Background' in Assets/Materials/UI.", "OK");
    }

    private static void SaveMaterial(Material mat, string path)
    {
        // Check if exists
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            // Update existing instead of overwriting reference
            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, path);
        }
    }
}