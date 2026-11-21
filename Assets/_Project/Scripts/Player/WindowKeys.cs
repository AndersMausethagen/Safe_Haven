using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// What each window key opens, for whatever answers the window keys while the player's own input handler is off: the Keeper's view,
/// which holds the player but keeps these keys (PlayerControl.Kept.Windows). The same windows as PlayerInputHandler's own keys.
/// <see cref="Answers"/> tells which keys it knows, so a key added to PlayerControl.WindowKeyPaths without a line here is caught by
/// KeeperViewTests rather than doing nothing.
/// </summary>
public static class WindowKeys
{
    /// <summary>Whether <see cref="Press"/> knows the key at <paramref name="path"/>.</summary>
    public static bool Answers(string path) => path switch
    {
        "Player/Items" or "Player/Inventory" or "Player/Skills" or "Player/Character" or "Player/Build" or "Player/Talents"
            or "Player/Journal" or "Player/Tutorial" or "Player/Haven" or "Player/WorldMap" or "Player/WindowsWheel" => true,
        _ => false,
    };

    /// <summary>Opens or closes what <paramref name="action"/> is the key of, for <paramref name="player"/>.</summary>
    public static void Press(InputAction action, GameObject player)
    {
        if (action == null || player == null)
        {
            return;
        }

        if (action == GameInput.Build)
        {
            if (player.TryGetComponent(out BuildPlacer builder))
            {
                builder.ToggleMenu();
            }

            return;
        }

        if (action == GameInput.WindowsWheel)
        {
            if (player.TryGetComponent(out CommandWheel wheel) && !wheel.IsOpen)
            {
                wheel.OpenWindows();
            }

            return;
        }

        if (action == GameInput.Tutorial)
        {
            TutorialWindow.Toggle();
            return;
        }

        if (action == GameInput.WorldMap)
        {
            WorldMapHud.Toggle();
            return;
        }

        UIWindowId? window = action == GameInput.Items || action == GameInput.Inventory ? UIWindowId.Items
            : action == GameInput.Skills ? UIWindowId.Skills
            : action == GameInput.Character ? UIWindowId.Character
            : action == GameInput.Talents ? UIWindowId.Proficiencies
            : action == GameInput.Journal ? UIWindowId.Journal
            : action == GameInput.Haven ? UIWindowId.Haven
            : (UIWindowId?)null;
        if (window.HasValue && UIStateManager.Current != null)
        {
            UIStateManager.Current.RequestToggle(window.Value);
        }
    }
}
