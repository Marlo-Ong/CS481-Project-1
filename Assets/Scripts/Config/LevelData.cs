// LevelData.cs
// Author a level: grid size, obstacles, pens, force palette, and initial sheep setup.
// Provides ToGameState() to construct a ready, deterministic GameState for play/AI.

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using SheepGame.Sim;   // GameState, ObstacleGrid, PenRegion, ForceSpec, ForceInstance
using SheepGame.Config;
using SheepGame.Util;  // RngSeed

namespace SheepGame.Data
{
    [CreateAssetMenu(menuName = "SheepGame/Level Data", fileName = "LevelData")]
    public sealed class LevelData : ScriptableObject
    {
        [Header("Grid")]
        [Min(2)]
        public int N = 10;

        [Header("Obstacles (whole tiles)")]
        [Tooltip("Cells that are blocked. Whole-tile obstacles.")]
        public List<Vector2Int> obstacleCells = new List<Vector2Int>();

        [Header("Pens (rectangles in tile space)")]
        [Tooltip("Player 0's pen as a tile-aligned rectangle.")]
        public RectInt pen0 = new RectInt(0, 4, 2, 2);
        [Tooltip("Player 1's pen as a tile-aligned rectangle.")]
        public RectInt pen1 = new RectInt(8, 4, 2, 2);

        [Header("Forces palette")]
        [Tooltip("All force types included in this level.")]
        public ForceType[] forceTypes;

        [Header("Initial forces placed on board (optional)")]
        public List<InitialForcePlacement> initialForces = new List<InitialForcePlacement>();

        [Header("Sheep initialization")]
        [Tooltip("If provided, these exact positions (in tile/world space) are used.")]
        public List<Vector2> explicitSheepPositions = new List<Vector2>();

        [Tooltip("If no explicit positions, create this many sheep via random scatter.")]
        [Min(0)]
        public int randomSheepCount = 8;

        [Tooltip("Mass range for randomized sheep masses (inclusive).")]
        public Vector2 massRange = new Vector2(0.5f, 2.0f);

        /// <summary>
        /// Build a new GameState instance from this level and a config/seed.
        /// The seed governs starting player, scatter, etc.
        /// </summary>
        public GameState ToGameState(GridConfig cfg, out uint resolvedSeed)
        {
            // 1) Seed
            resolvedSeed = RngSeed.HashToSeed(string.IsNullOrEmpty(cfg?.gameSeed) ? Guid.NewGuid().ToString() : cfg.gameSeed);
            var rng = RngSeed.Create(resolvedSeed);

            // 2) Obstacles
            var obst = new ObstacleGrid(N);
            foreach (var c in obstacleCells)
            {
                if (c.x >= 0 && c.x < N && c.y >= 0 && c.y < N)
                    obst.SetBlocked(new int2(c.x, c.y), true);
            }

            // 3) Pens -> world rectangles (closed intervals)
            PenRegion p0 = RectToPen(pen0);
            PenRegion p1 = RectToPen(pen1);

            // 4) Force specs + palette counts
            var specs = new ForceSpec[forceTypes?.Length ?? 0];
            var remaining = new int[2, specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                specs[i] = forceTypes[i].ToSpec();
                remaining[0, i] = Mathf.Max(0, forceTypes[i].countPerPlayer);
                remaining[1, i] = Mathf.Max(0, forceTypes[i].countPerPlayer);
            }

            // 5) Construct base state
            var state = GameState.CreateEmpty(N, obst, p0, p1, specs, remaining);
            state.Score[0] = state.Score[1] = 0;

            // 6) Starting player randomized by seed (consistent with your spec)
            state.CurrentPlayer = (int)(resolvedSeed & 1u);

            // 7) Place any pre-authored forces
            foreach (var f in initialForces)
            {
                if (!InBounds(f.cell, N)) continue;
                // Don’t place into obstacles or pens
                if (obst.IsBlocked(new int2(f.cell.x, f.cell.y))) continue;

                var wpos = SheepConstants.CellCenter(new int2(f.cell.x, f.cell.y));
                if (p0.ContainsPoint(wpos) || p1.ContainsPoint(wpos)) continue;

                int typeIdx = Mathf.Clamp(f.forceTypeIndex, 0, specs.Length - 1);
                state.Forces.Add(new ForceInstance(new int2(f.cell.x, f.cell.y), typeIdx, Mathf.Clamp(f.ownerPlayer, 0, 1)));
            }

            // 8) Sheep positions
            float2[] sheepPos;
            if (explicitSheepPositions != null && explicitSheepPositions.Count > 0)
            {
                sheepPos = new float2[explicitSheepPositions.Count];
                for (int i = 0; i < explicitSheepPositions.Count; i++)
                    sheepPos[i] = explicitSheepPositions[i];
            }
            else
            {
                sheepPos = RngSeed.ScatterSheep(randomSheepCount, N, obst, p0, p1, ref rng);
            }

            state.SheepPos = sheepPos;

            // 9) Sheep masses
            state.SheepMass = new float[sheepPos.Length];
            state.SheepInvMass = new float[sheepPos.Length];

            for (int i = 0; i < sheepPos.Length; i++)
            {
                float m = Mathf.Clamp(RngSeed.Range(ref rng, massRange.x, massRange.y), 0.01f, 1000f);
                state.SheepMass[i] = m;
                state.SheepInvMass[i] = 1f / m;
            }

            return state;
        }

        private static PenRegion RectToPen(RectInt r)
        {
            // Convert tile rect [x, y, w, h] to a closed rectangle in world/tile space:
            // Min = (x, y), Max = (x + w, y + h)
            var min = new float2(r.xMin, r.yMin);
            var max = new float2(r.xMin + r.width, r.yMin + r.height);
            return new PenRegion(min, max);
        }

        private static bool InBounds(Vector2Int c, int N)
            => c.x >= 0 && c.x < N && c.y >= 0 && c.y < N;

        [Serializable]
        public struct InitialForcePlacement
        {
            public Vector2Int cell;
            public int forceTypeIndex;
            [Range(0, 1)] public int ownerPlayer;
        }
    }
}
