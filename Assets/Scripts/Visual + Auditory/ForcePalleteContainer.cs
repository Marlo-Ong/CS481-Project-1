using System.Collections.Generic;
using SheepGame.Data;
using SheepGame.Gameplay;
using SheepGame.Sim;
using UnityEngine;
using UnityEngine.UI;

public class ForcePalleteContainer : MonoBehaviour
{
    [SerializeField] private GameController controller;
    [SerializeField] private int playerIndex;
    [SerializeField] private Button buttonTemplate;
    private Dictionary<ForceInstance, Button> buttonsByForce;
    public static int SelectedForceType { get; private set; }

    void Start()
    {
        if (controller.State == null)
            controller.StateSet += OnStateSet;
        else
            OnStateSet(controller.State);
    }

    private void OnStateSet(GameState state)
    {
        buttonsByForce = new();

        // Create buttons for each force in palette.
        for (int i = 0; i < state.RemainingByPlayerType.GetLength(1); i++)
        {
            for (int j = 0; j < state.RemainingByPlayerType[playerIndex, i]; j++)
            {
                var button = Instantiate(buttonTemplate, parent: transform);
                button.onClick.AddListener(() => SelectedForceType = i);
            }
        }
    }
}
