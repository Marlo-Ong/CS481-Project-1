// PenRegion.cs
// Axis-aligned rectangle in tile/world space with helpers for distance and segment overlap.

using Unity.Mathematics;

namespace SheepGame.Sim
{
    public struct PenRegion
    {
        public float2 Min; // inclusive
        public float2 Max; // inclusive (treat as a closed rect for containment tests)

        public PenRegion(float2 min, float2 max)
        {
            Min = min;
            Max = max;
        }

        public bool ContainsPoint(float2 p)
        {
            return p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y;
        }

        public float DistanceToPoint(float2 p)
        {
            // Euclidean distance to nearest point on / in the rect
            float dx = math.max(math.max(Min.x - p.x, 0f), p.x - Max.x);
            float dy = math.max(math.max(Min.y - p.y, 0f), p.y - Max.y);
            return math.sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// If the segment [a,b] intersects or enters this rect, returns true and the earliest t in [0,1].
        /// Liang–Barsky clipping.
        /// </summary>
        public bool SegmentEntryT(float2 a, float2 b, out float tEnter)
        {
            float2 d = b - a;
            float tMin = 0f, tMax = 1f;

            if (!ClipEdge(-d.x, a.x - Min.x, ref tMin, ref tMax)) { tEnter = 0; return false; }
            if (!ClipEdge(d.x, Max.x - a.x, ref tMin, ref tMax)) { tEnter = 0; return false; }
            if (!ClipEdge(-d.y, a.y - Min.y, ref tMin, ref tMax)) { tEnter = 0; return false; }
            if (!ClipEdge(d.y, Max.y - a.y, ref tMin, ref tMax)) { tEnter = 0; return false; }

            // If start is inside, tMin==0; we consider that an immediate overlap
            tEnter = tMin;
            return tMin <= tMax;
        }

        private static bool ClipEdge(float p, float q, ref float tMin, ref float tMax)
        {
            if (math.abs(p) < 1e-8f)
            {
                // Parallel: if outside, reject
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
}
