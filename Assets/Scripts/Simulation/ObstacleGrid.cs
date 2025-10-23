// ObstacleGrid.cs
// Bit-packed obstacle map + DDA-based segment collision against whole-tile blockers.

using Unity.Collections;
using Unity.Mathematics;

namespace SheepGame.Sim
{
    public sealed class ObstacleGrid
    {
        public readonly int N;           // width == height in tiles
        private readonly bool[] _blocked; // row-major: y*N + x

        public ObstacleGrid(int n, bool[] blockedCopy = null)
        {
            N = n;
            _blocked = blockedCopy != null ? (bool[])blockedCopy.Clone() : new bool[N * N];
        }

        public bool InBounds(int2 c) => c.x >= 0 && c.x < N && c.y >= 0 && c.y < N;

        public bool IsBlocked(int2 c)
        {
            if (!InBounds(c)) return true; // treat out-of-bounds as solid walls
            return _blocked[c.y * N + c.x];
        }

        public void SetBlocked(int2 c, bool value)
        {
            if (!InBounds(c)) return;
            _blocked[c.y * N + c.x] = value;
        }

        public bool[] ToArrayCopy() => (bool[])_blocked.Clone();

        public NativeArray<byte> ToNative(Allocator allocator)
        {
            var na = new NativeArray<byte>(_blocked.Length, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < _blocked.Length; i++) na[i] = _blocked[i] ? (byte)1 : (byte)0;
            return na;
        }

        /// <summary>
        /// DDA: finds earliest t in [0,1] where moving from a to b would cross into a blocked cell,
        /// returning also the collision normal (axis-aligned).
        /// Returns false if no obstacle hit along the segment inside the grid.
        /// </summary>
        public bool FirstObstacleHit(float2 a, float2 b, out float tHit, out float2 normal)
        {
            tHit = 0f;
            normal = float2.zero;

            float2 d = b - a;
            if (math.all(math.abs(d) < 1e-8f))
                return false;

            // Current cell (clamped in-bounds)
            int2 cell = new int2(math.clamp((int)math.floor(a.x), 0, N - 1),
                                 math.clamp((int)math.floor(a.y), 0, N - 1));

            // Steps and initial tMax to next grid line
            int stepX = d.x > 0 ? 1 : -1;
            int stepY = d.y > 0 ? 1 : -1;

            float nextGridX = (d.x > 0) ? (cell.x + 1) : (cell.x);
            float nextGridY = (d.y > 0) ? (cell.y + 1) : (cell.y);

            float txMax = (math.abs(d.x) < 1e-8f) ? float.PositiveInfinity : (nextGridX - a.x) / d.x;
            float tyMax = (math.abs(d.y) < 1e-8f) ? float.PositiveInfinity : (nextGridY - a.y) / d.y;

            float txDelta = (math.abs(d.x) < 1e-8f) ? float.PositiveInfinity : stepX / d.x;
            float tyDelta = (math.abs(d.y) < 1e-8f) ? float.PositiveInfinity : stepY / d.y;

            // Safety limit
            for (int iter = 0; iter < 1_000; iter++)
            {
                // Decide which grid line we cross first
                if (txMax < tyMax)
                {
                    // Crossing vertical line first -> moving into next X cell
                    int2 nextCell = new int2(cell.x + stepX, cell.y);
                    if (!InBounds(nextCell)) // boundary is a wall
                    {
                        tHit = math.clamp(txMax, 0f, 1f);
                        normal = new float2(-stepX, 0f);
                        return true;
                    }
                    if (IsBlocked(nextCell))
                    {
                        tHit = math.clamp(txMax, 0f, 1f);
                        normal = new float2(-stepX, 0f);
                        return true;
                    }
                    cell = nextCell;
                    txMax += txDelta;
                }
                else
                {
                    // Crossing horizontal line first -> into next Y cell
                    int2 nextCell = new int2(cell.x, cell.y + stepY);
                    if (!InBounds(nextCell))
                    {
                        tHit = math.clamp(tyMax, 0f, 1f);
                        normal = new float2(0f, -stepY);
                        return true;
                    }
                    if (IsBlocked(nextCell))
                    {
                        tHit = math.clamp(tyMax, 0f, 1f);
                        normal = new float2(0f, -stepY);
                        return true;
                    }
                    cell = nextCell;
                    tyMax += tyDelta;
                }

                // If both txMax, tyMax exceed 1.0, segment end is reached
                if (txMax > 1f && tyMax > 1f)
                    break;
            }

            return false;
        }

        /// <summary>
        /// First boundary hit against the outer box [0,N]x[0,N]; returns true if the infinite line
        /// would cross the boundary between a and b (t in [0,1]).
        /// </summary>
        public bool FirstBoundaryHit(float2 a, float2 b, out float tHit, out float2 normal)
        {
            tHit = float.PositiveInfinity;
            normal = float2.zero;

            float2 d = b - a;
            if (math.all(math.abs(d) < 1e-8f)) return false;

            // Check each boundary plane
            TryBoundary(a, d, 0f, new float2(1, 0), ref tHit, ref normal); // x = 0 (normal +X)
            TryBoundary(a, d, N, new float2(-1, 0), ref tHit, ref normal); // x = N (normal -X)
            TryBoundary(a, d, 0f, new float2(0, 1), ref tHit, ref normal); // y = 0 (normal +Y)
            TryBoundary(a, d, N, new float2(0, -1), ref tHit, ref normal); // y = N (normal -Y)

            return tHit <= 1f;
        }

        private static void TryBoundary(float2 a, float2 d, float plane, float2 normal, ref float tBest, ref float2 nBest)
        {
            // Handle vertical/horizontal planes
            float t;
            if (normal.x != 0f)
            {
                if (math.abs(d.x) < 1e-8f) return;
                t = (plane - a.x) / d.x;
            }
            else
            {
                if (math.abs(d.y) < 1e-8f) return;
                t = (plane - a.y) / d.y;
            }
            if (t >= 0f && t <= 1f)
            {
                float2 hit = a + t * d;
                // Ensure the other axis is within bounds at hit
                if (normal.x != 0f)
                {
                    if (hit.y < 0f - 1e-6f || hit.y > nBest.x /* will be N later; ignore check */) { }
                }
                if (t < tBest)
                {
                    tBest = t;
                    nBest = normal;
                }
            }
        }
    }
}
