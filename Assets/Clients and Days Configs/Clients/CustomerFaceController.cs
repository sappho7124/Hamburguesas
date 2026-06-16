using UnityEngine;

public class CustomerFaceController : MonoBehaviour
{
    public enum Mood { Neutral, Happy, Sad, Scared, Puking, Dead, Angry, ReallyAngry }

    [System.Serializable]
    public struct FaceMapping
    {
        public Mood mood;
        [Tooltip("The X,Y offset for this face when the mouth is CLOSED")]
        public Vector2 uvOffset; 
        [Tooltip("The X,Y offset for this face when the mouth is OPEN (Talking)")]
        public Vector2 openMouthUV; 
    }

    [Header("References")]
    public Renderer faceRenderer;
    [Tooltip("If the face is the second material on the mesh, this is 1. If it's a separate mesh, it's 0.")]
    public int materialIndex = 0;

    [Header("Face Grid Settings")]
    public FaceMapping[] faceMappings;

    private Material instancedFaceMaterial;
    public Mood CurrentMood { get; private set; } = Mood.Neutral;
    
    private bool isTalking = false;

    void Awake()
    {
        if (faceRenderer != null)
        {
            // Instance the material so changing this character's face doesn't change others
            instancedFaceMaterial = faceRenderer.materials[materialIndex];
        }
    }

    void Start()
    {
        SetMood(Mood.Neutral);
    }

    public void SetMood(Mood newMood)
    {
        CurrentMood = newMood;
        UpdateFaceVisuals();
    }

    public void SetTalking(bool talking)
    {
        isTalking = talking;
        UpdateFaceVisuals();
    }

    private void UpdateFaceVisuals()
    {
        if (instancedFaceMaterial == null) return;

        foreach (var mapping in faceMappings)
        {
            if (mapping.mood == CurrentMood)
            {
                // Slide the texture to the correct face based on talking state
                instancedFaceMaterial.mainTextureOffset = isTalking ? mapping.openMouthUV : mapping.uvOffset;
                return;
            }
        }
    }

    public void UpdateWaitMood(float patiencePercent)
    {
        if (CurrentMood == Mood.Puking || CurrentMood == Mood.Dead || CurrentMood == Mood.Scared) return;

        if (patiencePercent < 0.5f) SetMood(Mood.Neutral);
        else if (patiencePercent < 0.85f) SetMood(Mood.Angry);
        else SetMood(Mood.ReallyAngry);
    }

    void OnDestroy()
    {
        if (instancedFaceMaterial != null) Destroy(instancedFaceMaterial);
    }
}