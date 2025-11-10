// InputPlacementController.cs
// Handles human placement: hover a cell, select force type, click to place.

using UnityEngine;
using Unity.Mathematics;
using SheepGame.Sim;
using SheepGame.Data;
using System.Collections.Generic;

namespace SheepGame.Gameplay
{
    public sealed class InputPlacementController : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private Grid grid;

        [Header("World UI")]
        [SerializeField] private GameObject hoverTile;
        [SerializeField] private LayerMask boardMask;

        [Header("Game Prefabs")]
        [SerializeField] private GameObject p1PenPrefab;
        [SerializeField] private GameObject p2PenPrefab;
        [SerializeField] private GameObject forceAttractPrefab;
        [SerializeField] private GameObject forceRepelPrefab;
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private GameObject sheepPrefab;

        private GameState State => controller.State;

        private int _selectedType = 0;
        private bool _hasHover;
        private int2 _hoverCell;
        private List<Transform> sheepInstances = new();
        private Vector3[] previousPositions;

        void Start()
        {
            if (controller == null)
            {
                Debug.LogError("GameController not assigned in InputPlacementController.");
                return;
            }

            if (controller.State == null)
                controller.StateSet += OnStateSet;
            else
                DrawBoardStateStatics(controller.State);

            controller.ForcePlaced += OnForcePlaced;
        }

        private void OnStateSet(GameState state)
        {
            DrawBoardStateStatics(State);
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
                //bool legal = controller.IsPlacementLegal(controller.State, controller.State.CurrentPlayer, _hoverCell, _selectedType);
                var cellPos = grid.CellToWorld(new Vector3Int(_hoverCell.x, 0, _hoverCell.y));
                hoverTile.transform.position = cellPos;
            }

            // Early out if not human's turn or simulating
            if (!controller.IsHumanTurn || controller.IsSimulating) return;

            // Click to place
            if (_hasHover && Input.GetMouseButtonDown(0))
                controller.TryPlaceHumanForce(_hoverCell, _selectedType);
        }

        void FixedUpdate()
        {
            if (controller.IsSimulating)
                DrawBoardStateDynamics(State);
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

        private void OnForcePlaced(ForceInstance instance)
        {
            bool attracts = State.ForceTypes[instance.ForceTypeIndex].IsAttractor;
            var force = Instantiate(attracts ? forceAttractPrefab : forceRepelPrefab);
            force.transform.position = SimToWorld(instance.Cell);
        }

        //void OnGUI()
        //{
        //    if (!controller || controller.State == null) return;

        //    var s = controller.State;
        //    GUILayout.BeginArea(new Rect(10, 10, 280, 200), GUI.skin.box);
        //    GUILayout.Label($"Current Player: {s.CurrentPlayer} {(controller.IsAITurn ? "(AI)" : "(Human)")}");
        //    GUILayout.Label($"Scores: P0={s.Score[0]}  P1={s.Score[1]}");
        //    GUILayout.Space(6);
        //    GUILayout.Label("Palette (remaining per type):");
        //    for (int i = 0; i < s.ForceTypes.Length; i++)
        //    {
        //        bool selected = (i == _selectedType);
        //        var spec = s.ForceTypes[i];
        //        string tag = spec.IsAttractor ? "Attractor" : "Repeller";
        //        int p = s.CurrentPlayer; // show for current player
        //        GUILayout.Label($"{(selected ? "➤ " : "  ")}[{i}] {tag}  rem={s.RemainingByPlayerType[p, i]}  (str={spec.Strength}, r={spec.Radius}, e={spec.Exponent})");
        //    }
        //    GUILayout.Space(6);
        //    GUILayout.Label(_hasHover ? $"Hover: ({_hoverCell.x},{_hoverCell.y})" : "Hover: —");
        //    GUILayout.EndArea();
        //}

        private void DrawBoardStateStatics(GameState state)
        {
            if (state == null)
                return;

            // Pens
            var pen1 = Instantiate(p1PenPrefab);
            var pen2 = Instantiate(p2PenPrefab);

            var rect = LevelData.PenToRect(state.Pens[0]);
            pen1.transform.position = SimToWorld(new int2(rect.x, rect.y));
            pen1.transform.localScale = new Vector3(rect.size.x, 0, rect.size.y);

            rect = LevelData.PenToRect(state.Pens[1]);
            pen2.transform.position = SimToWorld(new int2(rect.x, rect.y));
            pen2.transform.localScale = new Vector3(rect.size.x, 0, rect.size.y);

            // Obstacles
            int n = state.N;
            int2 curr = int2.zero;

            for (curr.y = 0; curr.y < n; curr.y++)
            {
                for (curr.x = 0; curr.x < n; curr.x++)
                {
                    if (state.Obstacles.IsBlocked(curr))
                    {
                        var obstacle = Instantiate(obstaclePrefab);
                        obstacle.transform.position = SimToWorld(curr);
                    }
                }
            }

            // Sheep
            previousPositions = new Vector3[state.SheepPos.Length];
            for (int i = 0; i < state.SheepPos.Length; i++)
            {
                var sheep = Instantiate(sheepPrefab).transform;
                var pos = state.SheepPos[i];
                sheep.position = new Vector3(pos.x, 0, pos.y);

                previousPositions[i] = sheep.position;
                sheepInstances.Add(sheep);
            }
        }

        private void DrawBoardStateDynamics(GameState state)
        {
            if (State == null)
                return;

            // Update sheep positions.
            int end = math.max(State.SheepPos.Length, sheepInstances.Count);
            for (int i = 0; i < end; i++)
            {
                // Disable sheep if removed in simulation.
                if (i >= State.SheepPos.Length)
                {
                    bool wasActive = sheepInstances[i].gameObject.activeInHierarchy;
                    sheepInstances[i].gameObject.SetActive(false);
                    if (wasActive)
                        SoundManager.Instance.PlayCapture();
                    continue;
                }

                sheepInstances[i].position = SimToWorld(State.SheepPos[i]);
                RotateFromPreviousPosition(sheepInstances[i], ref previousPositions[i]);
            }
        }

        private void RotateFromPreviousPosition(Transform transform, ref Vector3 prev)
        {
            const float turnSpeed = 720f;
            Vector3 curr = transform.position;
            Vector3 disp = curr - prev;
            disp.y = 0f; // XZ only

            if (disp.sqrMagnitude > 1e-6f) // avoid jitter
            {
                // Yaw toward direction of travel on XZ plane
                float targetYaw = Mathf.Atan2(disp.x, disp.z) * Mathf.Rad2Deg;
                float currentYaw = transform.eulerAngles.y;
                float nextYaw = float.IsInfinity(turnSpeed)
                    ? targetYaw
                    : Mathf.MoveTowardsAngle(currentYaw, targetYaw, turnSpeed * Time.deltaTime);

                transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
            }

            prev = curr;
        }

        /// <summary>
        /// Converts a GameState int2 cell to a world position, interpolated by the visual grid.
        /// </summary>
        private Vector3 SimToWorld(int2 cell)
        {
            // Get XZ grid cell from XY simulation cell.
            var gridCell = new Vector3Int(cell.x, 0, cell.y);
            return gridCell;
            return grid.CellToWorld(gridCell);
        }

        /// <summary>
        /// Converts a GameState float2 position to a world position, interpolated by the visual grid.
        /// </summary>
        private Vector3 SimToWorld(float2 pos)
        {
            var local = new Vector3(pos.x, 0, pos.y);
            return local;
        }

        public int GetSelectedType() => _selectedType;
        public bool HasHover() => _hasHover;
        public int2 GetHoverCell() => _hoverCell;

    }

}
