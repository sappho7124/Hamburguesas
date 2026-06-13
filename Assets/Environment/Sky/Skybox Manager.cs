using UnityEngine;

public class SkyboxManager : MonoBehaviour
{
    [Tooltip("Speed of the skybox rotation.")]
    public float rotationSpeed = 1.0f;

    private float currentRotation = 0f;

    void Update()
    {
        // Calculate the new rotation over time
        currentRotation += rotationSpeed * Time.deltaTime;

        // Keep the value looping cleanly between 0 and 360 degrees
        currentRotation %= 360f;

        // Apply the rotation value directly to the global skybox shader property
        RenderSettings.skybox.SetFloat("_Rotation", currentRotation);
    }
}
