// SheepSimulation.cs
// Deterministic physics step: potential fields, mass-scaled response, swept movement with one slide,
// immediate capture on pen overlap, early-settle guard.

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using static SheepGame.Sim.SheepConstants;

namespace SheepGame.Sim
{
    public static class SheepSimulation
    {
        /// <summary>
        /// Run up to X ticks deterministically, removing captured sheep immediately.
        /// Returns number of ticks actually performed (may early-settle).
        /// </summary>
        public static int StepXTicks(GameState state, int ticks, bool earlySettle = true)
        {
            if (state.SheepPos.Length == 0 || ticks <= 0) return 0;

            int settleRun = 0;
            int performed = 0;

            for (int t = 0; t < ticks; t++)
            {
                float maxDispThisTick = StepOneTick(state);

                performed++;
                if (earlySettle)
                {
                    if (maxDispThisTick < SettleThreshold)
                    {
                        settleRun++;
                        if (settleRun >= SettleConsecutiveTicks) break;
                    }
                    else
                    {
                        settleRun = 0;
                    }
                }

                if (state.SheepPos.Length == 0) break; // everything collected
            }

            return performed;
        }

        /// <summary>
        /// One tick: compute displacements (forces + repulsion), move with collision slide and pen capture,
        /// compact sheep arrays when captured. Returns the max displacement magnitude this tick.
        /// </summary>
        public static float StepOneTick(GameState state)
        {
            var pos = state.SheepPos;
            var mass = state.SheepMass;
            var invMass = state.SheepInvMass;

            int S = pos.Length;
            if (S == 0) return 0f;

            float2[] newPos = new float2[S]; // write buffer
            int[] capturedBy = new int[S];   // -1 none, 0 or 1 indicates pen index
            for (int i = 0; i < S; i++) capturedBy[i] = -1;

            float maxDisp = 0f;

            for (int i = 0; i < S; i++)
            {
                float2 p0 = pos[i];

                // Accumulate force field from placed forces
                float2 F = float2.zero;

                for (int f = 0; f < state.Forces.Count; f++)
                {
                    var inst = state.Forces[f];
                    var spec = state.ForceTypes[inst.ForceTypeIndex];

                    float2 c = inst.WorldPos;
                    float2 rvec = c - p0;                    // attraction is + towards center
                    float dist = math.length(rvec);
                    if (dist < Epsilon) dist = Epsilon;

                    if (dist <= spec.Radius)
                    {
                        float2 dir = rvec / dist;
                        float mag = spec.SignedStrength * math.pow(dist, spec.Exponent);
                        F += dir * mag;
                    }
                }

                // Sheep–sheep repulsion (scaled by pusher mass^beta)
                for (int j = 0; j < S; j++)
                {
                    if (j == i) continue;
                    float2 r = p0 - pos[j]; // repel away from neighbor j
                    float d = math.length(r);
                    if (d < Epsilon) d = Epsilon;
                    if (d > SheepRepelCutoff) continue;

                    float2 dir = r / d;
                    float pushPower = math.pow(mass[j], MassPushBeta); // heavy shoves more
                    float mag = SheepRepelStrength * pushPower * math.pow(d, SheepRepelExponent);
                    F += dir * mag;
                }

                // Convert to desired displacement (scaled by invMass)
                float2 dispWanted = Dt * invMass[i] * F;
                float dispLen = math.length(dispWanted);
                if (dispLen > MaxStep) dispWanted = dispWanted * (MaxStep / dispLen);

                // Attempt move with one slide; capture on earliest pen overlap
                float2 p1 = p0 + dispWanted;

                // Choose earliest event among pen overlap and obstacle/boundary collision
                bool hitObstacle = state.Obstacles.FirstObstacleHit(p0, p1, out float tObs, out float2 nObs);
                bool hitBoundary = state.Obstacles.FirstBoundaryHit(p0, p1, out float tBound, out float2 nBound);
                float tColl = float.PositiveInfinity;
                float2 nColl = float2.zero;

                if (hitObstacle && tObs < tColl) { tColl = tObs; nColl = nObs; }
                if (hitBoundary && tBound < tColl) { tColl = tBound; nColl = nBound; }

                // Earliest pen entry along [p0, p1]
                int penIdx = -1;
                float tPen = float.PositiveInfinity;
                for (int k = 0; k < 2; k++)
                {
                    if (state.Pens[k].SegmentEntryT(p0, p1, out float tEnter))
                    {
                        if (tEnter < tPen) { tPen = tEnter; penIdx = k; }
                    }
                }

                if (penIdx != -1 && tPen <= 1f && tPen <= tColl)
                {
                    // Captured before any collision
                    newPos[i] = math.lerp(p0, p1, tPen);
                    capturedBy[i] = penIdx;
                    continue;
                }

                if (tColl <= 1f)
                {
                    // Slide once
                    float2 hitPoint = math.lerp(p0, p1, math.clamp(tColl, 0f, 1f));
                    float2 rem = (1f - math.clamp(tColl, 0f, 1f)) * dispWanted;
                    // Project remainder onto tangent (remove normal component)
                    float2 tang = rem - nColl * math.dot(rem, nColl);
                    float2 p2 = hitPoint + tang;

                    // Check pens along [hitPoint, p2]
                    int pen2 = -1;
                    float tPen2 = float.PositiveInfinity;
                    for (int k = 0; k < 2; k++)
                    {
                        if (state.Pens[k].SegmentEntryT(hitPoint, p2, out float tEnter2))
                        {
                            if (tEnter2 < tPen2) { tPen2 = tEnter2; pen2 = k; }
                        }
                    }

                    if (pen2 != -1 && tPen2 <= 1f)
                    {
                        newPos[i] = math.lerp(hitPoint, p2, tPen2);
                        capturedBy[i] = pen2;
                    }
                    else
                    {
                        newPos[i] = p2;
                    }
                }
                else
                {
                    // No collision; full move
                    newPos[i] = p1;
                }

                float2 actualDisp = newPos[i] - p0;
                float aLen = math.length(actualDisp);
                if (aLen > maxDisp) maxDisp = aLen;
            }

            // Compact captured sheep (swap-with-last)
            CompactCaptured(state, newPos, capturedBy);

            return maxDisp;
        }

        private static void CompactCaptured(GameState state, float2[] newPos, int[] capturedBy)
        {
            int S = newPos.Length;
            var pos = state.SheepPos;
            var mass = state.SheepMass;
            var invMass = state.SheepInvMass;

            int write = 0;
            for (int i = 0; i < S; i++)
            {
                int penIdx = capturedBy[i];
                if (penIdx >= 0)
                {
                    // Score goes to owning pen
                    state.Score[penIdx] += 1;
                    continue; // drop this sheep
                }

                // Keep sheep i → write index
                pos[write] = newPos[i];
                mass[write] = mass[i];
                invMass[write] = invMass[i];
                write++;
            }

            if (write != S)
            {
                // Shrink arrays
                Array.Resize(ref state.SheepPos, write);
                Array.Resize(ref state.SheepMass, write);
                Array.Resize(ref state.SheepInvMass, write);
            }
            else
            {
                // No captures: copy back positions when lengths match
                for (int i = 0; i < S; i++) pos[i] = newPos[i];
            }
        }
    }
}
