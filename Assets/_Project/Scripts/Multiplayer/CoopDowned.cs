using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Downed and helped up, on a raid together. While a friend who is still standing is on the same run, a blow that would
/// kill this player knocks them down instead: health is held at 1 (<see cref="Damageable.HoldFloor"/>), and reaching it
/// puts them on the ground, unable to act and left alone by creatures (<see cref="IsDown"/>). A friend holds E beside them
/// (<see cref="FriendRevive"/>) to help them up with some health (<c>SafeHaven.HelpUp</c>, passed on by the host). They
/// die after <see cref="MultiplayerSettings.BleedOutSeconds"/>, or once no friend is standing on the run, and the raid then
/// ends for them as any death does. Alone, a killing blow kills as it always has.
/// The decision to go down waits for the next look after the blow, so a Deathless relic gets its say first.
/// Plain C#: <see cref="CoopSession"/> ticks it and owns the connection.
/// </summary>
public sealed class CoopDowned : IDisposable
{
    private const string HelpUpMessage = "SafeHaven.HelpUp";
    private const float DownAt = 1.001f;
    private const float AloneSeconds = 3f;

    private NetworkManager network;
    private MultiplayerSettings settings;
    private Damageable health;
    private Player player;
    private bool holding;
    private bool struckDown;
    private float bleedOutAt;
    private float aloneSince = -1f;

    /// <summary>True while this player is down, waiting for a friend.</summary>
    public static bool IsDown { get; private set; }

    /// <summary>Seconds left before a downed player dies (0 when not down).</summary>
    public float BleedOutLeft => IsDown ? Mathf.Max(0f, bleedOutAt - Time.time) : 0f;

    public void Start(NetworkManager manager, MultiplayerSettings multiplayer)
    {
        network = manager;
        settings = multiplayer;
        Reset();
        network.CustomMessagingManager.RegisterNamedMessageHandler(HelpUpMessage, OnHelpUp);
    }

    /// <summary>The session ended: stand up quietly and let go of the health floor.</summary>
    public void Reset()
    {
        if (IsDown)
        {
            StandUp(null, quiet: true);
        }

        Release();
        aloneSince = -1f;
        struckDown = false;
    }

    public void Dispose()
    {
        Reset();
        Bind(null);
    }

    public void Tick()
    {
        Bind(Player.Current);
        if (health == null || network == null)
        {
            return;
        }

        bool friendStanding = FriendStandingHere();
        if (!IsDown)
        {
            if (GameSession.ActiveRaid != null && friendStanding && health.IsAlive)
            {
                Hold();
            }
            else
            {
                Release();
            }

            if (struckDown && holding && health.IsAlive && health.CurrentHealth <= DownAt)
            {
                GoDown();
            }

            struckDown = false;
            return;
        }

        aloneSince = friendStanding ? -1f : aloneSince < 0f ? Time.time : aloneSince;
        if (Time.time >= bleedOutAt || (aloneSince >= 0f && Time.time - aloneSince >= AloneSeconds) || GameSession.ActiveRaid == null)
        {
            Die();
        }
    }

    /// <summary>Helps a downed friend up (from <see cref="FriendRevive"/>): sent to them, through the host when this is a guest.</summary>
    public void HelpUp(ulong friendClientId)
    {
        if (network == null || !network.IsConnectedClient && !network.IsServer)
        {
            return;
        }

        using var writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(friendClientId);
        writer.WriteValueSafe(GameSession.Appearance != null ? GameSession.Appearance.DisplayName : CharacterAppearance.DefaultName);
        ulong to = network.IsServer ? friendClientId : NetworkManager.ServerClientId;
        network.CustomMessagingManager.SendNamedMessage(HelpUpMessage, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void OnHelpUp(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong target);
        reader.ReadValueSafe(out string helper);
        // The host passes a guest's help on to the guest it is for.
        if (network.IsServer && target != NetworkManager.ServerClientId)
        {
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(target);
            writer.WriteValueSafe(helper);
            network.CustomMessagingManager.SendNamedMessage(HelpUpMessage, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
            return;
        }

        if (IsDown)
        {
            StandUp(helper, quiet: false);
        }
    }

    private void Bind(Player now)
    {
        if (now == player)
        {
            return;
        }

        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.ReleaseFloor(this);
        }

        holding = false;
        player = now;
        health = now != null ? now.GetComponent<Damageable>() : null;
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDamaged(DamageInfo damage, DamageResult result) => struckDown |= holding && result.HealthLost > 0f;

    private void Hold()
    {
        if (!holding)
        {
            health.HoldFloor(this, 1f);
            holding = true;
        }
    }

    private void Release()
    {
        if (holding && health != null)
        {
            health.ReleaseFloor(this);
        }

        holding = false;
    }

    private void GoDown()
    {
        IsDown = true;
        bleedOutAt = Time.time + settings.BleedOutSeconds;
        aloneSince = -1f;
        health.AddInvulnerability(this);
        SetActing(false);
        GameMessages.Post($"You're down! A friend can help you up within {settings.BleedOutSeconds:0} seconds.", MessageKind.Warning);
    }

    private void StandUp(string helper, bool quiet)
    {
        IsDown = false;
        if (health != null)
        {
            health.RemoveInvulnerability(this);
            health.ShowShare(settings != null ? settings.HelpedUpHealth : 0.35f);
        }

        SetActing(true);
        if (!quiet)
        {
            GameMessages.Post($"{helper} helped you up.", MessageKind.Gain);
        }
    }

    private void Die()
    {
        IsDown = false;
        health.RemoveInvulnerability(this);
        Release();
        health.Finish(null);
    }

    // Down: no moving or acting, lying as the dead lie (which friends see through the mirrored animation).
    private void SetActing(bool acting)
    {
        if (player == null)
        {
            return;
        }

        if (player.TryGetComponent(out PlayerInputHandler input))
        {
            input.enabled = acting;
        }

        if (!acting && player.TryGetComponent(out PlayerMovement movement))
        {
            movement.SetMovementInput(Vector3.zero);
        }

        PlayerAnimationController animation = player.GetComponentInChildren<PlayerAnimationController>();
        if (animation != null)
        {
            animation.SetDead(!acting);
        }
    }

    // A friend on this run who is up and about.
    private bool FriendStandingHere()
    {
        string place = CoopCreatures.PlaceHere();
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && !friend.IsOwner && friend.IsVisible && !friend.Look.Downed && friend.Look.Place.ToString() == place)
            {
                return true;
            }
        }

        return false;
    }
}
