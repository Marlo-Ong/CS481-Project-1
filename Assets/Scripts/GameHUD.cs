// GameHUD.cs
using SheepGame.Gameplay;
using SheepGame.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using System.Collections;

public class GameHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GameController controller;
    [SerializeField] InputPlacementController inputCtrl;

    // =========================
    // PLAYER HUD (Top-Left)
    // =========================
    [Header("Player HUD (Top-Left)")]
    [SerializeField] TMP_Text currentPlayerTxt;
    [SerializeField] TMP_Text playerScoreTxt;
    [SerializeField] TMP_Text difficultyTxt;
    [SerializeField] TMP_Text forcesTxt;
    [SerializeField] Slider turnProgress;

    // =========================
    // AI HUD (Top-Right)
    // =========================
    [Header("AI HUD (Top-Right)")]
    [SerializeField] TMP_Text aiHeaderTxt;
    [SerializeField] TMP_Text aiScoreTxt;
    [SerializeField] TMP_Text aiDifficultyTxt;
    [SerializeField] TMP_Text aiForcesTxt;
    [SerializeField] Slider aiTurnProgress;

    [Header("Player Assignment")]
    [SerializeField] int humanPlayerIndex = 0;
    [SerializeField] int aiPlayerIndex = 1;

    // --- Add to class fields ---
    [Header("Turn Banner")]
    [SerializeField] CanvasGroup turnBannerCg;     // assign the CanvasGroup on TurnBanner
    [SerializeField] TMP_Text turnBannerText;      // assign TurnBannerText

    int _lastPlayer = -1;                          // track turn swaps
    Coroutine _bannerRoutine;

    int lastSelectedType = 0;

    void Start()
    {
        if (!controller) controller = FindFirstObjectByType<GameController>();
        if (!inputCtrl) inputCtrl = FindFirstObjectByType<InputPlacementController>();
        if (controller) controller.StateSet += _ => Refresh(full: true);

        if (turnBannerCg) { turnBannerCg.alpha = 0f; turnBannerCg.gameObject.SetActive(false); }
        Refresh(full: true);
    }

    void Update() => Refresh();

    void Refresh(bool full = false)
    {
        if (!controller || controller.State == null) return;
        var s = controller.State;

        if (inputCtrl) lastSelectedType = inputCtrl.GetSelectedType();

        bool simulating = controller.IsSimulating;
        int cur = s.CurrentPlayer;
        bool isPlayersTurn = cur == humanPlayerIndex;
        bool isAITurn = cur == aiPlayerIndex;

        // -------- PLAYER (Left) --------
        if (currentPlayerTxt)
        {
            string who = isPlayersTurn ? "(You)" : "";
            currentPlayerTxt.text = $"<b>Player {who}</b>";
        }

        if (playerScoreTxt)
        {
            // Only show your own score on the left
            playerScoreTxt.text = $"Your Score: {s.Score[humanPlayerIndex]}";
        }

        if (turnProgress)
        {
            // Only show the bar on the left during the player's (human) simulating turn
            bool show = simulating && isPlayersTurn;
            turnProgress.gameObject.SetActive(show);
            if (show)
            {
                turnProgress.minValue = 0;
                turnProgress.maxValue = Mathf.Max(1, AdaptiveDifficulty.TicksPerTurn);
                turnProgress.value = controller.TicksPerformedThisTurn;
            }
        }

        if (difficultyTxt)
        {
            difficultyTxt.gameObject.SetActive(true);
            difficultyTxt.text = $"AI Depth {AdaptiveDifficulty.TargetDepth} • {AdaptiveDifficulty.TicksPerTurn} tpt";
        }

        if (forcesTxt)
        {
            // Left side only dims when it is NOT the player's turn
            forcesTxt.text = BuildForcesBlock(
                header: "<b>Forces</b>:\n<size=90%>Press 1 or 2 to select</size>",
                playerIndex: humanPlayerIndex,
                isSideTurn: isPlayersTurn,
                allowSelection: isPlayersTurn && !simulating,   // only selectable on your turn and not simulating
                selectedIndex: lastSelectedType
            );
        }

        // -------- AI (Right) --------
        if (aiHeaderTxt)
        {
            string tag = isAITurn ? "(Thinking…)" : "";
            aiHeaderTxt.text = $"<b>AI {tag} </b>";
        }

        if (aiScoreTxt)
        {
            // Only show AI's own score on the right
            aiScoreTxt.text = $"AI Score: {s.Score[aiPlayerIndex]}";
        }

        if (aiTurnProgress)
        {
            // Only show the bar on the right during the AI simulating turn
            bool show = simulating && isAITurn;
            aiTurnProgress.gameObject.SetActive(show);
            if (show)
            {
                aiTurnProgress.minValue = 0;
                aiTurnProgress.maxValue = Mathf.Max(1, AdaptiveDifficulty.TicksPerTurn);
                aiTurnProgress.value = controller.TicksPerformedThisTurn;
            }
        }

        if (aiDifficultyTxt)
        {
            aiDifficultyTxt.gameObject.SetActive(true);
            aiDifficultyTxt.text = $"AI Depth {AdaptiveDifficulty.TargetDepth} • {AdaptiveDifficulty.TicksPerTurn} tpt";
        }

        if (aiForcesTxt)
        {
            // AI side: never selectable, but only dim when it's NOT the AI's turn
            aiForcesTxt.text = BuildForcesBlock(
                header: "<b>AI Forces:</b> \n (view only)",
                playerIndex: aiPlayerIndex,
                isSideTurn: isAITurn,
                allowSelection: false,
                selectedIndex: -1
            );
        }

        // --- Turn banner: show only when the turn CHANGES ---
        if (_lastPlayer != cur)
        {
            _lastPlayer = cur;

            if (turnBannerText && turnBannerCg)
            {
                bool playersTurn = (cur == humanPlayerIndex);
                turnBannerText.text = playersTurn ? "Your Turn!" : "AI's Turn";

                if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
                _bannerRoutine = StartCoroutine(ShowTurnBanner());
            }
        }
    }

    string BuildForcesBlock(string header, int playerIndex, bool isSideTurn, bool allowSelection, int selectedIndex)
    {
        var s = controller.State;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(header);

        for (int i = 0; i < s.ForceTypes.Length; i++)
        {
            int rem = controller.RemainingForType(playerIndex, i);
            var spec = s.ForceTypes[i];
            string name = spec.IsAttractor ? "Attractor" : "Repeller";

            // If it's not this side's turn, dim everything regardless of counts.
            bool dim = !isSideTurn;

            // The human side can highlight the selected item on their turn.
            bool selected = allowSelection && (i == selectedIndex);

            string sel = allowSelection ? (selected ? " > " : "    ") : "    ";
            string colorStart = dim ? "<color=#777777>" : "";
            string colorEnd = dim ? "</color>" : "";

            sb.AppendLine($"{sel}{colorStart}[{i + 1}] {name}  <b>x{rem}</b>{colorEnd}");
        }

        return sb.ToString();
    }

    public void SetSelectedTypeHUD(int index)
    {
        lastSelectedType = Mathf.Clamp(index, 0, (controller?.State?.ForceTypes.Length ?? 1) - 1);
        inputCtrl?.SendMessage("SetSelectedType", lastSelectedType, SendMessageOptions.DontRequireReceiver);
        Refresh();
    }

    IEnumerator ShowTurnBanner()
    {
        // prepare
        turnBannerCg.gameObject.SetActive(true);
        var rt = (RectTransform)turnBannerCg.transform;
        float startY = -50f, endY = -40f;  // slight slide down
        float t;

        // fade/slide in (0.2s)
        t = 0f; turnBannerCg.alpha = 0f; rt.anchoredPosition = new Vector2(0f, startY);
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float k = t / 0.2f;
            turnBannerCg.alpha = Mathf.SmoothStep(0f, 1f, k);
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, k));
            yield return null;
        }
        turnBannerCg.alpha = 1f; rt.anchoredPosition = new Vector2(0f, endY);

        // hold (0.8s)
        yield return new WaitForSeconds(0.8f);

        // fade out (0.5s)
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float k = t / 0.5f;
            turnBannerCg.alpha = 1f - k;
            yield return null;
        }
        turnBannerCg.alpha = 0f;
        turnBannerCg.gameObject.SetActive(false);
    }

}
