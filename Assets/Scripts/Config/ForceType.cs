// ForceType.cs
// ScriptableObject authoring for a single force type in the palette.
// Converts cleanly to the runtime ForceSpec used by the sim.

using UnityEngine;
using SheepGame.Sim; // for ForceSpec

namespace SheepGame.Data
{
    [CreateAssetMenu(menuName = "SheepGame/Force Type", fileName = "ForceType")]
    public sealed class ForceType : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "Attractor";

        [Header("Behavior")]
        [Tooltip("True = attracts toward the center; False = repels away.")]
        public bool isAttractor = true;

        [Tooltip("Base magnitude (positive). Sign is applied from isAttractor.")]
        public float strength = 1.0f;

        [Tooltip("Influence radius in tiles.")]
        public float radius = 2.0f;

        [Tooltip("Falloff exponent (e.g., -1, -2).")]
        public float exponent = -2.0f;

        [Header("Palette")]
        [Tooltip("How many of this type each player gets at the start.")]
        public int countPerPlayer = 4;

        public ForceSpec ToSpec() => new ForceSpec(strength, radius, exponent, isAttractor);

        public override string ToString() => displayName;
    }
}
