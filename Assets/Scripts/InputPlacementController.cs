// InputPlacementController.cs
// Handles human placement: hover a cell, select force type, click to place.

using UnityEngine;
using Unity.Mathematics;
using SheepGame.Sim;

namespace SheepGame.Gameplay
{
    public sealed class InputPlacementController : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private Color legalColor = new Color(0.2f, 1f, 0.2f, 0.35f);
        [SerializeField] private Color illegalColor = new Color(1f, 0.2f, 0.2f, 0.35f);

        private int _selectedType = 0;
        private bool _hasHover;
        private int2 _hoverCell;

        void Reset()
        {
            controller = FindAnyObjectByType<GameController>();
        }

        void Update()
        {
            if (controller == null || controller.State == null) return;

            // Force type selection: number keys 1..9
            for (int key = 0; key < 9; key++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + key))
                {
                    if (key < controller.State.ForceTypes.Length)
                        _selectedType = key;
                }
            }

            // Hover cell under mouse (XY plane, z=0)
            _hasHover = TryGetMouseCell(out _hoverCell);

            // Early out if not human's turn or simulating
            if (!controller.IsHumanTurn || controller.IsSimulating) return;

            // Click to place
            if (_hasHover && Input.GetMouseButtonDown(0))
            {
                controller.TryPlaceHumanForce(_hoverCell, _selectedType);
            }
        }

        private bool TryGetMouseCell(out int2 cell)
        {
            cell = default;
            Camera cam = Camera.main;
            if (!cam) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            // Intersect with z=0 plane
            if (math.abs(ray.direction.z) < 1e-6f) return false;
            float t = -ray.origin.z / ray.direction.z;
            if (t < 0f) return false;

            Vector3 hit = ray.origin + t * ray.direction;
            int x = Mathf.FloorToInt(hit.x);
            int y = Mathf.FloorToInt(hit.y);

            if (x < 0 || y < 0 || controller.N == 0 || x >= controller.N || y >= controller.N) return false;
            cell = new int2(x, y);
            return true;
        }

        void OnDrawGizmos()
        {
            if (!controller || controller.State == null || !_hasHover) return;

            bool legal = controller.IsPlacementLegal(controller.State, controller.State.CurrentPlayer, _hoverCell, _selectedType);
            Gizmos.color = legal ? legalColor : illegalColor;
            Gizmos.DrawCube(new Vector3(_hoverCell.x + 0.5f, _hoverCell.y + 0.5f, 0), new Vector3(1f, 1f, 0.01f));
        }

        void OnGUI()
        {
            if (!controller || controller.State == null) return;

            var s = controller.State;
            GUILayout.BeginArea(new Rect(10, 10, 280, 200), GUI.skin.box);
            GUILayout.Label($"Current Player: {s.CurrentPlayer} {(controller.IsAITurn ? "(AI)" : "(Human)")}");
            GUILayout.Label($"Scores: P0={s.Score[0]}  P1={s.Score[1]}");
            GUILayout.Space(6);
            GUILayout.Label("Palette (remaining per type):");
            for (int i = 0; i < s.ForceTypes.Length; i++)
            {
                bool selected = (i == _selectedType);
                var spec = s.ForceTypes[i];
                string tag = spec.IsAttractor ? "Attractor" : "Repeller";
                int p = s.CurrentPlayer; // show for current player
                GUILayout.Label($"{(selected ? "➤ " : "  ")}[{i}] {tag}  rem={s.RemainingByPlayerType[p, i]}  (str={spec.Strength}, r={spec.Radius}, e={spec.Exponent})");
            }
            GUILayout.Space(6);
            GUILayout.Label(_hasHover ? $"Hover: ({_hoverCell.x},{_hoverCell.y})" : "Hover: —");
            GUILayout.EndArea();
        }
    }
}
