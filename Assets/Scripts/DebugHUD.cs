// DebugHUD.cs
// Tiny on-screen status + optional gizmo toggles. Also draws a simple legend.

using UnityEngine;
using Unity.Mathematics;
using SheepGame.Sim;

namespace SheepGame.Gameplay
{
    public sealed class DebugHUD : MonoBehaviour
    {
        [SerializeField] private GameController controller;

        void Reset()
        {
            controller = FindAnyObjectByType<GameController>();
        }

        void OnGUI()
        {
            if (!controller || controller.State == null) return;
            var cfg = controller.Config;
            var s = controller.State;

            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 220), GUI.skin.box);
            GUILayout.Label("<b>SheepGame Debug</b>");
            GUILayout.Space(4);
            GUILayout.Label($"Tick (this turn): {controller.TicksPerformedThisTurn}/{cfg.ticksPerTurn}  {(controller.IsSimulating ? "(simulating)" : "")}");
            GUILayout.Label($"Current Player: {s.CurrentPlayer} {(controller.IsAITurn ? "(AI)" : "(Human)")}");
            GUILayout.Label($"Sheep count: {s.SheepPos.Length}");
            GUILayout.Label($"Score: P0={s.Score[0]}  P1={s.Score[1]}");
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            bool jobs = GUILayout.Toggle(cfg.useJobs, "Use Jobs");
            bool aids = GUILayout.Toggle(cfg.debugVisualAids, "Visual Aids");
            GUILayout.EndHorizontal();
            cfg.useJobs = jobs;
            cfg.debugVisualAids = aids;

            GUILayout.Label("Early Settle:");
            GUILayout.BeginHorizontal();
            cfg.earlySettle = GUILayout.Toggle(cfg.earlySettle, cfg.earlySettle ? "On" : "Off", GUILayout.Width(80));
            GUILayout.Label($"Threshold={SheepConstants.SettleThreshold:0.000}");
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Legend:");
            GUILayout.Label("• White dots = sheep");
            GUILayout.Label("• Yellow dots = forces");
            GUILayout.Label("• Teal/Orange = pens");
            GUILayout.EndArea();
        }
    }
}
