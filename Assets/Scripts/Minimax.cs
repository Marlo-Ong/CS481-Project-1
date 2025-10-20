using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Minimax : MonoBehaviour
{
    [Header("Algorithm Parameters")]
    [SerializeField] private int maxDepth = 2;
    [SerializeField] private int ticksToSimulate = 8;

    #region Minimax Methods

    public Move GetMove(GameState state, Player playerToMove)
    {
        return RunMinimax(
            state,
            depth: this.maxDepth,
            alpha: float.NegativeInfinity,
            beta: float.PositiveInfinity,
            maximizing: playerToMove == GameManager.PlayerOne
        ).move;
    }

    private (Move move, float score) RunMinimax(GameState state, int depth, float alpha, float beta, bool maximizing)
    {
        if (depth <= 0 || IsTerminal(state))
            return (default, Evaluate(state));

        float bestScore = maximizing ? float.NegativeInfinity : float.PositiveInfinity;
        Move bestMove = default;

        // Generate legal moves for the side to move.
        foreach (var move in GenerateMoves(state, maximizing: true))
        {
            // Apply and simulate one turn of physics.
            GameState nextState = Simulate(GameState.ApplyMove(state, move));

            // Recurse. (Switch players)
            (_, float score) = RunMinimax(nextState, maxDepth - 1, alpha, beta, !maximizing);

            // Update best scores.
            if (maximizing)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                alpha = Mathf.Max(alpha, bestScore);
            }
            else
            {
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                beta = Mathf.Min(beta, bestScore);
            }

            // Prune.
            if (beta <= alpha)
                break;
        }

        return (bestMove, bestScore);
    }

    public GameState Simulate(GameState state)
    {
        // Advance a fixed number of ticks for determinism.
        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;

        for (int t = 0; t < this.ticksToSimulate; t++)
        {
            var sheepIds = GetSheepIds(state); // must be stable even as sheep are removed

            // Move sheep under the net potential field.
            foreach (int id in sheepIds)
            {
                if (!SheepExists(state, id))
                    continue;

                Vector2 pos = GetSheepPosition(state, id);
                Vector2 pf = GetVectorSumAtPosition(state, pos); // sum of ALL placed forces

                var desiredHeading = math.atan2(pf.y, pf.x);
                var desiredSpeed = (math.cos(pf) + 1) / 2;

                // Super-simple Euler step (replace with your integrator if needed)
                // OR: implement a kinematics component on the sheep
                pos += pf * dt;
                SetSheepPosition(state, id, pos);

                // Scoring & removal
                if (IsInsideMyPen(state, pos))
                {
                    AwardPoint(state, forMaximizingSide: true);
                    RemoveSheep(state, id);
                }
                else if (IsInsideOppPen(state, pos))
                {
                    AwardPoint(state, forMaximizingSide: false);
                    RemoveSheep(state, id);
                }
            }
        }

        return state;
    }

    private float Evaluate(GameState state)
    {
        var diff = GetScoreForMaxSide(state) - GetScoreForOppSide(state);

        if (diff == 0)
        {
            // Evaluate game state based on sheep distance to pen
        }

        return diff;
    }

    #endregion
    #region Game State Methods

    private IEnumerable<Move> GenerateMoves(GameState state, bool maximizing)
    {
        var remainingForces = GetPaletteForSide(state, maximizing);

        foreach (var cell in GetEmptyCells(state))
        {
            foreach (var f in remainingForces)
                yield return new Move { cell = cell, force = f };
        }
    }

    private bool IsTerminal(GameState state)
    {
        return NoSheepLeft(state) || NoMovesRemain(state);
    }

    private bool NoSheepLeft(GameState state) { /* ... */ return false; }
    private bool NoMovesRemain(GameState state) { /* ... */ return false; }

    // Scores
    private int GetScoreForMaxSide(GameState state) { /* ... */ return 0; }
    private int GetScoreForOppSide(GameState state) { /* ... */ return 0; }
    private void AwardPoint(GameState state, bool forMaximizingSide) { /* ... */ }

    // Move gen
    private IEnumerable<(int x, int y)> GetEmptyCells(GameState state) { /* ... */ yield break; }
    private IReadOnlyList<Force> GetPaletteForSide(GameState state, bool maximizing) { /* ... */ return System.Array.Empty<Force>(); }

    // Sheep sumessors
    private IEnumerable<int> GetSheepIds(GameState state) { /* ... */ yield break; }
    private bool SheepExists(GameState state, int id) { /* ... */ return false; }
    private Vector2 GetSheepPosition(GameState state, int id) { /* ... */ return default; }
    private void SetSheepPosition(GameState state, int id, Vector2 pos) { /* ... */ }
    private void RemoveSheep(GameState state, int id) { /* ... */ }

    private Vector2 GetVectorSumAtPosition(GameState state, Vector2 pos)
    {
        Vector2 sum = Vector2.zero;
        foreach (var f in state.forces)
        {
            Vector2 d = pos - f.position;
            float c = f.magnitude;
            float e = -1;
            float sign = (f.type == Force.Type.Attractive) ? -1f : +1f;

            sum += c * math.pow(d.magnitude, e) * sign * d;
        }
        return sum;
    }

    // Pens
    bool IsInsideMyPen(GameState state, Vector2 pos) { /* ... */ return false; }
    bool IsInsideOppPen(GameState state, Vector2 pos) { /* ... */ return false; }

    #endregion
}
