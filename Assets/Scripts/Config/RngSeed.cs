// RngSeed.cs
// Deterministic seed hashing + helpers for randomized setup (sheep scatter, ranges).

using System.Text;
using Unity.Mathematics;
using UnityEngine;
using SheepGame.Sim; // ObstacleGrid, PenRegion
using Random = Unity.Mathematics.Random;

namespace SheepGame.Util
{
    public static class RngSeed
    {
        /// <summary>
        /// FNV-1a 32-bit hash of an arbitrary string → Unity.Mathematics.Random seed.
        /// </summary>
        public static uint HashToSeed(string s)
        {
            unchecked
            {
                const uint FNV_OFFSET = 2166136261u;
                const uint FNV_PRIME = 16777619u;
                uint hash = FNV_OFFSET;
                var bytes = Encoding.UTF8.GetBytes(s ?? "");
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= FNV_PRIME;
                }
                // Unity.Mathematics.Random requires seed != 0
                if (hash == 0u) hash = 0xCAFEBABEu;
                return hash;
            }
        }

        public static Random Create(uint seed) => new Random(seed);

        /// <summary>
        /// Inclusive min/max random float.
        /// </summary>
        public static float Range(ref Random rng, float minInclusive, float maxInclusive)
        {
            // Random.NextFloat is [0,1), make it inclusive on max by tiny epsilon
            float t = rng.NextFloat();
            return math.lerp(minInclusive, maxInclusive, t);
        }

        /// <summary>
        /// Returns uniformly scattered sheep positions inside free cells,
        /// avoiding obstacles and pens. Picks distinct cells without replacement,
        /// then jitters each position uniformly inside its cell.
        /// </summary>
        public static float2[] ScatterSheep(int count, int N, ObstacleGrid obstacles, PenRegion pen0, PenRegion pen1, ref Random rng)
        {
            if (count <= 0) return new float2[0];

            // 1) Build list of free cells (not blocked, not inside pens)
            var freeCells = new System.Collections.Generic.List<int2>(N * N);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    var cell = new int2(x, y);
                    if (obstacles.IsBlocked(cell)) continue;

                    float2 center = SheepConstants.CellCenter(cell);
                    // If the *cell area* overlaps a pen, we still allow scattering at random within the cell,
                    // but we’ll reject any final position that lands inside a pen (see jitter loop below).
                    freeCells.Add(cell);
                }
            }

            if (freeCells.Count == 0) return new float2[0];

            // 2) Sample cells without replacement (Fisher–Yates-ish)
            int take = math.min(count, freeCells.Count);
            for (int i = 0; i < take; i++)
            {
                int swapIndex = rng.NextInt(i, freeCells.Count);
                // swap
                var tmp = freeCells[i];
                freeCells[i] = freeCells[swapIndex];
                freeCells[swapIndex] = tmp;
            }

            // 3) Jitter inside each chosen cell; reject into pens (resample jitter up to a few tries)
            var result = new float2[take];
            for (int i = 0; i < take; i++)
            {
                int2 cell = freeCells[i];
                float2 p;
                int tries = 0;
                do
                {
                    p = new float2(cell.x + rng.NextFloat(), cell.y + rng.NextFloat());
                    tries++;
                } while ((pen0.ContainsPoint(p) || pen1.ContainsPoint(p)) && tries < 8);

                result[i] = p;
            }

            return result;
        }
    }
}
