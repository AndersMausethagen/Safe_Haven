using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Downed and helped up, on a raid together. Down, a player crawls (crouched, slow: <see cref="FrameUpdate"/>) and sees the
/// seconds left and any friend helping them (<see cref="DownedHud"/>, <c>SafeHaven.Helping</c>). While a friend who is still standing is on the same run, a blow that would
/// kill this player knocks them down instead: health is held at 1 (<see cref="Damageable.HoldFloor"/>), and reaching it
/// puts them on the ground, unable to act and left alone by creatures (<see cref="IsDown"/>). A friend holds E beside them
/// (<see cref="FriendRevive"/>) to help them up with some health (<c>SafeHaven.HelpUp</c>, passed on by the host). They
/// die after <see cref="MultiplayerSettings.BleedOutSeconds"/>, or once no friend is standing on the run, and the raid then
/// ends for them as any death does. Alone, a killing blow kills as it always has.
/// The decision to go down waits for the next look after the blow, so a Deathless relic gets its say first.
/// Plain C#: <see cref="CoopSession"/> ticks it and owns the connection.
/// </summary>
public sealed class CoopDowned : ICoopPart, IDisposable
{
    private const string HelpUpMessage = "SafeHaven.HelpUp";
    private const string HelpingMessage = "SafeHaven.Helping";
    /// <summary>Health at (or just over) the held floor of 1: a frame's regeneration can't hide a killing blow.</summary>
    private const float DownAt = 1.05f;
    private const float AloneSeconds = 3f;

    private const float LookSeconds = 0.1f;
    private CoopLink link;
    private float nextLook;

    private NetworkManager network => link?.Network;
    private MultiplayerSettings settings;
    private Damageable health;
    private Player player;
    private PlayerMovement movement;
    private PlayerStealth stealth;
    private PlayerInteractor interactor;
    private PlayerInputHandler input;
    private bool holding;
    private bool struckDown;
    private float bleedOutAt;
    private float aloneSince = -1f;
    private int falls;
    private int fallsInRun;
    private string helper;
    // Who is helping, by client id: two helpers sharing a name, one stopping cleared the other's progress.
    private ulong helperId;
    private float helpStartedAt = -1f;
    private float helpSeconds;

    /// <summary>True while this player is down, waiting for a friend.</summary>
    public static bool IsDown { get; private set; }

    /// <summary>Who is helping this downed player up now, or null.</summary>
    public string Helper => helpStartedAt >= 0f ? helper : null;

    /// <summary>How far a friend's help has got (0 to 1), or -1 while nobody is helping.</summary>
    public float HelpShare => helpStartedAt >= 0f && helpSeconds > 0f ? Mathf.Clamp01((Time.time - helpStartedAt) / helpSeconds) : -1f;

    /// <summary>Seconds left before a downed player dies (0 when not down).</summary>
    public float BleedOutLeft => IsDown ? Mathf.Max(0f, bleedOutAt - Time.time) : 0f;

    public CoopDowned(MultiplayerSettings multiplayer)
    {
        settings = multiplayer;
    }

    public void Begin(CoopLink session)
    {
        link = session;
        Reset();
        link.On(HelpUpMessage, OnHelpUp);
        link.On(HelpingMessage, OnHelping);
    }

    /// <summary>
    /// The session ended. Down on a raid, nobody is left to help: the blow that knocked them down kills them (leaving is no
    /// way back up). Otherwise stand up quietly. Either way the health floor goes.
    /// </summary>
    public void Reset()
    {
        if (IsDown)
        {
            if (GameSession.ActiveRaid != null && health != null)
            {
                Die();
            }
            else
            {
                StandUp(null, quiet: true);
            }
        }

        Release();
        aloneSince = -1f;
        struckDown = false;
    }

    void ICoopPart.Tick()
    {
        FrameUpdate();
        if (Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + LookSeconds;
            Tick();
        }
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        Reset();
        link = null;
    }

    public void Dispose()
    {
        Reset();
        Bind(null);
    }

    /// <summary>Ten times a second: holds or lets go of the floor, and ends a down that has run out.</summary>
    private void Tick()
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

            return;
        }

        aloneSince = friendStanding ? -1f : aloneSince < 0f ? Time.time : aloneSince;
        if (Time.time >= bleedOutAt || (aloneSince >= 0f && Time.time - aloneSince >= AloneSeconds) || GameSession.ActiveRaid == null)
        {
            Die();
        }
    }

    /// <summary>
    /// Every frame: a downed player can still drag themselves along, crouched and slow (they can't fight, use or carry on as
    /// usual: the rest of their input is off). They are drawn as crouching to friends, with "Down!" over their head.
    /// </summary>
    private void FrameUpdate()
    {
        if (struckDown && !IsDown && health != null)
        {
            struckDown = false;
            if (holding && health.IsAlive && health.CurrentHealth <= DownAt)
            {
                GoDown();
            }
        }

        if (!IsDown || movement == null)
        {
            return;
        }

        // Behind the Escape menu, nobody crawls.
        if (GamePause.IsPaused)
        {
            movement.SetMovementInput(Vector3.zero);
            return;
        }

        Vector2 stick = GameInput.Move != null ? GameInput.Move.ReadValue<Vector2>() : Vector2.zero;
        Camera view = Camera.main;
        Vector3 direction = Vector3.zero;
        if (view != null && stick.sqrMagnitude > 0.01f)
        {
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
            direction = forward * stick.y + right * stick.x;
        }

        movement.SetMovementInput(Vector3.ClampMagnitude(direction, 1f) * settings.CrawlSpeed);
    }

    /// <summary>A friend began (or stopped) holding E beside a downed friend: they see who is helping, and how far along.</summary>
    public void SendHelping(ulong friendClientId, bool helping, float seconds)
    {
        if (network == null || (!network.IsConnectedClient && !network.IsServer))
        {
            return;
        }

        using var writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(friendClientId);
        writer.WriteValueSafe(GameSession.Appearance != null ? GameSession.Appearance.DisplayName : CharacterAppearance.DefaultName);
        writer.WriteValueSafe(network.LocalClientId);
        writer.WriteValueSafe(helping);
        writer.WriteValueSafe(seconds);
        ulong to = network.IsServer ? friendClientId : NetworkManager.ServerClientId;
        network.CustomMessagingManager.SendNamedMessage(HelpingMessage, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void OnHelping(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong target);
        reader.ReadValueSafe(out string who);
        reader.ReadValueSafe(out ulong whoId);
        reader.ReadValueSafe(out bool helping);
        reader.ReadValueSafe(out float seconds);
        if (network.IsServer && target != NetworkManager.ServerClientId)
        {
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(target);
            writer.WriteValueSafe(who);
            writer.WriteValueSafe(whoId);
            writer.WriteValueSafe(helping);
            writer.WriteValueSafe(seconds);
            network.CustomMessagingManager.SendNamedMessage(HelpingMessage, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
            return;
        }

        if (!IsDown)
        {
            return;
        }

        if (helping)
        {
            helper = who;
            helperId = whoId;
            helpSeconds = seconds;
            helpStartedAt = Time.time;
        }
        else if (helperId == whoId)
        {
            helpStartedAt = -1f;
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

        // A new body (a new scene: home from the raid it happened on): a down left over from the last one is over, quietly.
        if (IsDown)
        {
            IsDown = false;
            helpStartedAt = -1f;
            aloneSince = -1f;
        }

        holding = false;
        struckDown = false;
        player = now;
        health = now != null ? now.GetComponent<Damageable>() : null;
        movement = now != null ? now.GetComponent<PlayerMovement>() : null;
        stealth = now != null ? now.GetComponent<PlayerStealth>() : null;
        interactor = now != null ? now.GetComponent<PlayerInteractor>() : null;
        input = now != null ? now.GetComponent<PlayerInputHandler>() : null;
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
        // Each fall in the same raid leaves less time than the last.
        int run = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed : 0;
        falls = run == fallsInRun ? falls + 1 : 1;
        fallsInRun = run;
        float seconds = Mathf.Max(settings.ShortestBleedOut, settings.BleedOutSeconds * Mathf.Pow(settings.BleedOutPerFall, falls - 1));
        bleedOutAt = Time.time + seconds;
        aloneSince = -1f;
        health.AddInvulnerability(this);
        // Whatever they were working at (a cache, a friend to help up) stops: it doesn't finish while they lie bleeding.
        if (interactor != null)
        {
            interactor.CancelInteraction();
        }

        SetActing(false);
        GameMessages.Post($"You're down! A friend can help you up within {seconds:0} seconds.", MessageKind.Warning);
    }

    private void StandUp(string helpedBy, bool quiet)
    {
        IsDown = false;
        helpStartedAt = -1f;
        if (health != null)
        {
            health.RemoveInvulnerability(this);
            health.ShowShare(settings != null ? settings.HelpedUpHealth : 0.35f);
        }

        SetActing(true);
        if (!quiet)
        {
            GameMessages.Post($"{helpedBy} helped you up.", MessageKind.Gain);
        }
    }

    private void Die()
    {
        IsDown = false;
        helpStartedAt = -1f;
        health.RemoveInvulnerability(this);
        Release();
        if (stealth != null)
        {
            stealth.SetSneaking(false);
        }

        health.Finish(null);
    }

    // Down: no moving or acting, lying as the dead lie (which friends see through the mirrored animation).
    private void SetActing(bool acting)
    {
        if (player == null)
        {
            return;
        }

        if (input != null)
        {
            input.enabled = acting;
        }

        // The handler switches the whole Player map off with it: moving stays on, to crawl.
        if (!acting && GameInput.Move != null)
        {
            GameInput.Move.Enable();
        }

        if (!acting && movement != null)
        {
            movement.SetMovementInput(Vector3.zero);
        }

        // Down is crouched and dragging along (FrameUpdate), not lying still.
        if (stealth != null)
        {
            stealth.SetSneaking(!acting);
        }
    }

    // A friend on this run who is up and about.
    private bool FriendStandingHere()
    {
        FixedString128Bytes place = CoopPlace.Key;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            // Up and about: neither down nor dead (health 0, watching the others).
            if (friend != null && !friend.IsOwner && friend.IsVisible && !friend.Look.Downed && friend.Look.Health > 0 && friend.Look.Place.Equals(place))
            {
                return true;
            }
        }

        return false;
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        IsDown = false;
    }
}
