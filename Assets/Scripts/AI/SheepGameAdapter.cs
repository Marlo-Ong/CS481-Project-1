// SheepGameAdapter.cs
// Bridges the generic MinimaxAI to your concrete GameState + physics.
// Handles legal move generation, ApplyMove (including X ticks), and terminal/eval.

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using SheepGame.Sim;
using SheepGame.Config;

namespace SheepGame.AI
{
    public sealed class SheepGameAdapter : IGameAdapter<GameState, ForcePlacement>
    {
        private readonly GridConfig _cfg;
        private readonly int _R;
        private readonly int _K;

        /// <param name="cfg">Grid/timing toggles (ticksPerTurn, earlySettle, useJobs, seed).</param>
        /// <param name="candidateRadius">Chebyshev radius around sheep for candidate cells.</param>
        /// <param name="topKPerType">How many top cells to keep per force type.</param>
        public SheepGameAdapter(GridConfig cfg, int candidateRadius = 3, int topKPerType = 6)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _R = math.max(0, candidateRadius);
            _K = math.max(1, topKPerType);
        }

        public int GetCurrentPlayerIndex(in GameState state) => state.CurrentPlayer;

        public bool IsTerminal(in GameState state, int maximizingPlayerIndex, out float terminalValue)
        {
            // End if all sheep collected
            if (state.SheepPos.Length == 0)
            {
                terminalValue = Evaluator.Evaluate(state, maximizingPlayerIndex);
                return true;
            }

            // End if BOTH players have no forces left
            bool anyP0 = AnyForcesLeft(state, 0);
            bool anyP1 = AnyForcesLeft(state, 1);
            if (!anyP0 && !anyP1)
            {
                terminalValue = Evaluator.Evaluate(state, maximizingPlayerIndex);
                return true;
            }

            terminalValue = 0f;
            return false;
        }

        public void GenerateMoves(in GameState state, int playerIndex, List<ForcePlacement> movesBuffer)
        {
            CandidateGenerator.Generate(state, playerIndex, movesBuffer, _R, _K);
        }

        public void ApplyMove(in GameState state, in ForcePlacement move, out GameState nextState)
        {
            // Clone state (depth-2 search keeps this cost small)
            var s = state.DeepCopy();

            // Legal responsibility: Minimax feeds only legal moves, but we guard anyway
            if (s.RemainingByPlayerType[s.CurrentPlayer, move.ForceTypeIndex] <= 0)
            {
                nextState = s; // no-op; shouldn't happen with proper candidate gen
                return;
            }

            // Place force (no stacking on same cell, obstacles allowed? Your rule: not on obstacle.)
            var cell = move.Cell;
            if (!s.Obstacles.InBounds(cell) || s.Obstacles.IsBlocked(cell))
            {
                nextState = s;
                return;
            }
            // Disallow stacking
            for (int i = 0; i < s.Forces.Count; i++)
            {
                if (s.Forces[i].Cell.x == cell.x && s.Forces[i].Cell.y == cell.y)
                { nextState = s; return; }
            }

            s.Forces.Add(new ForceInstance(cell, move.ForceTypeIndex, s.CurrentPlayer));
            s.RemainingByPlayerType[s.CurrentPlayer, move.ForceTypeIndex] -= 1;

            // Advance turn before sim so capture scoring is attributed immediately
            s.CurrentPlayer = 1 - s.CurrentPlayer;

            // Run X ticks deterministically (early settle allowed)
            if (_cfg.useJobs)
                SheepSimulationJobs.StepXTicksJobs(s, _cfg.ticksPerTurn, _cfg.earlySettle);
            else
                SheepSimulation.StepXTicks(s, _cfg.ticksPerTurn, _cfg.earlySettle);

            nextState = s;
        }

        public float Evaluate(in GameState state, int maximizingPlayerIndex)
            => Evaluator.Evaluate(state, maximizingPlayerIndex);

        // ---- helpers ----

        private static bool AnyForcesLeft(in GameState s, int player)
        {
            for (int t = 0; t < s.ForceTypes.Length; t++)
            {
                if (s.RemainingByPlayerType[player, t] > 0) return true;
            }
            return false;
        }
    }
}
