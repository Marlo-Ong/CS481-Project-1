using System.Collections.Generic;
using UnityEngine;

public interface IObject
{
}

public struct Entity : IObject
{
    public Vector3 position;
    public float mass;
}

public struct GameState
{
    public List<Entity> entities;
    public List<Force> forces;
    public IObject[,] grid;
    public (int width, int length) size;

    public void Initialize(int width, int length)
    {
        this.size = (width, length);
        this.grid = new IObject[width, length];

        // Create empty state.
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < length; j++)
            {
                this.grid[i, j] = null;
            }
        }
    }

    public static GameState Clone(GameState state)
    {
        GameState newState = new()
        {
            size = state.size,
            grid = new IObject[state.size.width, state.size.length]
        };

        // Copy state.
        for (int i = 0; i < state.size.width; i++)
        {
            for (int j = 0; j < state.size.width; j++)
            {
                newState.grid[i, j] = state.grid[i, j];
            }
        }

        return newState;
    }

    public static GameState ApplyMove(GameState state, Move move)
    {
        var clone = Clone(state);
        clone.grid[move.cell.x, move.cell.y] = move.force;
        return clone;
    }
}

public struct Force : IObject
{
    public enum Type { Attractive, Repulsive }
    public enum Size { Small, Medium, Large }

    public Vector2 position;
    public Type type;
    public Size size;
    public float magnitude; // sign can be ignored; 'type' handles direction
    public float radius;
}

public struct Move
{
    public (int x, int y) cell;
    public Force force;
}
