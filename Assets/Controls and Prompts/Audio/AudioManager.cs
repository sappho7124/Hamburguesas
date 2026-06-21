using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SoundEntry
{
    public string soundName;
    public AudioClip clip;
    [Tooltip("Check this if it's an ambient sound (like a fan) that loops")]
    public bool loop = false;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Tooltip("Add your clips here, and name them what you will call them in Yarn Spinner")]
    public List<SoundEntry> sounds = new List<SoundEntry>();
    
    private Dictionary<string, AudioSource> activeSources = new Dictionary<string, AudioSource>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlaySound(string soundName, float intensity)
    {
        SoundEntry entry = sounds.Find(s => s.soundName == soundName);
        if (entry == null)
        {
            Debug.LogWarning($"[AudioManager] Sound '{soundName}' not found in the database!");
            return;
        }

        // Create an AudioSource for this sound if it doesn't exist yet
        if (!activeSources.ContainsKey(soundName))
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = entry.clip;
            source.loop = entry.loop;
            activeSources[soundName] = source;
        }

        // Apply intensity/volume and play
        activeSources[soundName].volume = Mathf.Clamp01(intensity);
        
        if (!activeSources[soundName].isPlaying)
        {
            activeSources[soundName].Play();
        }
    }

    public void StopSound(string soundName)
    {
        if (activeSources.ContainsKey(soundName))
        {
            activeSources[soundName].Stop();
        }
    }
}