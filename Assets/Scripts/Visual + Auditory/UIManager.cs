// UIManager.cs
// Manages UI panels and button interactions in game.

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    }

    void Start()
    {
        ApplySceneUI(SceneManager.GetActiveScene().name);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Update()
    {
        // Allow ESC to toggle pause only during gameplay
        if (SceneManager.GetActiveScene().name == gameplaySceneName && Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode) => ApplySceneUI(s.name);

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
        Show(creditsPanel, true);
    }

    public void OnBackPressed()
    {
        SoundManager.Instance?.PlayClick();
        // Back from Credits or Levels → return to main menu
        Show(creditsPanel, false);
        Show(levelsPanel, false);
        Show(menuPanel, true);
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
    }

    public void OnPlayAgain()
    {
        SoundManager.Instance?.PlayClick();
        isPaused = false;
        HideAllPanels();
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
        OnPauseStateChanged?.Invoke(false);
    }

}