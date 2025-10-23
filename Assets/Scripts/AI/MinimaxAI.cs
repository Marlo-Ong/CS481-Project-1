// MinimaxAI.cs
// Lightweight, deterministic minimax (optional alpha-beta) with pluggable adapter.

using System;
using System.Collections.Generic;

namespace SheepGame.AI
{
    public interface IGameAdapter<TState, TMove>
    {
        int GetCurrentPlayerIndex(in TState state);
        bool IsTerminal(in TState state, int maximizingPlayerIndex, out float terminalValue);
        void GenerateMoves(in TState state, int playerIndex, List<TMove> movesBuffer);
        void ApplyMove(in TState state, in TMove move, out TState nextState);
        float Evaluate(in TState state, int maximizingPlayerIndex);
    }

    public readonly struct SearchResult<TMove>
    {
        public readonly bool HasMove;
        public readonly TMove Move;
        public readonly float Value;
        public readonly int RootMovesConsidered;
        public readonly int NodesVisited;

        public SearchResult(TMove move, float value, int rootMoves, int nodes, bool hasMove)
        {
            Move = move;
            Value = value;
            RootMovesConsidered = rootMoves;
            NodesVisited = nodes;
            HasMove = hasMove;
        }
    }

    public sealed class MinimaxAI<TState, TMove>
    {
        private readonly IGameAdapter<TState, TMove> _adapter;
        private readonly List<TMove> _moveBuffer = new List<TMove>(128);
        private readonly List<TMove> _tmpBuffer = new List<TMove>(128);

        public MinimaxAI(IGameAdapter<TState, TMove> adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public SearchResult<TMove> Search(in TState rootState, int depth = 2, bool alphaBeta = false)
        {
            if (depth < 1) depth = 1;

            int maximizingPlayer = _adapter.GetCurrentPlayerIndex(rootState);
            int nodes = 0;

            if (_adapter.IsTerminal(rootState, maximizingPlayer, out float terminal))
            {
                return new SearchResult<TMove>(default, terminal, 0, 1, hasMove: false);
            }

            _moveBuffer.Clear();
            _adapter.GenerateMoves(rootState, maximizingPlayer, _moveBuffer);

            if (_moveBuffer.Count == 0)
            {
                float val = _adapter.Evaluate(rootState, maximizingPlayer);
                return new SearchResult<TMove>(default, val, 0, 1, hasMove: false);
            }

            float bestVal = float.NegativeInfinity;
            TMove bestMove = _moveBuffer[0];
            float alphaVal = float.NegativeInfinity;
            float betaVal = float.PositiveInfinity;

            for (int i = 0; i < _moveBuffer.Count; i++)
            {
                _adapter.ApplyMove(rootState, _moveBuffer[i], out var child);
                float v = alphaBeta
                    ? MinValueAB(in child, depth - 1, maximizingPlayer, ref nodes, alphaVal, betaVal)
                    : MinValue(in child, depth - 1, maximizingPlayer, ref nodes);

                if (v > bestVal) { bestVal = v; bestMove = _moveBuffer[i]; }

                if (alphaBeta)
                {
                    if (v > alphaVal) alphaVal = v;
                    if (alphaVal >= betaVal) break;
                }
            }

            return new SearchResult<TMove>(bestMove, bestVal, _moveBuffer.Count, nodes, hasMove: true);
        }

        private float MaxValue(in TState state, int depth, int maximizingPlayer, ref int nodes)
        {
            nodes++;

            if (_adapter.IsTerminal(state, maximizingPlayer, out float terminal))
                return terminal;

            if (depth == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            _tmpBuffer.Clear();
            int player = _adapter.GetCurrentPlayerIndex(state);
            _adapter.GenerateMoves(state, player, _tmpBuffer);

            if (_tmpBuffer.Count == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            float best = float.NegativeInfinity;

            for (int i = 0; i < _tmpBuffer.Count; i++)
            {
                _adapter.ApplyMove(state, _tmpBuffer[i], out var child);
                float v = MinValue(in child, depth - 1, maximizingPlayer, ref nodes);
                if (v > best) best = v;
            }
            return best;
        }

        private float MinValue(in TState state, int depth, int maximizingPlayer, ref int nodes)
        {
            nodes++;

            if (_adapter.IsTerminal(state, maximizingPlayer, out float terminal))
                return terminal;

            if (depth == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            _tmpBuffer.Clear();
            int player = _adapter.GetCurrentPlayerIndex(state);
            _adapter.GenerateMoves(state, player, _tmpBuffer);

            if (_tmpBuffer.Count == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            float best = float.PositiveInfinity;

            for (int i = 0; i < _tmpBuffer.Count; i++)
            {
                _adapter.ApplyMove(state, _tmpBuffer[i], out var child);
                float v = MaxValue(in child, depth - 1, maximizingPlayer, ref nodes);
                if (v < best) best = v;
            }
            return best;
        }

        private float MaxValueAB(in TState state, int depth, int maximizingPlayer, ref int nodes, float alpha, float beta)
        {
            nodes++;

            if (_adapter.IsTerminal(state, maximizingPlayer, out float terminal))
                return terminal;

            if (depth == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            _tmpBuffer.Clear();
            int player = _adapter.GetCurrentPlayerIndex(state);
            _adapter.GenerateMoves(state, player, _tmpBuffer);

            if (_tmpBuffer.Count == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            float value = float.NegativeInfinity;

            for (int i = 0; i < _tmpBuffer.Count; i++)
            {
                _adapter.ApplyMove(state, _tmpBuffer[i], out var child);
                float v = MinValueAB(in child, depth - 1, maximizingPlayer, ref nodes, alpha, beta);
                if (v > value) value = v;
                if (value > alpha) alpha = value;
                if (alpha >= beta) break;
            }
            return value;
        }

        private float MinValueAB(in TState state, int depth, int maximizingPlayer, ref int nodes, float alpha, float beta)
        {
            nodes++;

            if (_adapter.IsTerminal(state, maximizingPlayer, out float terminal))
                return terminal;

            if (depth == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            _tmpBuffer.Clear();
            int player = _adapter.GetCurrentPlayerIndex(state);
            _adapter.GenerateMoves(state, player, _tmpBuffer);

            if (_tmpBuffer.Count == 0)
                return _adapter.Evaluate(state, maximizingPlayer);

            float value = float.PositiveInfinity;

            for (int i = 0; i < _tmpBuffer.Count; i++)
            {
                _adapter.ApplyMove(state, _tmpBuffer[i], out var child);
                float v = MaxValueAB(in child, depth - 1, maximizingPlayer, ref nodes, alpha, beta);
                if (v < value) value = v;
                if (value < beta) beta = value;
                if (alpha >= beta) break;
            }
            return value;
        }
    }
}
