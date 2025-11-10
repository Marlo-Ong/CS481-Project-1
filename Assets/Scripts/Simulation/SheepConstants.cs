// SheepConstants.cs
// Global tunables for the simulation. Adjust freely; AI should read these too.

using Unity.Mathematics;

namespace SheepGame.Sim
{
    public static class SheepConstants
    {
        // Core integration
        public const float Dt = 0.15f;          // positional step scale
        public const float Epsilon = 0.01f;    // small distance floor to avoid singularities
        public const float MaxStep = 0.5f;     // max displacement per tick (tiles)

        // Force easing
        public const float ForceSoftening = 0.60f;   // r0 in tiles; try 0.25–0.5
        public const float ForceDeadZone = 0.02f;   // optional dead zone near centers
        public const bool NoOvershootToAttractors = true; // clamp crossing the center


        // Sheep–sheep repulsion
        public const float SheepRepelStrength = 0.3f; // k_ss
        public const float SheepRepelCutoff = 1.5f;   // tiles
        public const float SheepRepelExponent = -2.0f; // p in r^p
        public const float MassPushBeta = 0.75f;        // shove power ~ mass^beta (you wanted 1.0)

        // Early-settle
        public const float SettleThreshold = 0.01f; // tiles
        public const int SettleConsecutiveTicks = 5;

        // Field occlusion (start simple: false)
        public const bool FieldsBlockedByObstacles = false;

        // Force decay
        public const bool ForceDecayEnabled = true;   // flip off to disable
        public const float ForceDecayFactor = 0.90f;  // exponential per-round factor (e.g., 0.90 => -10%/round)
        public const float ForceMinScale = 0.05f;  // below this, treat as zero / remove

        // Velocity
        public const float VelocityDamping = 0.80f;
        public const float SlideFriction = 0.85f;

        // Utility
        public static float2 CellCenter(int2 cell) => new float2(cell.x + 0.5f, cell.y + 0.5f);
    }
}
