using UnityEngine;
using TMPro;

public class FloatingWord : MonoBehaviour
{
    [Header("Animation Settings")]
    public float floatSpeed = 30f;
    public float lifetime = 2f;
    public float fadeInTime = 0.2f;
    public float fadeOutTime = 0.5f;

    private TextMeshProUGUI tmp;
    private RectTransform rt;
    private float age = 0f;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        age += Time.deltaTime;
        
        // Float upwards
        rt.anchoredPosition += Vector2.up * floatSpeed * Time.deltaTime;

        // Smooth fade in and fade out
        float alpha = 1f;
        if (age < fadeInTime) alpha = age / fadeInTime;
        else if (age > lifetime - fadeOutTime) alpha = (lifetime - age) / fadeOutTime;

        if (tmp != null)
        {
            Color c = tmp.color;
            c.a = Mathf.Clamp01(alpha);
            tmp.color = c;
        }

        // Clean up
        if (age >= lifetime) Destroy(gameObject);
    }
}