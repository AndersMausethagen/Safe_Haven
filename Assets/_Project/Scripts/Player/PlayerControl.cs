using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who holds the player's controls (Docs/BugPatterns.md, fourth round). A system that takes them holds them in its own name and lets go
/// of its own hold only; the controls come back once nobody holds them. Before, ten systems switched the input handler off and on
/// themselves, each guessing whether it was theirs to switch back: the Nøkken's grab gave a downed player, or one whose raid had ended,
/// full controls; a cinematic that began while a scene change held the controls didn't take them, and the change gave them back in the
/// middle of it. ConventionTests fails on a write to PlayerInputHandler.enabled anywhere but here.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerControl : MonoBehaviour
{
    private readonly HashSet<object> holders = new HashSet<object>();
    private PlayerInputHandler input;

    /// <summary>Whether anyone holds this player's controls.</summary>
    public bool IsHeld => holders.Count > 0;

    /// <summary>Who holds them now (for the leak watch and the cheat window).</summary>
    public IReadOnlyCollection<object> Holders => holders;

    /// <summary>Takes the controls in <paramref name="owner"/>'s name (a component, or a key object of its own).</summary>
    public static void Hold(GameObject player, object owner) => Set(player, owner, true);

    /// <summary>Lets go of <paramref name="owner"/>'s hold; the controls come back if nobody else holds them.</summary>
    public static void Release(GameObject player, object owner) => Set(player, owner, false);

    /// <summary>Holds or lets go in <paramref name="owner"/>'s name, as <paramref name="held"/> says.</summary>
    public static void Set(GameObject player, object owner, bool held)
    {
        if (player == null || owner == null)
        {
            return;
        }

        PlayerControl control = player.TryGetComponent(out PlayerControl found) ? found : (held ? player.AddComponent<PlayerControl>() : null);
        if (control != null)
        {
            control.Change(owner, held);
        }
    }

    /// <summary>Whether <paramref name="player"/>'s controls are held by anyone.</summary>
    public static bool HeldOn(GameObject player) => player != null && player.TryGetComponent(out PlayerControl control) && control.IsHeld;

    /// <summary>Whether <paramref name="owner"/> holds <paramref name="player"/>'s controls.</summary>
    public static bool HeldBy(GameObject player, object owner) =>
        player != null && owner != null && player.TryGetComponent(out PlayerControl control) && control.holders.Contains(owner);

    private void Change(object owner, bool held)
    {
        if (held)
        {
            holders.Add(owner);
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
        holders.RemoveWhere(owner => owner is Object unity && unity == null);
        if (input == null && !TryGetComponent(out input))
        {
            return;
        }

        input.enabled = holders.Count == 0;
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
