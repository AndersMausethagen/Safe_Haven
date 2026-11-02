using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the cheats going across scenes: F1 opens the menu (CheatWindow, made in the scene when first asked for), each new player
/// gets the switches that are on, and those that keep doing something get their tick. A persistent system created by GameSystems.
/// </summary>
public sealed class CheatSystem : MonoBehaviour, IGameSystem
{
    private InputAction toggle;
    private Player lastPlayer;

    public void Initialise()
    {
        if (!CheatSettings.Current.IsAvailable)
        {
            enabled = false;
            return;
        }

        toggle = GameInput.CheatMenu;
        if (toggle != null)
        {
            toggle.performed += OnToggle;
        }
    }

    private void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.performed -= OnToggle;
        }
    }

    public void OnSceneEntered(Scene scene)
    {
    }

    private void Update()
    {
        // The player object is new in every scene, and arrives a little after the scene does.
        if (Player.Current != lastPlayer)
        {
            lastPlayer = Player.Current;
            Cheats.ApplyToPlayer();
        }

        Cheats.Tick();
    }

    private static void OnToggle(InputAction.CallbackContext context)
    {
        CheatWindow.Toggle();
    }
}
