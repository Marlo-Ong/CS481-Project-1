// AIAgentController.cs
// Bridges GameController and the Minimax AI. Acts instantly when it's the AI's turn.

using UnityEngine;
using System.Collections.Generic;
using SheepGame.AI;
using SheepGame.Sim;

namespace SheepGame.Gameplay
{
    public sealed class AIAgentController : MonoBehaviour
    {
        [SerializeField] private GameController controller;

        [Header("AI Settings")]
        [Tooltip("Depth for minimax (you requested 2).")]
        [SerializeField] private int depth = 2;

        [Tooltip("Use alpha-beta pruning for a small speedup.")]
        [SerializeField] private bool useAlphaBeta = false;

        [Tooltip("Candidate radius (R) around sheep for pruning.")]
        [SerializeField] private int candidateRadius = 3;

        [Tooltip("Top-K cells per force type to consider.")]
        [SerializeField] private int topKPerType = 6;

        private MinimaxAI<GameState, ForcePlacement> _ai;
        private SheepGameAdapter _adapter;

        void Reset()
        {
            controller = FindAnyObjectByType<GameController>();
        }

        void Start()
        {
            if (!controller || controller.Config == null)
            {
                Debug.LogError("AIAgentController: missing GameController/GridConfig.");
                enabled = false;
                return;
            }

            _adapter = new SheepGameAdapter(controller.Config, candidateRadius, topKPerType);
            _ai = new MinimaxAI<GameState, ForcePlacement>(_adapter);
        }

        void Update()
        {
            if (!controller || controller.State == null) return;

            // Only act when it's the AI's turn AND not simulating
            if (!controller.IsAITurn || controller.IsSimulating) return;

            // Generate and apply a move (synchronously). For depth=2 this is fine.
            var result = _ai.Search(controller.State, Mathf.Max(1, depth), useAlphaBeta);

            if (result.HasMove)
            {
                controller.ApplyAIMove(result.Move);
            }
            else
            {
                // No legal moves -> the GameController will detect terminal after simulation phases.
                // Here we advance turn manually to avoid stalling.
                // However, your rules say players cannot pass. If this occurs, both palettes are likely empty.
                // Do nothing; GameController will mark terminal at next simulation boundary or UI refresh.
            }
        }
    }
}
