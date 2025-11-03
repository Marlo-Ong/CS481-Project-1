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
        public const float ForceSoftening = 0.30f;   // r0 in tiles; try 0.25–0.5
        public const float ForceDeadZone = 1.00f;   // optional dead zone near centers
        public const bool NoOvershootToAttractors = false; // clamp crossing the center


        // Sheep–sheep repulsion
        public const float SheepRepelStrength = 1.0f; // k_ss
        public const float SheepRepelCutoff = 2.0f;   // tiles
        public const float SheepRepelExponent = -2.0f; // p in r^p
        public const float MassPushBeta = 1.0f;        // shove power ~ mass^beta (you wanted 1.0)

        // Early-settle
        public const float SettleThreshold = 0.01f; // tiles
        public const int SettleConsecutiveTicks = 5;

        // Field occlusion (start simple: false)
        public const bool FieldsBlockedByObstacles = false;

        // Utility
        public static float2 CellCenter(int2 cell) => new float2(cell.x + 0.5f, cell.y + 0.5f);
    }
}
