using UnityEngine;
using UnityEngine.SceneManagement;

// Note: This script does not inherit from MonoBehaviour!
// It is a static class that Unity runs automatically in the background.
public static class CoreAutoLoader
{
    // This attribute tells Unity to run this function the exact moment you hit Play,
    // right BEFORE the main scene actually finishes loading.[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadCoreScene()
    {
        string coreSceneName = "Core"; // This must match your scene name exactly

        // 1. Check if the Core scene is already loaded (in case you opened it manually in the editor)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name == coreSceneName)
            {
                return; // It's already here, do nothing
            }
        }

        // 2. If we didn't find it, load it additively in the background!
        // You MUST have the Core scene added to File -> Build Settings for this to work.
        SceneManager.LoadScene(coreSceneName, LoadSceneMode.Additive);
    }
}