// GameSettings.cs
using SheepGame.Data;
using UnityEngine;

public class GameSettings : MonoBehaviour
{
    [SerializeField] private LevelData[] levels;

    public static LevelData Level { get; private set; }
    public static int LevelIndex { get; private set; }

    public void SetLevel(int index)
    {
        LevelIndex = index;
        Level = levels[index];
        PlayerPrefs.SetInt("LEVEL_ID", index); 
        PlayerPrefs.Save();
    }
}
