// GameController.cs
// Turn manager: place → simulate (animated over FixedUpdate) → next player → end checks.

using System;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using SheepGame.Sim;
using SheepGame.Data;
using SheepGame.Config;
using static SheepGame.Sim.SheepConstants;

namespace SheepGame.Gameplay
{
    public sealed class GameController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private LevelData levelData;
        [SerializeField] private GridConfig config;
        private UIManager ui;

        [Header("Players")]
        [Tooltip("If true, Player 0 is controlled by AI.")]
        [SerializeField] private bool aiPlaysPlayer0 = false;

        [Tooltip("If true, Player 1 is controlled by AI.")]
        [SerializeField] private bool aiPlaysPlayer1 = true;

        [Header("Visuals")]
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color obstacleColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color pen0Color = new Color(0.2f, 0.7f, 1f, 0.2f);
        [SerializeField] private Color pen1Color = new Color(1f, 0.6f, 0.2f, 0.2f);
        [SerializeField] private Color forceColor = new Color(0.8f, 0.9f, 0.1f, 0.8f);
        [SerializeField] private Color sheepColor = new Color(1f, 1f, 1f, 1f);

        public GameState State { get; private set; }

        // Simulation animation state
        public bool IsSimulating { get; private set; }
        public int TicksPerformedThisTurn { get; private set; }

        private int _ticksTargetThisTurn;
        private int _settleRun;

        // For convenience from other scripts
        public int N => State?.N ?? 0;
        public GridConfig Config => config;

        public bool IsAITurn
        {
            get
            {
                if (State == null) return false;
                return (State.CurrentPlayer == 0 && aiPlaysPlayer0) || (State.CurrentPlayer == 1 && aiPlaysPlayer1);
            }
        }

        public bool IsHumanTurn
        {
            get
            {
                if (State == null) return false;
                return (State.CurrentPlayer == 0 && !aiPlaysPlayer0) || (State.CurrentPlayer == 1 && !aiPlaysPlayer1);
            }
        }

        private bool isPaused = false;
        void OnEnable()
        {
            UIManager.OnPauseStateChanged += HandlePause;
        }

        void OnDisable()
        {
            UIManager.OnPauseStateChanged -= HandlePause;
        }

        private void HandlePause(bool paused)
        {
            isPaused = paused;
        }

        void Start()
        {
            ui = FindFirstObjectByType<UIManager>();
            if (levelData == null || config == null)
            {
                Debug.LogError("GameController: Assign LevelData and GridConfig.");
                enabled = false;
                return;
            }

            // Build initial state from authored level + config seed
            uint seed;
            State = levelData.ToGameState(config, out seed);

            // Start of first turn: not simulating yet, waiting for placement.
            IsSimulating = false;
            TicksPerformedThisTurn = 0;
            _settleRun = 0;
            _ticksTargetThisTurn = config.ticksPerTurn;
        }

        void FixedUpdate()
        {
            if (isPaused || State == null) return;

            if (IsSimulating)
            {
                // Animate exactly one physics tick per FixedUpdate
                float maxDisp = config.useJobs
                    ? SheepSimulationJobs.StepOneTickJobs(State)
                    : SheepSimulation.StepOneTick(State);

                TicksPerformedThisTurn++;

                // Early settle bookkeeping
                if (config.earlySettle)
                {
                    if (maxDisp < SettleThreshold) _settleRun++;
                    else _settleRun = 0;
                }

                bool settled = config.earlySettle && _settleRun >= SettleConsecutiveTicks;
                if (TicksPerformedThisTurn >= _ticksTargetThisTurn || settled || State.SheepPos.Length == 0)
                {
                    // End of simulation phase for this turn
                    IsSimulating = false;
                    TicksPerformedThisTurn = 0;
                    _settleRun = 0;

                    // End-of-game?
                    if (IsTerminal())
                    {
                        // Optionally: announce game over here
                        CheckGameOver();

                        return;
                    }

                    // Next turn begins (CurrentPlayer was flipped at placement time)
                    // If next player is AI, AIAgentController will notice and act.
                }
            }
        }

        // ============ Public API for input/AI ============

        public bool TryPlaceHumanForce(int2 cell, int forceTypeIndex)
        {
            if (!IsHumanTurn || IsSimulating) return false;
            if (!IsPlacementLegal(State, State.CurrentPlayer, cell, forceTypeIndex)) return false;

            PlaceForceAndBeginSim(cell, forceTypeIndex);
            return true;
        }

        public bool ApplyAIMove(ForcePlacement move)
        {
            if (!IsAITurn || IsSimulating) return false;
            if (!IsPlacementLegal(State, State.CurrentPlayer, move.Cell, move.ForceTypeIndex)) return false;

            PlaceForceAndBeginSim(move.Cell, move.ForceTypeIndex);
            return true;
        }

        public int RemainingForType(int player, int typeIndex)
        {
            if (State == null || typeIndex < 0 || typeIndex >= State.ForceTypes.Length) return 0;
            return State.RemainingByPlayerType[player, typeIndex];
        }

        public bool IsPlacementLegal(GameState s, int player, int2 cell, int typeIndex)
        {
            if (s == null) return false;
            if (typeIndex < 0 || typeIndex >= s.ForceTypes.Length) return false;
            if (!s.Obstacles.InBounds(cell)) return false;
            if (s.Obstacles.IsBlocked(cell)) return false; // cannot place on obstacles (by your rules)
            if (s.RemainingByPlayerType[player, typeIndex] <= 0) return false;

            // Disallow stacking with existing forces
            for (int i = 0; i < s.Forces.Count; i++)
            {
                var c = s.Forces[i].Cell;
                if (c.x == cell.x && c.y == cell.y) return false;
            }
            // Pens are allowed.
            return true;
        }

        // ============ Internals ============

        private void PlaceForceAndBeginSim(int2 cell, int typeIndex)
        {
            // Place force for current player
            State.Forces.Add(new ForceInstance(cell, typeIndex, State.CurrentPlayer));
            State.RemainingByPlayerType[State.CurrentPlayer, typeIndex] -= 1;

            // Flip turn BEFORE sim (as agreed)
            State.CurrentPlayer = 1 - State.CurrentPlayer;

            // Start animated simulation
            _ticksTargetThisTurn = Mathf.Max(1, config.ticksPerTurn);
            TicksPerformedThisTurn = 0;
            _settleRun = 0;
            IsSimulating = true;
        }

        private bool IsTerminal()
        {
            // 1) All sheep collected
            if (State.SheepPos.Length == 0) return true;

            // 2) Both players have no forces left
            bool p0 = AnyForcesLeft(0);
            bool p1 = AnyForcesLeft(1);
            if (!p0 && !p1) return true;

            return false;
        }

        private bool AnyForcesLeft(int player)
        {
            for (int t = 0; t < State.ForceTypes.Length; t++)
                if (State.RemainingByPlayerType[player, t] > 0) return true;
            return false;
        }

        private void CheckGameOver()
        {
            if (State == null) return;

            int playerScore = State.Score[0];
            int aiScore = State.Score[1];

            bool playerWon = playerScore > aiScore;
            ui?.OnShowResult(playerWon);
        }

        // ============ Gizmos for quick visualization ============

        void OnDrawGizmos()
        {
            if (State == null || !config || !config.debugVisualAids) return;
            int n = State.N;

            // Grid
            Gizmos.color = gridColor;
            for (int x = 0; x <= n; x++)
            {
                Gizmos.DrawLine(new Vector3(x, 0, 0), new Vector3(x, n, 0));
            }
            for (int y = 0; y <= n; y++)
            {
                Gizmos.DrawLine(new Vector3(0, y, 0), new Vector3(n, y, 0));
            }

            // Obstacles
            Gizmos.color = obstacleColor;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (State.Obstacles.IsBlocked(new int2(x, y)))
                    {
                        Gizmos.DrawCube(new Vector3(x + 0.5f, y + 0.5f, 0), new Vector3(1f, 1f, 0.01f));
                    }
                }

            // Pens
            Gizmos.color = pen0Color;
            DrawRect(State.Pens[0].Min, State.Pens[0].Max);
            Gizmos.color = pen1Color;
            DrawRect(State.Pens[1].Min, State.Pens[1].Max);

            // Forces
            Gizmos.color = forceColor;
            foreach (var f in State.Forces)
            {
                var p = SheepConstants.CellCenter(f.Cell);
                Gizmos.DrawSphere(new Vector3(p.x, p.y, 0), 0.15f);
            }

            // Sheep
            Gizmos.color = sheepColor;
            foreach (var p in State.SheepPos)
            {
                Gizmos.DrawSphere(new Vector3(p.x, p.y, 0), 0.1f);
            }
        }

        private static void DrawRect(float2 min, float2 max)
        {
            Vector3 a = new Vector3(min.x, min.y, 0);
            Vector3 b = new Vector3(max.x, min.y, 0);
            Vector3 c = new Vector3(max.x, max.y, 0);
            Vector3 d = new Vector3(min.x, max.y, 0);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
    }
}
