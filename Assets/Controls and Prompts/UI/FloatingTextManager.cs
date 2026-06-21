using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance;

    [Header("References")]
    [Tooltip("The UI Panel where the words will spawn")]
    public RectTransform panelRect;
    [Tooltip("A Prefab with a TextMeshProUGUI and the FloatingWord script")]
    public GameObject floatingWordPrefab;

    [Header("Spawn Settings")]
    public float spawnRate = 0.15f;
    public float minScale = 0.8f;
    public float maxScale = 1.8f;
    
    [Header("Colors")]
    public Color[] brightColors = new Color[] {
        Color.red, Color.magenta, Color.cyan, Color.yellow, Color.green, new Color(1f, 0.5f, 0f) // Orange
    };

    private bool isSpawning = false;
    private float spawnTimer = 0f;
    private List<string> activeWords = new List<string>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (!isSpawning || activeWords.Count == 0) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnRate)
        {
            spawnTimer = 0f;
            SpawnWord();
        }
    }

    public void StartWords(string commaSeparatedWords)
    {
        activeWords.Clear();
        string[] split = commaSeparatedWords.Split(',');
        foreach (var s in split)
        {
            if (!string.IsNullOrWhiteSpace(s))
                activeWords.Add(s.Trim());
        }
        
        isSpawning = true;
    }

    public void StopWords()
    {
        isSpawning = false;
        
        // Destroy all currently floating words immediately
        foreach(Transform child in panelRect)
        {
            Destroy(child.gameObject);
        }
    }

    private void SpawnWord()
    {
        if (floatingWordPrefab == null || panelRect == null) return;

        GameObject go = Instantiate(floatingWordPrefab, panelRect);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        RectTransform rt = go.GetComponent<RectTransform>();

        // 1. Pick random word
        tmp.text = activeWords[Random.Range(0, activeWords.Count)];

        // 2. Pick random bright color
        if (brightColors.Length > 0)
        {
            tmp.color = brightColors[Random.Range(0, brightColors.Length)];
        }

        // 3. Set random position within the Panel bounds
        float x = Random.Range(panelRect.rect.xMin, panelRect.rect.xMax);
        float y = Random.Range(panelRect.rect.yMin, panelRect.rect.yMax);
        rt.anchoredPosition = new Vector2(x, y);

        // 4. Random rotation (slight tilt)
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
        
        // 5. Random scale
        float scale = Random.Range(minScale, maxScale);
        rt.localScale = new Vector3(scale, scale, 1f);
    }
}