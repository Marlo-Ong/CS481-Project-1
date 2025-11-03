// UIManager.cs
// Manages UI panels and button interactions in game.

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject creditsPanel;
    public GameObject levelsPanel;
    public GameObject pausePanel;
    public GameObject winPanel;
    public GameObject endPanel;

    [Header("Scene Names")]
    public string gameplaySceneName = "SampleScene"; // Change as needed
    public string startSceneName = "StartScene";

    [Header("Win/Loss UI")]
    public TMP_Text resultText;

    // Singleton
    private static UIManager _instance;
    public static UIManager Instance => _instance;

    public static event System.Action<bool> OnPauseStateChanged;

    private bool isPaused;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    void Start()
    {
        ApplySceneUI(SceneManager.GetActiveScene().name);
    }

    void Update()
    {
        // Allow ESC to toggle pause only during gameplay
        if (SceneManager.GetActiveScene().name == gameplaySceneName && Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!PlayerPrefs.HasKey("AI_Depth")) return;

        int depth = PlayerPrefs.GetInt("AI_Depth", 2);
        int tpt = PlayerPrefs.GetInt("AI_TPT", 50);

        // Apply depth back to AI (private field)
        var ai = FindFirstObjectByType<SheepGame.Gameplay.AIAgentController>();
        if (ai)
        {
            var f = typeof(SheepGame.Gameplay.AIAgentController)
                .GetField("depth", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(ai, Mathf.Clamp(depth, 1, 4));
        }

        // Apply ticks/turn back to controller
        var gc = FindFirstObjectByType<SheepGame.Gameplay.GameController>();
        if (gc) gc.Config.ticksPerTurn = tpt;

        // Clean up (optional): remove keys so next run starts fresh default if needed
        // PlayerPrefs.DeleteKey("AI_Depth");
        // PlayerPrefs.DeleteKey("AI_TPT");
    }

    private void ApplySceneUI(string sceneName)
    {
        HideAllPanels();

        if (sceneName == startSceneName)
        {
            Show(menuPanel, true);
        }
        else if (sceneName == gameplaySceneName)
        {
            // Gameplay starts clean, no panels open
            isPaused = false;
        }
    }

    // ---------- General helpers ----------
    private void HideAllPanels()
    {
        Show(menuPanel, false);
        Show(creditsPanel, false);
        Show(levelsPanel, false);
        Show(pausePanel, false);
        Show(winPanel, false);
        Show(endPanel, false);
    }

    private static void Show(GameObject go, bool v) { if (go) go.SetActive(v); }

    // ---------- Menu ----------
    public void OnPlayPressed()
    {
        SoundManager.Instance?.PlayClick();
        Show(levelsPanel, true);
        Show(menuPanel, false);
    }

    public void OnSelectLevel()
    {
        SoundManager.Instance?.PlayClick();
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnCreditsPressed()
    {
        SoundManager.Instance?.PlayClick();
        Show(menuPanel, false);
        Show(endPanel, false);
        Show(creditsPanel, true);
    }

    public void OnBackPressed()
    {
        SoundManager.Instance?.PlayClick();
        // Back from Credits or Levels → return to main menu
        Show(creditsPanel, false);
        Show(levelsPanel, false);

        if (SceneManager.GetActiveScene().name == startSceneName)
        {
            Show(menuPanel, true);
        }
        if (SceneManager.GetActiveScene().name == gameplaySceneName)
        {
            Show(endPanel, true);
        }
    }

    public void OnQuitPressed()
    {
        SoundManager.Instance?.PlayClick();
        Application.Quit();
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    // ---------- Pause ----------
    public void TogglePause()
    {
        isPaused = !isPaused;
        Show(pausePanel, isPaused);
        OnPauseStateChanged?.Invoke(isPaused);
    }

    public void OnResumePressed()
    {
        SoundManager.Instance?.PlayClick();
        isPaused = false;
        Show(pausePanel, false);
        OnPauseStateChanged?.Invoke(false);
    }

    public void OnMainMenuPressed()
    {
        SoundManager.Instance?.PlayClick();
        isPaused = false;
        HideAllPanels();
        SceneManager.LoadScene(startSceneName);
    }

    // ---------- Win/Loss ----------
    public void OnShowResult(bool didWin)
    {
        if (!winPanel) return;

        Show(winPanel, true);
        isPaused = true;
        OnPauseStateChanged?.Invoke(true);

        if (resultText)
        {
            resultText.text = didWin ? "You Win!" : "You Lose!";
            resultText.color = didWin ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.2f);
        }
        SoundManager.Instance.PlayResult(didWin);
    }

    public void OnRestartLevel()
    {
        SoundManager.Instance?.PlayClick();
        isPaused = false;
        HideAllPanels();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnPlayAgain()
    {
        // Read current depth from AIAgentController (private field) and ticks/turn from GameController
        var ai = FindFirstObjectByType<SheepGame.Gameplay.AIAgentController>();
        var gc = FindFirstObjectByType<SheepGame.Gameplay.GameController>();

        int depth = 2;
        if (ai)
        {
            var f = typeof(SheepGame.Gameplay.AIAgentController)
                .GetField("depth", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) depth = (int)f.GetValue(ai);
        }
        int tpt = gc ? gc.Config.ticksPerTurn : 50;

        // Persist once
        PlayerPrefs.SetInt("AI_Depth", depth);
        PlayerPrefs.SetInt("AI_TPT", tpt);
        PlayerPrefs.Save();

        // Reload scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnResultToMenu()
    {
        SoundManager.Instance?.PlayClick();
        isPaused = false;
        HideAllPanels();
        SceneManager.LoadScene(startSceneName);
    }

    public void OnContinuePressed()
    {
        SoundManager.Instance?.PlayClick();
        Show(pausePanel, false);
        Show(endPanel, true);
        Show(winPanel, false);
        OnPauseStateChanged?.Invoke(false);
    }

}