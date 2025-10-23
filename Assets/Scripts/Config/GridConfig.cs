// GridConfig.cs
// High-level knobs you’ll tweak in the Inspector. Kept lightweight on purpose.
// Note: physics constants live in SheepConstants; this config is about gameplay wiring.

using UnityEngine;

namespace SheepGame.Config
{
    [CreateAssetMenu(menuName = "SheepGame/Grid Config", fileName = "GridConfig")]
    public sealed class GridConfig : ScriptableObject
    {
        [Header("Turn Simulation")]
        [Tooltip("Physics ticks simulated after each placement.")]
        public int ticksPerTurn = 50;

        [Tooltip("Stop early if the flock settles under the threshold for K consecutive ticks.")]
        public bool earlySettle = true;

        [Header("Execution")]
        [Tooltip("Use Jobs + Burst path when available.")]
        public bool useJobs = true;

        [Tooltip("Optional seed for deterministic matches. Leave blank to randomize at runtime.")]
        public string gameSeed = "sheep-001";

        [Header("Editor Aids")]
        [Tooltip("Draw vector fields / heatmaps etc. (your DebugHUD can listen to this).")]
        public bool debugVisualAids = false;
    }
}
