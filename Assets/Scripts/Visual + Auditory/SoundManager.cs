// soundManager.cs
// A simple sound manager for playing UI and gameplay sound effects and music.

using UnityEngine;

public class SoundManager : MonoBehaviour
{
    // Simple scene-local singleton (no DontDestroyOnLoad)
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("2D AudioSource for one-shots (UI + SFX).")]
    public AudioSource sfxSource;
    [Tooltip("Looping music for this scene (optional).")]
    public AudioSource musicSource;

    [Header("BG Clip")]
    public AudioClip bgMusicClip;

    [Header("UI Clips")]
    public AudioClip clickClip;

    [Header("Gameplay Clips")]
    public AudioClip placeForceClip;
    public AudioClip captureClip;

    [Header("Result Clips")]
    public AudioClip winClip;
    public AudioClip loseClip;

    void Awake()
    {
        // Singleton pattern (keeps one instance alive across scenes)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Auto-play background music if assigned
        if (musicSource && bgMusicClip)
        {
            musicSource.clip = bgMusicClip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    // --- UI ---
    public void PlayClick() => PlayOneShot(clickClip);

    // --- Gameplay ---
    public void PlayPlaceForce() => PlayOneShot(placeForceClip);
    public void PlayCapture() => PlayOneShot(captureClip);

    // --- Result ---
    public void PlayResult(bool didWin) => PlayOneShot(didWin ? winClip : loseClip);

    // --- Music helpers (optional) ---
    public void SetMusic(AudioClip clip, float volume = 0.35f, bool loop = true)
    {
        if (!musicSource || !clip) return;
        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.loop = loop;
        musicSource.Play();
    }

    // --- internal ---
    private void PlayOneShot(AudioClip clip)
    {
        if (sfxSource && clip) sfxSource.PlayOneShot(clip);
    }
}
