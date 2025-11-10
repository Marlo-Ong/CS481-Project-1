// TutorialManager.cs
// Runs a short guided tutorial ONLY when you're in the Tutorial level,
// then unlocks normal play (same board) to finish the match vs AI.
// In any other level, the panel stays hidden and this script disables itself.

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

using SheepGame.Gameplay; // GameController
using SheepGame.Sim;      // ForceInstance

public class TutorialManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI tutorialText; // Assign your TMP text
    [SerializeField] private CanvasGroup panelGroup;        // Assign a CanvasGroup on your panel (optional)

    [Header("Refs")]
    [SerializeField] private GameController gameController; // Assign your GameController in the scene

    [Header("Context")]
    [Tooltip("Which level index in your Level Select is the Tutorial? (0 if Tutorial is the first button)")]
    [SerializeField] private int tutorialLevelIndex = 0;

    [Tooltip("Optional exact scene name for the Tutorial level. Leave empty if you only use LevelIndex.")]
    [SerializeField] private string tutorialSceneName = "Tutorial";

    // Internal state
    bool _tutorialActive = false;           // are we currently running tutorial steps?
    bool _started = false;
    bool _attractorPlacedByPlayer = false;
    bool _repellerPlacedByPlayer = false;

    void Awake()
    {
        // Default hidden
        HidePanel(immediate: true);

        // Decide if we are in the Tutorial level by scene name or by selected level index
        var activeScene = SceneManager.GetActiveScene().name;
        bool inByName = !string.IsNullOrEmpty(tutorialSceneName) && activeScene == tutorialSceneName;
        bool inByIndex = (GameSettings.Level != null && GameSettings.LevelIndex == tutorialLevelIndex);

        _tutorialActive = inByName || inByIndex;

        if (!_tutorialActive)
        {
            // Not the Tutorial level → stay hidden and disable self.
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (gameController != null)
        {
            gameController.ForcePlaced += OnForcePlaced;
        }
    }

    void OnDisable()
    {
        if (gameController != null)
        {
            gameController.ForcePlaced -= OnForcePlaced;
        }
    }

    void Start()
    {
        if (!_tutorialActive) return;

        if (!gameController)
        {
            Debug.LogError("[Tutorial] Missing GameController reference on TutorialManager.");
            enabled = false;
            return;
        }

        // Lock the game for guided steps:
        gameController.TutorialLockToHuman(true);  // keep it the human's turn during steps
        gameController.TutorialBlockAI(true);      // AI won't start acting during steps
        gameController.TutorialHoldSim(true);      // we'll unfreeze as needed during steps

        gameController.ForcePlayerStartsTutorial(0);
        StartCoroutine(RunTutorial());
        _started = true;
    }

    IEnumerator RunTutorial()
    {
        // Intro
        yield return ShowText("Welcome! Let's learn to guide sheep into your pen using forces.", 1.6f);

        // Step 1 — Attractor
        yield return ShowText("Step 1: Press <b>1</b> to select <b>Attractor</b>, then click a tile to place it.");
        gameController.TutorialHoldSim(false);  // allow simulation so sheep can respond
        gameController.TutorialBlockAI(true);   // still block AI during the step
        gameController.TutorialLockToHuman(true);

        // Wait for player's attractor
        _attractorPlacedByPlayer = false;
        yield return new WaitUntil(() => _attractorPlacedByPlayer);

        yield return ShowText("Nice! Sheep move toward attractors.", 1.0f);

        // Step 2 — Repeller
        yield return ShowText("Step 2: Press <b>2</b> to select <b>Repeller</b>, then click to place it.");
        gameController.TutorialHoldSim(false);
        gameController.TutorialBlockAI(true);
        gameController.TutorialLockToHuman(true);

        _repellerPlacedByPlayer = false;
        yield return new WaitUntil(() => _repellerPlacedByPlayer);

        yield return ShowText("Great! Try combining pull and push!", 1.2f);

        // Wrap up — unlock normal play on THIS SAME MATCH (no reload)
        gameController.TutorialHoldSim(true);
        gameController.TutorialBlockAI(true);
        gameController.TutorialLockToHuman(true);

        yield return ShowText("Tutorial complete. Now finish this match against the AI.", 1.2f);

        // Free play in-place
        HidePanel(immediate: true);

        gameController.TutorialHoldSim(false);
        gameController.TutorialBlockAI(false);
        gameController.TutorialLockToHuman(false);

        // Enable: if human runs out, AI auto-places all remaining forces
        gameController.tutorialAutoAIDump = true;
    }

    // ----- EVENT: track what the player placed -----
    void OnForcePlaced(ForceInstance placed)
    {
        if (!_tutorialActive) return;

        // Treat Player 0 as the human during the tutorial
        if (placed.OwnerPlayer != 0) return;

        var s = gameController.State;
        if (s == null || s.ForceTypes == null || placed.ForceTypeIndex < 0 || placed.ForceTypeIndex >= s.ForceTypes.Length)
            return;

        bool isAttractor = s.ForceTypes[placed.ForceTypeIndex].IsAttractor;

        if (isAttractor) _attractorPlacedByPlayer = true;
        else _repellerPlacedByPlayer = true;
    }

    // ----- UI helpers -----
    IEnumerator ShowText(string text, float holdSeconds = -1f)
    {
        if (!_tutorialActive) yield break;

        if (tutorialText) tutorialText.text = text;
        ShowPanel();

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);
        else
            yield return null;
    }

    void ShowPanel()
    {
        if (panelGroup)
        {
            panelGroup.gameObject.SetActive(true);
            panelGroup.alpha = 1f;

            // Important: do NOT block raycasts so world clicks still work during steps
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
        else if (tutorialText)
        {
            tutorialText.gameObject.SetActive(true);
        }
    }

    void HidePanel(bool immediate = false)
    {
        if (panelGroup)
        {
            if (immediate)
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
                panelGroup.gameObject.SetActive(false);
            }
            else
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }
        }
        else if (tutorialText)
        {
            tutorialText.gameObject.SetActive(false);
        }
    }
}
