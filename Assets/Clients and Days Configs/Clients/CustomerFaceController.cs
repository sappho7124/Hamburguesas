using UnityEngine;

public class CustomerFaceController : MonoBehaviour
{
    public enum Mood { Neutral, Happy, Sad, Scared, Puking, Dead, Angry, ReallyAngry }

    [System.Serializable]
    public struct FaceMapping
    {
        public Mood mood;
        [Tooltip("The X,Y offset for this face on the texture atlas")]
        public Vector2 uvOffset; 
    }

    [Header("References")]
    public Renderer faceRenderer;
    [Tooltip("If the face is the second material on the mesh, this is 1. If it's a separate mesh, it's 0.")]
    public int materialIndex = 0;

    [Header("Face Grid Settings")]
    // Note: Tiling is now configured directly on the Material's "Main Texture" properties!
    public FaceMapping[] faceMappings;

    private Material instancedFaceMaterial;
    public Mood CurrentMood { get; private set; } = Mood.Neutral;

    void Awake()
    {
        if (faceRenderer != null)
        {
            // Instance the material so changing this character's face doesn't change other characters
            instancedFaceMaterial = faceRenderer.materials[materialIndex];
        }
    }

    void Start()
    {
        SetMood(Mood.Neutral);
    }

    public void SetMood(Mood newMood)
    {
        if (instancedFaceMaterial == null) return;
        
        CurrentMood = newMood;

        foreach (var mapping in faceMappings)
        {
            if (mapping.mood == newMood)
            {
                // Slide the texture to the correct face (Changes Offset without touching Tiling)
                instancedFaceMaterial.mainTextureOffset = mapping.uvOffset;
                return;
            }
        }
    }

    // Helper for the wait timer: Translates 0.0 to 1.0 into escalating anger
    public void UpdateWaitMood(float patiencePercent)
    {
        // Don't override extreme reactions if they are reacting to food
        if (CurrentMood == Mood.Puking || CurrentMood == Mood.Dead || CurrentMood == Mood.Scared) return;

        if (patiencePercent < 0.5f) SetMood(Mood.Neutral);
        else if (patiencePercent < 0.85f) SetMood(Mood.Angry);
        else SetMood(Mood.ReallyAngry);
    }

    void OnDestroy()
    {
        // Clean up the instanced material to prevent memory leaks!
        if (instancedFaceMaterial != null)
        {
            Destroy(instancedFaceMaterial);
        }
    }
}