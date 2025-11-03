// GameHUD.cs
// handles the display of player, score, and other relevant game information.

using SheepGame.Gameplay;
using SheepGame.Sim;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GameController controller;
    [SerializeField] InputPlacementController inputCtrl;

    [Header("UI")]
    [SerializeField] TMP_Text currentPlayerTxt;
    [SerializeField] TMP_Text scoreTxt;
    [SerializeField] TMP_Text difficultyTxt;
    [SerializeField] TMP_Text forcesTxt;
    [SerializeField] Slider turnProgress;

    int lastSelectedType = 0;

    void Start()
    {
        if (!controller) controller = FindFirstObjectByType<GameController>();
        if (!inputCtrl) inputCtrl = FindFirstObjectByType<InputPlacementController>();

        if (controller) controller.StateSet += _ => Refresh(full: true);
        Refresh(full: true);
    }

    void Update() => Refresh();

    void Refresh(bool full = false)
    {
        if (!controller || controller.State == null) return;
        var s = controller.State;

        // keep UI selection in sync with input (so pressing 1/2 updates the arrow)
        if (inputCtrl) lastSelectedType = inputCtrl.GetSelectedType();

        // --- player / score
        bool aiTurn = controller.IsAITurn;
        currentPlayerTxt.text = $"Player {s.CurrentPlayer}'s Turn {(aiTurn ? "(AI)" : "(You)")}";
        scoreTxt.text = $"P0 Score: {s.Score[0]}   P1 Score: {s.Score[1]}";

        // --- progress bar
        if (turnProgress)
        {
            turnProgress.gameObject.SetActive(controller.IsSimulating);
            turnProgress.minValue = 0;
            turnProgress.maxValue = Mathf.Max(1, AdaptiveDifficulty.TicksPerTurn);
            turnProgress.value = controller.TicksPerformedThisTurn;
        }

        // --- difficulty (only if component present)
        if (difficultyTxt)
        {
            difficultyTxt.gameObject.SetActive(true);
            difficultyTxt.text = $"AI Depth {AdaptiveDifficulty.TargetDepth} • {AdaptiveDifficulty.TicksPerTurn} tpt";
        }

        // --- Forces
        if (forcesTxt)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Forces</b>:");
            sb.AppendLine("<size=90%>Press 1 or 2 to select</size>");

            for (int i = 0; i < s.ForceTypes.Length; i++)
            {
                int rem = controller.RemainingForType(s.CurrentPlayer, i);
                var spec = s.ForceTypes[i];
                string name = spec.IsAttractor ? "Attractor" : "Repeller";
                bool selectable = rem > 0 && controller.IsHumanTurn && !controller.IsSimulating;
                bool selected = (i == lastSelectedType);

                // highlight selection
                string sel = selected ? " > " : "    ";
                string grayStart = selectable ? "" : "<color=#777777>";
                string grayEnd = selectable ? "" : "</color>";

                sb.AppendLine($"{sel}{grayStart}[{i + 1}] {name}  <b>x{rem}</b>{grayEnd}");
            }

            forcesTxt.text = sb.ToString();
        }
    }


    // keep selection synced with InputPlacementController
    public void SetSelectedTypeHUD(int index)
    {
        lastSelectedType = Mathf.Clamp(index, 0, (controller?.State?.ForceTypes.Length ?? 1) - 1);
        inputCtrl?.SendMessage("SetSelectedType", lastSelectedType, SendMessageOptions.DontRequireReceiver);
        Refresh();
    }
}