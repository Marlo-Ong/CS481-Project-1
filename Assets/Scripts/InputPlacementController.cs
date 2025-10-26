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
        [SerializeField] private Grid grid;
        [SerializeField] private GameObject hoverTile;
        [SerializeField] private LayerMask boardMask;
        [SerializeField] private GameObject forcePrefab;

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

            // Hover cell under mouse
            _hasHover = TryGetMouseCell(out _hoverCell);
            if (_hasHover)
            {
                bool legal = controller.IsPlacementLegal(controller.State, controller.State.CurrentPlayer, _hoverCell, _selectedType);
                var cellPos = grid.CellToWorld(new Vector3Int(_hoverCell.x, 0, _hoverCell.y));
                hoverTile.transform.position = cellPos;
            }

            // Early out if not human's turn or simulating
            if (!controller.IsHumanTurn || controller.IsSimulating) return;

            // Click to place
            if (_hasHover && Input.GetMouseButtonDown(0))
            {
                if (controller.TryPlaceHumanForce(_hoverCell, _selectedType))
                {
                    DecoratePlaceForce();
                }
            }
        }

        private bool TryGetMouseCell(out int2 cell)
        {
            cell = default;

            Camera cam = Camera.main;
            if (!cam)
                return false;

            var mousePosition = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out var hit, maxDistance: 1000f, boardMask))
            {
                Vector3 worldPos = hit.point;
                Vector3Int worldCell = grid.WorldToCell(worldPos);
                cell = new int2(worldCell.x, worldCell.z);
                return true;
            }
            return false;
        }

        private void DecoratePlaceForce()
        {
            var force = Instantiate(forcePrefab);
            force.transform.position = grid.CellToWorld(new Vector3Int(_hoverCell.x, 0, _hoverCell.y));
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
