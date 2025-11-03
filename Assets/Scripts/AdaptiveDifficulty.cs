// AdaptveDiffivulty.cs
// Helps adjust AI difficulty based on player performance.

using UnityEngine;
using SheepGame.Gameplay;

public sealed class AdaptiveDifficulty : MonoBehaviour
{
    [SerializeField] private GameController controller;
    [SerializeField] private AIAgentController ai;
    [SerializeField] private int minDepth = 1;
    [SerializeField] private int maxDepth = 3;

    // call this after each game ends, or periodically after turns
    public void Rebalance()
    {
        if (!controller || controller.State == null || !ai) return;
        int p0 = controller.State.Score[0];
        int p1 = controller.State.Score[1];
        float wr = (p0 + p1) > 0 ? (float)p0 / (p0 + p1) : 0.5f;

        // raise depth if player is winning too often, lower if struggling
        int targetDepth = aiDepthFromWinRate(wr);
        var f = typeof(AIAgentController).GetField("depth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(ai, Mathf.Clamp(targetDepth, minDepth, maxDepth));

        // also adjust ticks per turn for stronger reactions
        controller.Config.ticksPerTurn = Mathf.Clamp(Mathf.RoundToInt(40 + (1f - wr) * 40), 30, 80);
    }

    private int aiDepthFromWinRate(float wr)
    {
        if (wr > 0.65f) return 3;
        if (wr < 0.35f) return 1;
        return 2;
    }

    public int GetCurrentDepth()
    {
        if (!ai) ai = FindFirstObjectByType<AIAgentController>();
        if (!ai) return 0;

        var f = typeof(AIAgentController)
                .GetField("depth",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

        if (f == null) return 0;

        int v = (int)f.GetValue(ai);
        return Mathf.Clamp(v, minDepth, maxDepth);
    }

}
