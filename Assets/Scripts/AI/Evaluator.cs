// Evaluator.cs
// Primary: score difference. Tiebreaker: signed Euclidean distances to pens.
// Encoded as a single float: big weight on score diff so any capture dominates distances.

using Unity.Mathematics;
using SheepGame.Sim;

namespace SheepGame.AI
{
    public static class Evaluator
    {
        // Make captures overwhelmingly more valuable than distance tweaks.
        private const float ScoreWeight = 1_000_000f;

        /// <summary>
        /// Returns an evaluation from the perspective of maximizingPlayer (higher is better).
        /// </summary>
        public static float Evaluate(in GameState s, int maximizingPlayer)
        {
            int opp = 1 - maximizingPlayer;

            int scoreDiff = s.Score[maximizingPlayer] - s.Score[opp];

            // Signed distance sum: lower is better for maximizing player.
            // We negate it so "higher is better" overall.
            float signedDistanceSum = 0f;
            for (int i = 0; i < s.SheepPos.Length; i++)
            {
                float dOwn = s.Pens[maximizingPlayer].DistanceToPoint(s.SheepPos[i]);
                float dOpp = s.Pens[opp].DistanceToPoint(s.SheepPos[i]);
                signedDistanceSum += (dOwn - dOpp);
            }

            return ScoreWeight * scoreDiff - signedDistanceSum;
        }
    }
}
