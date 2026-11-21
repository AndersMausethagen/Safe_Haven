using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

/// <summary>
/// Who holds the player's controls (Docs/BugPatterns.md, fourth round). A system that takes them holds them in its own name and lets go
/// of its own hold only; the controls come back once nobody holds them. Before, ten systems switched the input handler off and on
/// themselves, each guessing whether it was theirs to switch back: the Nøkken's grab gave a downed player, or one whose raid had ended,
/// full controls; a cinematic that began while a scene change held the controls didn't take them, and the change gave them back in the
/// middle of it. ConventionTests fails on a write to PlayerInputHandler.enabled anywhere but here.
/// <para>
/// A hold may keep a few keys working (<see cref="Kept"/>): being downed keeps moving, to crawl; watching friends after a fall keeps
/// Interact and Dodge. A key works only while every holder keeps it, so a cinematic or a raid's end held at the same time stops the
/// crawl too (before, the downed state switched Move on by hand, and it stayed on under every other hold).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerControl : MonoBehaviour
{
    /// <summary>Keys a hold leaves working.</summary>
    [Flags]
    public enum Kept
    {
        None = 0,
        Move = 1,
        Interact = 2,
        Dodge = 4,
        /// <summary>The keys that open the game's windows (<see cref="WindowKeyPaths"/>): the Keeper's view keeps them, and
        /// routes them itself (the input handler is off under any hold).</summary>
        Windows = 8,
    }

    /// <summary>The window keys <see cref="Kept.Windows"/> keeps: the bag, abilities, character, building, skills, journal,
    /// tutorial, the Haven and the map. Not the wheels or the quickbar, which belong to playing the character.</summary>
    public static readonly string[] WindowKeyPaths =
    {
        "Player/Items", "Player/Inventory", "Player/Skills", "Player/Character", "Player/Build", "Player/Talents",
        "Player/Journal", "Player/Tutorial", "Player/Haven", "Player/WorldMap",
    };

    private readonly Dictionary<object, Kept> holders = new Dictionary<object, Kept>();
    private readonly List<object> gone = new List<object>();
    private readonly List<InputAction> keys = new List<InputAction>();
    private PlayerInputHandler input;
    // Whether the holds switched the handler off: one that switched itself off (no camera when it woke) isn't switched back on.
    private bool switchedOff;

    /// <summary>Whether anyone holds this player's controls.</summary>
    public bool IsHeld => holders.Count > 0;

    /// <summary>Who holds them now (for the leak watch and the cheat window).</summary>
    public IReadOnlyCollection<object> Holders => holders.Keys;

    /// <summary>Takes the controls in <paramref name="owner"/>'s name (a component, or a key object of its own), leaving <paramref name="keep"/> working.</summary>
    public static void Hold(GameObject player, object owner, Kept keep = Kept.None) => Set(player, owner, true, keep);

    /// <summary>Lets go of <paramref name="owner"/>'s hold; the controls come back if nobody else holds them.</summary>
    public static void Release(GameObject player, object owner) => Set(player, owner, false);

    /// <summary>Holds (leaving <paramref name="keep"/> working) or lets go in <paramref name="owner"/>'s name, as <paramref name="held"/> says.</summary>
    public static void Set(GameObject player, object owner, bool held, Kept keep = Kept.None)
    {
        if (player == null || owner == null)
        {
            return;
        }

        PlayerControl control = player.TryGetComponent(out PlayerControl found) ? found : (held ? player.AddComponent<PlayerControl>() : null);
        if (control != null)
        {
            control.Change(owner, held, keep);
        }
    }

    /// <summary>Whether <paramref name="player"/>'s controls are held by anyone.</summary>
    public static bool HeldOn(GameObject player) => player != null && player.TryGetComponent(out PlayerControl control) && control.IsHeld;

    /// <summary>Whether <paramref name="owner"/> holds <paramref name="player"/>'s controls.</summary>
    public static bool HeldBy(GameObject player, object owner) =>
        player != null && owner != null && player.TryGetComponent(out PlayerControl control) && control.holders.ContainsKey(owner);

    private void Change(object owner, bool held, Kept keep)
    {
        if (held)
        {
            holders[owner] = keep;
        }
        else
        {
            holders.Remove(owner);
        }

        Apply();
    }

    // A holder that is gone (a destroyed cinematic, a scene's director) holds nothing: its hold is let go with it.
    private void Apply()
    {
        gone.Clear();
        foreach (object owner in holders.Keys)
        {
            if (owner is Object unity && unity == null)
            {
                gone.Add(owner);
            }
        }

        foreach (object owner in gone)
        {
            holders.Remove(owner);
        }

        if (input == null && !TryGetComponent(out input))
        {
            return;
        }

        if (holders.Count == 0)
        {
            // The handler's OnEnable gives the whole map back (GameInput.SetPlayerKeys), the kept keys with it.
            if (switchedOff)
            {
                switchedOff = false;
                input.enabled = true;
            }
            else if (!input.enabled)
            {
                GameInput.SetPlayerKeys(false);
            }

            return;
        }

        if (input.enabled)
        {
            switchedOff = true;
            input.enabled = false;
        }

        // What every holder keeps works, nothing while paused; the map's one switch is GameInput's.
        Kept keep = GamePause.IsPaused ? Kept.None : Kept.Move | Kept.Interact | Kept.Dodge | Kept.Windows;
        foreach (Kept kept in holders.Values)
        {
            keep &= kept;
        }

        keys.Clear();
        if ((keep & Kept.Move) != 0) keys.Add(GameInput.Move);
        if ((keep & Kept.Interact) != 0) keys.Add(GameInput.Interact);
        if ((keep & Kept.Dodge) != 0) keys.Add(GameInput.Dodge);
        if ((keep & Kept.Windows) != 0)
        {
            foreach (string path in WindowKeyPaths)
            {
                keys.Add(InputSystem.actions != null ? InputSystem.actions.FindAction(path) : null);
            }
        }
        GameInput.SetPlayerKeys(false, keys);
    }

    private void OnEnable() => GamePause.Changed += OnPauseChanged;

    private void OnDisable() => GamePause.Changed -= OnPauseChanged;

    private void OnPauseChanged(bool paused)
    {
        if (holders.Count > 0)
        {
            Apply();
        }
    }

    private void Update()
    {
        // Only to let go of holders destroyed without letting go (cheap: nothing to do while nobody holds them).
        if (holders.Count > 0 && Time.frameCount % 30 == 0)
        {
            Apply();
        }
    }
}
