// GameController.cs
// Turn manager: place → simulate (animated over FixedUpdate) → next player → end checks.

using System;
using System.Linq;
using System.Collections.Generic;
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

        public GameState State { get; private set; }

        // Simulation animation state
        public bool IsSimulating { get; private set; }
        public int TicksPerformedThisTurn { get; private set; }

        private int _ticksTargetThisTurn;
        private int _settleRun;

        //Checks if player has decided to move a force
        public bool isForceMoving;

        //The path a force will follow
        public List<int2> movementPath;

        // For convenience from other scripts
        public int N => State?.N ?? 0;
        public GridConfig Config => config;

        // Events
        public event Action<ForceInstance> ForcePlaced;
        public event Action<GameState> StateSet;

        private AdaptiveDifficulty _difficulty;

        //Tutorial
        private bool _tutHoldSim = false;       // stop sim stepping
        private bool _tutBlockAI = false;       // block AI turns

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

            _difficulty = FindFirstObjectByType<AdaptiveDifficulty>();

            // Build initial state from authored level + config seed
            State = GameSettings.Level.ToGameState(config, out uint seed);
            StateSet?.Invoke(State);

            // Start of first turn: not simulating yet, waiting for placement.
            IsSimulating = false;
            TicksPerformedThisTurn = 0;
            _settleRun = 0;
            _ticksTargetThisTurn = AdaptiveDifficulty.TicksPerTurn;
        }

        void FixedUpdate()
        {
            if (isPaused || State == null) return;
            // 1) At the very start of your sim step (before StepOneTick / StepXTicks):
            if (_tutHoldSim)
            {
                // Skip advancing physics/simulation this frame
                return;
            }

            // 2) Right before AI chooses/executes a move:
            if (_tutBlockAI && IsAITurn && !IsSimulating)
            {
                // If it’s AI’s turn but we’re blocking AI, just don’t let it act yet.
                return;
            }


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

                }
            }

            // Tutorial-only: if human (player 0) has no forces left, keep giving turns to AI
            if (tutorialAutoAIDump && State != null && !IsSimulating)
            {
                // If it's human's turn but they have no forces, hand the turn to AI
                if (State.CurrentPlayer == 0 && TotalRemainingFor(0) == 0)
                {
                    State.CurrentPlayer = 1;
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
            // 1) Place force for the CURRENT player (do NOT flip before placing)
            var force = new ForceInstance(cell, typeIndex, State.CurrentPlayer);
            State.Forces.Add(force);
            State.RemainingByPlayerType[State.CurrentPlayer, typeIndex] -= 1;

            SoundManager.Instance?.PlayPlaceForce();
            ForcePlaced?.Invoke(force);

            // 2) Flip once for normal play — but NOT during tutorial lock
            if (!tutorialLockToHuman)
                State.CurrentPlayer = 1 - State.CurrentPlayer;

            // 3) Start the sim for this turn
            _ticksTargetThisTurn = Mathf.Max(1, AdaptiveDifficulty.TicksPerTurn);
            TicksPerformedThisTurn = 0;
            _settleRun = 0;
            IsSimulating = true;
        }

        private void MoveForce(int2 curCell, int2 targetCell, int typeIndex)
        {
            List<int2> path = State.aStar.FindPath(curCell, targetCell);
            ForceInstance force;
            foreach (ForceInstance forces in State.Forces)
            {
                if (curCell.x == forces.Cell.x && curCell.y == forces.Cell.y)
                {
                    force = forces;
                }
            }
            foreach (int2 cell in path)
            {

            }
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
            {
                if (State.RemainingByPlayerType[player, t] > 0)
                    return true;
            }
            return false;
        }

        private void CheckGameOver()
        {
            if (State == null) return;

            int playerScore = State.Score[0];
            int aiScore = State.Score[1];

            bool playerWon = playerScore > aiScore;
            ui?.OnShowResult(playerWon);
            _difficulty?.Rebalance(playerWon, config.ticksPerTurn);
        }

        // Tutorial helpers
        public bool tutorialAutoAIDump = false;  // when true, if human has 0 forces, AI auto-runs all its forces

        int TotalRemainingFor(int player)
        {
            if (State == null || State.ForceTypes == null) return 0;
            int sum = 0;
            int types = State.ForceTypes.Length;
            for (int t = 0; t < types; t++)
                sum += State.RemainingByPlayerType[player, t];
            return sum;
        }

        public bool tutorialLockToHuman = false;   // when true, never advance off player 0
        public void TutorialLockToHuman(bool on) => tutorialLockToHuman = on;

        // Called by the tutorial to freeze/unfreeze the simulation stepping
        public void TutorialHoldSim(bool hold) { _tutHoldSim = hold; }
        // Called by the tutorial to allow/prevent AI from acting
        public void TutorialBlockAI(bool block) { _tutBlockAI = block; }

        // Ensure player starts in tutorial scene (prevents AI-first)
        public void ForcePlayerStartsTutorial(int playerIndex = 0)
        {
            if (State != null && State.Score != null) State.CurrentPlayer = Mathf.Clamp(playerIndex, 0, 1);
        }

        // Simple success check: player is team 0
        public bool AnySheepInPlayerPen()
        {
            return State != null && State.Score != null && State.Score.Length > 0 && State.Score[0] > 0;
        }

        public void EnsureHumanStartsIfTutorialDone()
        {
            // Only for the tutorial level after completion
            bool tutorialDone = PlayerPrefs.GetInt("TUTORIAL_DONE", 0) == 1;
            if (!tutorialDone) return;

            // Make sure we start on human and can act
            if (State != null)
            {
                State.CurrentPlayer = 0;   // human
            }

            // Clear any tutorial locks just in case
            tutorialLockToHuman = false;
            _tutHoldSim = false;
            _tutBlockAI = false;
            IsSimulating = false;
        }
    }
}
