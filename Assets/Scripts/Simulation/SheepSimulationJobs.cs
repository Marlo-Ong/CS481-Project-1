// SheepSimulationJobs.cs
// Parallel version of one-tick stepping. Designed for small S; correctness > micro-optimizations.
// Uses a single combined job per tick that computes new positions + capture flags per sheep.
// Captures are compacted on main thread (immediate at end of the tick).

#define USE_JOBS // comment out to disable jobs path at compile time

#if USE_JOBS
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static SheepGame.Sim.SheepConstants;
#endif

using System;

namespace SheepGame.Sim
{
    public static class SheepSimulationJobs
    {
#if USE_JOBS
        [BurstCompile]
        private struct CombinedStepJob : IJobParallelFor
        {
            // Read-only
            [ReadOnly] public int N;
            [ReadOnly] public NativeArray<byte> ObstGrid; // 1=blocked
            [ReadOnly] public NativeArray<float2> PensMin;
            [ReadOnly] public NativeArray<float2> PensMax;

            [ReadOnly] public NativeArray<float2> InPos;
            [ReadOnly] public NativeArray<float> Mass;
            [ReadOnly] public NativeArray<float> InvMass;

            [ReadOnly] public NativeArray<ForceNative> Forces;

            // Write
            public NativeArray<float2> OutPos;
            public NativeArray<int> CapturedBy;    // -1 none, 0/1
            public NativeArray<float> DispMag;     // for settle check

            public void Execute(int i)
            {
                float2 p0 = InPos[i];

                // Accumulate external forces
                float2 F = float2.zero;

                for (int f = 0; f < Forces.Length; f++)
                {
                    var inst = Forces[f];
                    float2 r = inst.Center - p0; // attract is toward center (signed strength handles direction)
                    float d = math.length(r);
                    if (d < Epsilon) d = Epsilon;
                    if (d <= inst.Radius)
                    {
                        float2 dir = r / d;
                        float mag = inst.SignedStrength * math.pow(d, inst.Exponent);
                        F += dir * mag;
                    }
                }

                // Sheep–sheep repulsion (mass-scaled by source)
                for (int j = 0; j < InPos.Length; j++)
                {
                    if (j == i) continue;
                    float2 r = p0 - InPos[j];
                    float d = math.length(r);
                    if (d < Epsilon) d = Epsilon;
                    if (d > SheepRepelCutoff) continue;

                    float2 dir = r / d;
                    float pushPower = math.pow(Mass[j], MassPushBeta);
                    float mag = SheepRepelStrength * pushPower * math.pow(d, SheepRepelExponent);
                    F += dir * mag;
                }

                float2 dispWanted = Dt * InvMass[i] * F;
                float dispLen = math.length(dispWanted);
                if (dispLen > MaxStep) dispWanted *= (MaxStep / dispLen);

                float2 p1 = p0 + dispWanted;

                // Collision with obstacles/bounds via DDA
                float tColl = float.PositiveInfinity;
                float2 nColl = float2.zero;
                if (FirstObstacleHitDDA(N, ObstGrid, p0, p1, out float tObs, out float2 nObs))
                {
                    tColl = tObs; nColl = nObs;
                }
                if (FirstBoundaryHit(N, p0, p1, out float tBound, out float2 nBound))
                {
                    if (tBound < tColl) { tColl = tBound; nColl = nBound; }
                }

                // Earliest pen entry
                int penIdx = -1;
                float tPen = float.PositiveInfinity;
                for (int k = 0; k < PensMin.Length; k++)
                {
                    if (SegmentEntryT(PensMin[k], PensMax[k], p0, p1, out float tEnter))
                    {
                        if (tEnter < tPen) { tPen = tEnter; penIdx = k; }
                    }
                }

                if (penIdx != -1 && tPen <= 1f && tPen <= tColl)
                {
                    OutPos[i] = math.lerp(p0, p1, tPen);
                    CapturedBy[i] = penIdx;
                    DispMag[i] = math.length(OutPos[i] - p0);
                    return;
                }

                if (tColl <= 1f)
                {
                    float2 hitPoint = math.lerp(p0, p1, math.clamp(tColl, 0f, 1f));
                    float2 rem = (1f - math.clamp(tColl, 0f, 1f)) * dispWanted;
                    float2 tang = rem - nColl * math.dot(rem, nColl);
                    float2 p2 = hitPoint + tang;

                    int pen2 = -1;
                    float tPen2 = float.PositiveInfinity;
                    for (int k = 0; k < PensMin.Length; k++)
                    {
                        if (SegmentEntryT(PensMin[k], PensMax[k], hitPoint, p2, out float tEnter2))
                        {
                            if (tEnter2 < tPen2) { tPen2 = tEnter2; pen2 = k; }
                        }
                    }

                    if (pen2 != -1 && tPen2 <= 1f)
                    {
                        OutPos[i] = math.lerp(hitPoint, p2, tPen2);
                        CapturedBy[i] = pen2;
                    }
                    else
                    {
                        OutPos[i] = p2;
                        CapturedBy[i] = -1;
                    }

                    DispMag[i] = math.length(OutPos[i] - p0);
                }
                else
                {
                    OutPos[i] = p1;
                    CapturedBy[i] = -1;
                    DispMag[i] = math.length(dispWanted);
                }
            }

            // --- Helpers (Burst-friendly) ---

            private static bool FirstBoundaryHit(int N, float2 a, float2 b, out float tHit, out float2 n)
            {
                tHit = float.PositiveInfinity; n = float2.zero;
                float2 d = b - a;
                if (math.all(math.abs(d) < 1e-8f)) return false;

                TryPlane(0f, new float2(1, 0), a, d, ref tHit, ref n); // x=0
                TryPlane(N, new float2(-1, 0), a, d, ref tHit, ref n); // x=N
                TryPlane(0f, new float2(0, 1), a, d, ref tHit, ref n); // y=0
                TryPlane(N, new float2(0, -1), a, d, ref tHit, ref n); // y=N

                return tHit <= 1f;
            }

            private static void TryPlane(float plane, float2 normal, float2 a, float2 d, ref float tBest, ref float2 nBest)
            {
                float t;
                if (normal.x != 0f) { if (math.abs(d.x) < 1e-8f) return; t = (plane - a.x) / d.x; }
                else { if (math.abs(d.y) < 1e-8f) return; t = (plane - a.y) / d.y; }

                if (t >= 0f && t <= 1f && t < tBest) { tBest = t; nBest = normal; }
            }

            private static bool FirstObstacleHitDDA(int N, NativeArray<byte> grid, float2 a, float2 b, out float tHit, out float2 normal)
            {
                tHit = 0f; normal = float2.zero;
                float2 d = b - a;
                if (math.all(math.abs(d) < 1e-8f)) return false;

                // Clamp start into grid; treat outside as wall handled by boundary
                int2 cell = new int2(math.clamp((int)math.floor(a.x), 0, N - 1),
                                     math.clamp((int)math.floor(a.y), 0, N - 1));

                int stepX = d.x > 0 ? 1 : -1;
                int stepY = d.y > 0 ? 1 : -1;

                float nextGridX = (d.x > 0) ? (cell.x + 1) : (cell.x);
                float nextGridY = (d.y > 0) ? (cell.y + 1) : (cell.y);

                float txMax = (math.abs(d.x) < 1e-8f) ? float.PositiveInfinity : (nextGridX - a.x) / d.x;
                float tyMax = (math.abs(d.y) < 1e-8f) ? float.PositiveInfinity : (nextGridY - a.y) / d.y;

                float txDelta = (math.abs(d.x) < 1e-8f) ? float.PositiveInfinity : stepX / d.x;
                float tyDelta = (math.abs(d.y) < 1e-8f) ? float.PositiveInfinity : stepY / d.y;

                for (int iter = 0; iter < 1_000; iter++)
                {
                    if (txMax < tyMax)
                    {
                        int2 nextCell = new int2(cell.x + stepX, cell.y);
                        if (nextCell.x < 0 || nextCell.x >= N) { tHit = math.clamp(txMax, 0f, 1f); normal = new float2(-stepX, 0f); return true; }
                        if (grid[nextCell.y * N + nextCell.x] != 0) { tHit = math.clamp(txMax, 0f, 1f); normal = new float2(-stepX, 0f); return true; }
                        cell = nextCell;
                        txMax += txDelta;
                    }
                    else
                    {
                        int2 nextCell = new int2(cell.x, cell.y + stepY);
                        if (nextCell.y < 0 || nextCell.y >= N) { tHit = math.clamp(tyMax, 0f, 1f); normal = new float2(0f, -stepY); return true; }
                        if (grid[nextCell.y * N + nextCell.x] != 0) { tHit = math.clamp(tyMax, 0f, 1f); normal = new float2(0f, -stepY); return true; }
                        cell = nextCell;
                        tyMax += tyDelta;
                    }
                    if (txMax > 1f && tyMax > 1f) break;
                }

                return false;
            }

            private static bool SegmentEntryT(float2 rectMin, float2 rectMax, float2 a, float2 b, out float tEnter)
            {
                float2 d = b - a;
                float tMin = 0f, tMax = 1f;

                if (!Clip(-d.x, a.x - rectMin.x, ref tMin, ref tMax)) { tEnter = 0f; return false; }
                if (!Clip(d.x, rectMax.x - a.x, ref tMin, ref tMax)) { tEnter = 0f; return false; }
                if (!Clip(-d.y, a.y - rectMin.y, ref tMin, ref tMax)) { tEnter = 0f; return false; }
                if (!Clip(d.y, rectMax.y - a.y, ref tMin, ref tMax)) { tEnter = 0f; return false; }

                tEnter = tMin;
                return tMin <= tMax;
            }

            private static bool Clip(float p, float q, ref float tMin, ref float tMax)
            {
                if (math.abs(p) < 1e-8f)
                {
                    if (q < 0f) return false;
                    return true;
                }
                float r = q / p;
                if (p < 0f)
                {
                    if (r > tMax) return false;
                    if (r > tMin) tMin = r;
                }
                else
                {
                    if (r < tMin) return false;
                    if (r < tMax) tMax = r;
                }
                return true;
            }
        }

        public struct ForceNative
        {
            public float2 Center;
            public float SignedStrength;
            public float Radius;
            public float Exponent;
        }
#endif

        /// <summary>
        /// Job-backed one-tick step. Returns max displacement magnitude. Compacts captures on main thread.
        /// </summary>
        public static float StepOneTickJobs(GameState state, Unity.Collections.Allocator tmpAllocator = Unity.Collections.Allocator.TempJob)
        {
#if USE_JOBS
            int S = state.SheepPos.Length;
            if (S == 0) return 0f;

            // Build native snapshots
            var obst = state.Obstacles.ToNative(tmpAllocator);

            var pensMin = new NativeArray<Unity.Mathematics.float2>(2, tmpAllocator);
            var pensMax = new NativeArray<Unity.Mathematics.float2>(2, tmpAllocator);
            pensMin[0] = state.Pens[0].Min; pensMax[0] = state.Pens[0].Max;
            pensMin[1] = state.Pens[1].Min; pensMax[1] = state.Pens[1].Max;

            var inPos = new NativeArray<Unity.Mathematics.float2>(state.SheepPos, tmpAllocator);
            var outPos = new NativeArray<Unity.Mathematics.float2>(S, tmpAllocator);
            var mass = new NativeArray<float>(state.SheepMass, tmpAllocator);
            var invMass = new NativeArray<float>(state.SheepInvMass, tmpAllocator);

            var capturedBy = new NativeArray<int>(S, tmpAllocator);
            var dispMag = new NativeArray<float>(S, tmpAllocator);

            var forces = new NativeArray<ForceNative>(state.Forces.Count, tmpAllocator);
            for (int i = 0; i < state.Forces.Count; i++)
            {
                var inst = state.Forces[i];
                var spec = state.ForceTypes[inst.ForceTypeIndex];
                forces[i] = new ForceNative
                {
                    Center = inst.WorldPos,
                    SignedStrength = spec.IsAttractor ? +spec.Strength : -spec.Strength,
                    Radius = spec.Radius,
                    Exponent = spec.Exponent
                };
            }

            var job = new CombinedStepJob
            {
                N = state.N,
                ObstGrid = obst,
                PensMin = pensMin,
                PensMax = pensMax,
                InPos = inPos,
                OutPos = outPos,
                Mass = mass,
                InvMass = invMass,
                CapturedBy = capturedBy,
                DispMag = dispMag,
                Forces = forces
            };

            var handle = job.Schedule(S, 1);
            handle.Complete();

            // Compact captures on main thread; compute max displacement
            float maxDisp = 0f;
            int write = 0;
            for (int i = 0; i < S; i++)
            {
                if (capturedBy[i] >= 0)
                {
                    state.Score[capturedBy[i]] += 1;
                    continue;
                }
                state.SheepPos[write] = outPos[i];
                state.SheepMass[write] = state.SheepMass[i];
                state.SheepInvMass[write] = state.SheepInvMass[i];
                if (dispMag[i] > maxDisp) maxDisp = dispMag[i];
                write++;
            }
            if (write != S)
            {
                Array.Resize(ref state.SheepPos, write);
                Array.Resize(ref state.SheepMass, write);
                Array.Resize(ref state.SheepInvMass, write);
            }

            obst.Dispose();
            pensMin.Dispose();
            pensMax.Dispose();
            inPos.Dispose();
            outPos.Dispose();
            mass.Dispose();
            invMass.Dispose();
            capturedBy.Dispose();
            dispMag.Dispose();
            forces.Dispose();

            return maxDisp;
#else
            // Fallback to single-threaded path
            return SheepSimulation.StepOneTick(state);
#endif
        }

        /// <summary>
        /// Run up to X ticks using the Jobs path (or fallback if disabled). Early-settle applied.
        /// </summary>
        public static int StepXTicksJobs(GameState state, int ticks, bool earlySettle = true)
        {
            int settleRun = 0;
            int performed = 0;

            for (int t = 0; t < ticks; t++)
            {
                float maxDisp = StepOneTickJobs(state);
                performed++;

                if (earlySettle)
                {
                    if (maxDisp < SettleThreshold)
                    {
                        settleRun++;
                        if (settleRun >= SettleConsecutiveTicks) break;
                    }
                    else settleRun = 0;
                }

                if (state.SheepPos.Length == 0) break;
            }
            return performed;
        }
    }
}
