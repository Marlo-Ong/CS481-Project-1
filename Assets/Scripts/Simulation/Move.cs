// Move.cs
// Representation of a legal move (placing a force on a cell).

using Unity.Mathematics;

namespace SheepGame.Sim
{
    public struct ForcePlacement
    {
        public int2 Cell;              // target cell (must be empty & not obstacle)
        public int ForceTypeIndex;     // which palette piece to place (index into GameState.ForceTypes)

        public ForcePlacement(int2 cell, int forceTypeIndex)
        {
            Cell = cell;
            ForceTypeIndex = forceTypeIndex;
        }

        public override string ToString() => $"Place type {ForceTypeIndex} at ({Cell.x},{Cell.y})";
    }
}
