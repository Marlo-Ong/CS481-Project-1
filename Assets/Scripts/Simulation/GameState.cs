// GameState.cs
// Immutable-ish snapshot suitable for AI nodes and simulation stepping.

using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace SheepGame.Sim
{
    [Serializable]
    public sealed class GameState
    {
        // Grid + static world
        public int N;                           // grid size (tiles)
        public ObstacleGrid Obstacles;          // whole-tile blockers
        public PenRegion[] Pens = new PenRegion[2]; // pen[0] belongs to player 0, pen[1] to player 1

        // Forces on the board (persist)
        public List<ForceInstance> Forces = new List<ForceInstance>(64);

        // Force palette (remaining counts) per player x force type
        public ForceSpec[] ForceTypes = Array.Empty<ForceSpec>();
        public int[,] RemainingByPlayerType = new int[2, 0]; // [player, typeIndex]

        // Sheep
        public float2[] SheepPos = Array.Empty<float2>();
        public float[] SheepMass = Array.Empty<float>();
        public float[] SheepInvMass = Array.Empty<float>();

        // Scoring and turn
        public int[] Score = new int[2];
        public int CurrentPlayer; // 0 or 1

        // --- Construction helpers ---
        public GameState DeepCopy()
        {
            var s = new GameState
            {
                N = N,
                Obstacles = new ObstacleGrid(Obstacles.N, Obstacles.ToArrayCopy()),
                Pens = new PenRegion[] { Pens[0], Pens[1] },
                Forces = new List<ForceInstance>(Forces),
                ForceTypes = (ForceSpec[])ForceTypes.Clone(),
                RemainingByPlayerType = (int[,])RemainingByPlayerType.Clone(),
                SheepPos = (float2[])SheepPos.Clone(),
                SheepMass = (float[])SheepMass.Clone(),
                SheepInvMass = (float[])SheepInvMass.Clone(),
                Score = (int[])Score.Clone(),
                CurrentPlayer = CurrentPlayer
            };
            return s;
        }

        public static GameState CreateEmpty(int n, ObstacleGrid obstacles, PenRegion pen0, PenRegion pen1, ForceSpec[] forceTypes, int[,] remainingByPlayerType)
        {
            var s = new GameState
            {
                N = n,
                Obstacles = obstacles,
                Pens = new[] { pen0, pen1 },
                ForceTypes = forceTypes,
                RemainingByPlayerType = remainingByPlayerType
            };
            return s;
        }
    }

    [Serializable]
    public struct ForceSpec
    {
        public float Strength;      // signed magnitude (positive magnitude; signed applied via IsAttractor)
        public float Radius;        // cutoff (tiles)
        public float Exponent;      // falloff exponent e (e.g., -1, -2)
        public bool IsAttractor;    // true => pulls towards center; false => repels

        public ForceSpec(float strength, float radius, float exponent, bool isAttractor)
        {
            Strength = strength;
            Radius = radius;
            Exponent = exponent;
            IsAttractor = isAttractor;
        }

        public float SignedStrength => IsAttractor ? +Strength : -Strength;
    }

    [Serializable]
    public struct ForceInstance
    {
        public int2 Cell;           // grid cell
        public int ForceTypeIndex;  // index into GameState.ForceTypes
        public int OwnerPlayer;     // optional bookkeeping

        public ForceInstance(int2 cell, int forceTypeIndex, int ownerPlayer)
        {
            Cell = cell;
            ForceTypeIndex = forceTypeIndex;
            OwnerPlayer = ownerPlayer;
        }

        public float2 WorldPos => SheepConstants.CellCenter(Cell);
    }
}
