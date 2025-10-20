using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static Player PlayerOne { get; private set; }
    public static Player PlayerTwo { get; private set; }

    void Start()
    {
        PlayerOne = new Player();
        PlayerTwo = new Player();
    }
}