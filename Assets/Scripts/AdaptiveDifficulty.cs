// AdaptveDiffivulty.cs
// Helps adjust AI difficulty based on player performance.

using UnityEngine;
using SheepGame.Gameplay;

public sealed class AdaptiveDifficulty : MonoBehaviour
{
    private static AdaptiveDifficulty Instance;

    [SerializeField] private int minDepth = 1;
    [SerializeField] private int maxDepth = 3;
    public static int TargetDepth { get; private set; }
    public static int TicksPerTurn { get; private set; }
    private int playerGamesWon;
    private int gamesPlayed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Rebalance(playerWon: false, ticksPerTurn: 100);
    }

    // call this after each game ends, or periodically after turns
    public void Rebalance(bool playerWon, int ticksPerTurn)
    {
        playerGamesWon += playerWon ? 1 : 0;
        gamesPlayed++;
        float wr = (float)playerGamesWon / gamesPlayed;

        // raise depth if player is winning too often, lower if struggling
        TargetDepth = aiDepthFromWinRate(wr);

        // also adjust ticks per turn for stronger reactions
        TicksPerTurn = Mathf.Clamp(Mathf.RoundToInt(ticksPerTurn + (1f - wr) * 40), 100, 200);
    }

    private int aiDepthFromWinRate(float wr)
    {
        if (wr > 0.65f) return 3;
        if (wr < 0.35f) return 1;
        return 2;
    }
}
