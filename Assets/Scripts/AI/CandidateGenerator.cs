// CandidateGenerator.cs
// Deterministic R/K pruning near sheep, with a one-step local estimate
// that accounts for invMass. Produces a stable move order.

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using SheepGame.Sim;
using static SheepGame.Sim.SheepConstants;

namespace SheepGame.AI
{
    public static class CandidateGenerator
    {
        /// <summary>
        /// Generate legal, pruned candidate placements in a stable order.
        /// - R: Chebyshev radius around each sheep where we consider cells
        /// - K: Keep top-K cells per force type by heuristic score
        /// Appends to 'outMoves' without clearing it.
        /// </summary>
        public static void Generate(GameState state, int playerIndex, List<ForcePlacement> outMoves, int R = 3, int K = 6)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (outMoves == null) throw new ArgumentNullException(nameof(outMoves));

            int n = state.N;
            int types = state.ForceTypes.Length;

            // Quick exit if player has nothing to place
            bool anyRemaining = false;
            for (int t = 0; t < types; t++)
                if (state.RemainingByPlayerType[playerIndex, t] > 0) { anyRemaining = true; break; }
            if (!anyRemaining) return;

            // Build occupancy map for forces (no stacking on same cell)
            var forceOccupied = new bool[n * n];
            for (int i = 0; i < state.Forces.Count; i++)
            {
                var c = state.Forces[i].Cell;
                if (c.x >= 0 && c.x < n && c.y >= 0 && c.y < n)
                    forceOccupied[c.y * n + c.x] = true;
            }

            // Gather candidate cells near sheep in deterministic order.
            var candidateCells = GatherCandidateCellsNearSheep(state, R);

            // Fallback: if nothing (e.g., no sheep), scan whole board row-major
            if (candidateCells.Count == 0)
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int idx = y * n + x;
                        if (state.Obstacles.IsBlocked(new int2(x, y))) continue;
                        // You allowed placements in pens; we permit them here.
                        if (forceOccupied[idx]) continue;
                        candidateCells.Add(new int2(x, y));
                    }
            }

            // For each force type with remaining count, rank candidate cells and keep top-K
            for (int type = 0; type < types; type++)
            {
                if (state.RemainingByPlayerType[playerIndex, type] <= 0) continue;

                var scored = new List<(float score, int2 cell)>(candidateCells.Count);

                for (int i = 0; i < candidateCells.Count; i++)
                {
                    int2 cell = candidateCells[i];

                    // Legal? Not an obstacle and not occupied by an existing force.
                    if (state.Obstacles.IsBlocked(cell)) continue;
                    if (forceOccupied[cell.y * n + cell.x]) continue;

                    float heuristic = EstimatePlacementHeuristic(state, playerIndex, type, cell);
                    scored.Add((heuristic, cell));
                }

                // Sort by score desc, then by y, then x for stable deterministic order
                scored.Sort((a, b) =>
                {
                    int cmp = -a.score.CompareTo(b.score);
                    if (cmp != 0) return cmp;
                    cmp = a.cell.y.CompareTo(b.cell.y);
                    if (cmp != 0) return cmp;
                    return a.cell.x.CompareTo(b.cell.x);
                });

                int take = math.min(K, scored.Count);
                for (int i = 0; i < take; i++)
                {
                    outMoves.Add(new ForcePlacement(scored[i].cell, type));
                }
            }

            // Ultimate fallback: if still empty (e.g., all near cells illegal), pick the first legal with any type
            if (outMoves.Count == 0)
            {
                for (int type = 0; type < types; type++)
                {
                    if (state.RemainingByPlayerType[playerIndex, type] <= 0) continue;

                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            int idx = y * n + x;
                            if (state.Obstacles.IsBlocked(new int2(x, y))) continue;
                            if (forceOccupied[idx]) continue;

                            outMoves.Add(new ForcePlacement(new int2(x, y), type));
                            return;
                        }
                }
            }
        }

        private static List<int2> GatherCandidateCellsNearSheep(GameState state, int R)
        {
            int n = state.N;
            var candidates = new List<int2>(state.SheepPos.Length * (2 * R + 1) * (2 * R + 1));
            if (state.SheepPos.Length == 0) return candidates;

            // Use a visited bitmap to avoid duplicates; fill order is deterministic
            var visited = new bool[n * n];

            for (int si = 0; si < state.SheepPos.Length; si++)
            {
                float2 p = state.SheepPos[si];
                int cx = math.clamp((int)math.floor(p.x), 0, n - 1);
                int cy = math.clamp((int)math.floor(p.y), 0, n - 1);

                for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 0 || x >= n || y < 0 || y >= n) continue;

                        // Chebyshev radius
                        if (math.max(math.abs(dx), math.abs(dy)) > R) continue;

                        int idx = y * n + x;
                        if (visited[idx]) continue;
                        visited[idx] = true;

                        // Do not filter pens here; rules allow placing in pens.
                        candidates.Add(new int2(x, y));
                    }
            }

            return candidates;
        }

        /// <summary>
        /// Heuristic: one-step displacement from this single force (with invMass scaling and MaxStep clamp),
        /// then compute improvement toward own pen and away from opponent pen.
        /// </summary>
        private static float EstimatePlacementHeuristic(GameState s, int player, int forceTypeIndex, int2 cell)
        {
            var spec = s.ForceTypes[forceTypeIndex];
            float2 c = SheepConstants.CellCenter(cell);

            int opp = 1 - player;

            float score = 0f;

            for (int i = 0; i < s.SheepPos.Length; i++)
            {
                float2 p = s.SheepPos[i];

                // Force contribution from this new force to sheep i
                float2 r = c - p; // attraction pulls toward c if signed strength positive
                float d = math.length(r);
                if (d < Epsilon) d = Epsilon;

                float2 disp = float2.zero;
                if (d <= spec.Radius)
                {
                    float2 dir = r / d;
                    float mag = spec.SignedStrength * math.pow(d, spec.Exponent);
                    float2 Fsingle = dir * mag;

                    // One-step estimate with invMass and MaxStep clamp
                    disp = Dt * s.SheepInvMass[i] * Fsingle;
                    float len = math.length(disp);
                    if (len > MaxStep) disp *= (MaxStep / len);
                }

                float2 pAfter = p + disp;

                float ownBefore = s.Pens[player].DistanceToPoint(p);
                float ownAfter = s.Pens[player].DistanceToPoint(pAfter);
                float oppBefore = s.Pens[opp].DistanceToPoint(p);
                float oppAfter = s.Pens[opp].DistanceToPoint(pAfter);

                // Better if own distance decreases and opponent distance increases
                score += (ownBefore - ownAfter) + (oppAfter - oppBefore);
            }

            return score;
        }
    }
}
