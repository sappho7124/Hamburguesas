using UnityEngine;
using UnityEditor;

public class ReplaceToonShader : EditorWindow
{
    // The exact names of the shaders in the Shader "" declaration
    private const string OLD_SHADER_NAME = "Toon/Toon 3D as 2D (URP)";
    private const string NEW_SHADER_NAME = "Modified Toon/Toon 3D as 2D (URP)";

    [MenuItem("Tools/Replace Toon Shaders")]
    public static void ReplaceShaders()
    {
        // 1. Find the new shader to make sure it actually exists and compiled correctly
        Shader newShader = Shader.Find(NEW_SHADER_NAME);
        if (newShader == null)
        {
            Debug.LogError($"[Shader Replacer] Could not find the new shader '{NEW_SHADER_NAME}'. Please check the Shader \"Name\" at the top of your shader file!");
            return;
        }

        // 2. Find ALL materials in the entire project
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        int replacedCount = 0;

        try
        {
            for (int i = 0; i < materialGuids.Length; i++)
            {
                // Update progress bar so the editor doesn't look frozen
                EditorUtility.DisplayProgressBar("Replacing Shaders", $"Checking material {i + 1}/{materialGuids.Length}", (float)i / materialGuids.Length);

                string assetPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                // 3. Check if the material is using the old shader
                if (mat != null && mat.shader != null && mat.shader.name == OLD_SHADER_NAME)
                {
                    // Record undo so you can Ctrl+Z if you make a mistake
                    Undo.RecordObject(mat, "Replace Shader");
                    
                    // Assign the new shader
                    mat.shader = newShader;
                    
                    // Tell Unity this asset has been changed and needs to be saved
                    EditorUtility.SetDirty(mat);
                    replacedCount++;
                }
            }
        }
        finally
        {
            // 4. Always clear the progress bar even if an error happens
            EditorUtility.ClearProgressBar();
        }

        // 5. Save all the changed materials to disk
        if (replacedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green><b>[Shader Replacer] Success!</b></color> Replaced '{OLD_SHADER_NAME}' with '{NEW_SHADER_NAME}' on <b>{replacedCount}</b> materials.");
        }
        else
        {
            Debug.Log($"[Shader Replacer] No materials found using '{OLD_SHADER_NAME}'.");
        }
    }
}