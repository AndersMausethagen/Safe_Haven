using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What can interrupt what the player is doing. Death interrupts every activity, whatever it says.</summary>
[Flags]
public enum PlayerInterrupt
{
    None = 0,
    /// <summary>Killed: ends every activity.</summary>
    Died = 1,
    /// <summary>Rocked back, knocked down or rebuffed by a blow (PlayerCombat.Flinched).</summary>
    Staggered = 2,
    /// <summary>The game paused (GamePause): the Escape menu, the cheat window, a cinematic that holds the world.</summary>
    Paused = 4,
    /// <summary>A window opened over the game (UIStateManager).</summary>
    WindowOpened = 8,
    /// <summary>The weapon in hand changed or was put away (PlayerCombat.WeaponChanged).</summary>
    WeaponSwapped = 16,
}

/// <summary>
/// Something the player does that an interruption can end: building, fishing, riding, working at something, being aboard,
/// crafting by hand, a skill's blow on its way. It registers with <see cref="PlayerInterrupts"/> in OnEnable (and leaves in
/// OnDisable) and is told; it never listens to the player's death, a pause or a flinch itself. A new activity is ended on death
/// without anyone remembering to (Docs/BugPatterns.md, guard 6: each activity listened for the interruptions someone thought of).
/// </summary>
public interface IPlayerActivity
{
    /// <summary>The interruptions that end it besides death, which ends every activity.</summary>
    PlayerInterrupt EndsOn { get; }

    /// <summary>It is interrupted: stop, give back what it holds.</summary>
    void Interrupt(PlayerInterrupt reason);
}

/// <summary>
/// The one place the player's activities hear that they are interrupted (<see cref="IPlayerActivity"/>): the player's death,
/// a blow that rocks them, a pause, a window opening, a weapon swap. On the player; made by Player, or by the first activity to
/// register.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInterrupts : MonoBehaviour
{
    private readonly List<IPlayerActivity> activities = new List<IPlayerActivity>();
    private readonly List<IPlayerActivity> telling = new List<IPlayerActivity>();
    private Damageable health;
    private PlayerCombat combat;
    private UIStateManager windows;

    /// <summary>The player's interrupts, made on them when there are none yet.</summary>
    public static PlayerInterrupts For(GameObject player) =>
        player.TryGetComponent(out PlayerInterrupts found) ? found : player.AddComponent<PlayerInterrupts>();

    /// <summary>The activities registered now, in the order they registered (the order they are told).</summary>
    public IReadOnlyList<IPlayerActivity> Activities => activities;

    public void Add(IPlayerActivity activity)
    {
        if (activity != null && !activities.Contains(activity))
        {
            activities.Add(activity);
        }
    }

    public void Remove(IPlayerActivity activity) => activities.Remove(activity);

    /// <summary>Tells every activity this ends (every one, for a death), in the order they registered.</summary>
    public void Raise(PlayerInterrupt reason)
    {
        telling.Clear();
        telling.AddRange(activities);
        foreach (IPlayerActivity activity in telling)
        {
            if (activity == null || (activity is UnityEngine.Object gone && gone == null))
            {
                activities.Remove(activity);
                continue;
            }

            if (reason != PlayerInterrupt.Died && (activity.EndsOn & reason) == 0)
            {
                continue;
            }

            // One that fails must not keep the others from stopping.
            try
            {
                activity.Interrupt(reason);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void OnEnable()
    {
        health = GetComponent<Damageable>();
        combat = GetComponent<PlayerCombat>();
        if (health != null)
        {
            health.Died += OnDied;
        }

        if (combat != null)
        {
            combat.Flinched += OnFlinched;
            combat.WeaponChanged += OnWeaponChanged;
        }

        GamePause.Changed += OnPauseChanged;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= OnDied;
        }

        if (combat != null)
        {
            combat.Flinched -= OnFlinched;
            combat.WeaponChanged -= OnWeaponChanged;
        }

        GamePause.Changed -= OnPauseChanged;
        Watch(null);
    }

    // The scene's window manager can come after the player: it is looked for each frame (a reference compare).
    private void Update()
    {
        if (windows != UIStateManager.Current)
        {
            Watch(UIStateManager.Current);
        }
    }

    private void Watch(UIStateManager next)
    {
        if (windows != null)
        {
            windows.AnyWindowOpenChanged -= OnWindowOpenChanged;
        }

        windows = next;
        if (windows != null)
        {
            windows.AnyWindowOpenChanged += OnWindowOpenChanged;
        }
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow) => Raise(PlayerInterrupt.Died);

    private void OnFlinched(float seconds, bool lostSwing) => Raise(PlayerInterrupt.Staggered);

    private void OnWeaponChanged(WeaponDefinition weapon) => Raise(PlayerInterrupt.WeaponSwapped);

    private void OnPauseChanged(bool paused)
    {
        if (paused)
        {
            Raise(PlayerInterrupt.Paused);
        }
    }

    private void OnWindowOpenChanged(bool open)
    {
        if (open)
        {
            Raise(PlayerInterrupt.WindowOpened);
        }
    }
}
