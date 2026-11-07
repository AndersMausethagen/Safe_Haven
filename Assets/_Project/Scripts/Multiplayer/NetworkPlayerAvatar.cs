using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One per player in a co-op session (the Network Player prefab, spawned by Netcode for each connection). On its owner's
/// machine it watches the local <see cref="Player"/> and tells the others how they look (<see cref="PlayerLookState"/>,
/// a network variable), where they are and what they are doing (<see cref="PoseSnapshot"/>s, a few each second,
/// unreliable) and their fishing line when one is out (<see cref="FishingLineState"/>, a network variable, so friends see
/// the float: <see cref="RemoteFishing"/>). Everywhere else it draws them as a <see cref="RemotePlayerBody"/>, a little in the past so movement is
/// smooth, and only while they are in the same place as you.
/// <para>
/// The player you control stays the ordinary single-player <see cref="Player"/>, run entirely on your machine: co-op adds
/// friends beside it rather than changing it. Lives across scene changes (Netcode's scene management is off; each game
/// changes scene on its own).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkPlayerAvatar : NetworkBehaviour
{
    private const float LookCheckInterval = 0.5f;
    private static readonly List<NetworkPlayerAvatar> all = new List<NetworkPlayerAvatar>();

    private readonly NetworkVariable<PlayerLookState> look =
        new NetworkVariable<PlayerLookState>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<FishingLineState> fishing =
        new NetworkVariable<FishingLineState>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly PoseBuffer poses = new PoseBuffer();
    private MultiplayerSettings settings;
    private RemotePlayerBody body;
    private FriendRevive revive;
    private FriendHitbox hitbox;
    private int shownState = -1;
    private AnimatorMirror ownMirror;
    private Animator ownAnimator;
    private PoseSnapshot outgoing;
    private float nextPose;
    private float nextLookCheck;
    private bool announced;
    private RemoteFishing remoteFishing;
    // The owner's last cast, and the server time it left the rod (worked out once per cast, so clock drift doesn't resend it).
    private float castLandsAt = float.NaN;
    private double castAt;

    /// <summary>Every player in the session, you included.</summary>
    public static IReadOnlyList<NetworkPlayerAvatar> All => all;

    /// <summary>Raised when a friend arrives or leaves (with their name), for messages and lists.</summary>
    public static event Action<NetworkPlayerAvatar> FriendArrived;
    public static event Action<NetworkPlayerAvatar> FriendLeft;

    public PlayerLookState Look => look.Value;
    public string DisplayName => look.Value.DisplayName;
    /// <summary>True when a friend is in the same place as you and drawn there.</summary>
    public bool IsVisible => body != null && body.gameObject.activeSelf;
    public RemotePlayerBody Body => body;

    public override void OnNetworkSpawn()
    {
        settings = MultiplayerSettings.Current;
        DontDestroyOnLoad(gameObject);
        all.Add(this);
        name = IsOwner ? "Network Player (you)" : $"Network Player {OwnerClientId}";
        if (IsOwner)
        {
            PublishLook();
            return;
        }

        look.OnValueChanged += OnLookChanged;
        if (look.Value.Name.Length > 0)
        {
            Announce();
        }
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);
        look.OnValueChanged -= OnLookChanged;
        if (!IsOwner && announced)
        {
            FriendLeft?.Invoke(this);
        }

        poses.Clear();
        remoteFishing?.Destroy();
        remoteFishing = null;
        if (body != null)
        {
            Destroy(body.gameObject);
            body = null;
        }
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsOwner)
        {
            UpdateOwn();
        }
        else
        {
            UpdateFriend();
        }
    }

    // ---- On the owner's machine: tell the others.

    private void UpdateOwn()
    {
        Player player = Player.Current;
        // Going down (or getting up) is told at once: friends need the "Help up" straight away.
        // Going down (or getting up) and starting or stopping to sneak are told at once; the rest twice a second.
        if (Time.unscaledTime >= nextLookCheck || look.Value.Downed != CoopDowned.IsDown
            || Mathf.Abs(look.Value.Visibility - PlayerLookState.VisibilityByte(Player.Current)) > 40)
        {
            nextLookCheck = Time.unscaledTime + LookCheckInterval;
            PublishLook();
        }

        PublishFishing();
        if (player == null || Time.unscaledTime < nextPose)
        {
            return;
        }

        nextPose = Time.unscaledTime + settings.PoseInterval;
        Animator animator = player.GetComponent<Animator>();
        if (animator != ownAnimator)
        {
            ownAnimator = animator;
            ownMirror = animator != null ? new AnimatorMirror(animator) : null;
        }

        Transform at = player.transform;
        transform.SetPositionAndRotation(at.position, at.rotation);
        outgoing.Time = NetworkManager.ServerTime.Time;
        outgoing.Position = at.position;
        outgoing.Yaw = at.eulerAngles.y;
        ownMirror?.Capture(ref outgoing.Animation);
        PoseRpc(outgoing);
    }

    private void PublishLook()
    {
        // In a raid the place is the run, not only its scene: friends see each other only on the same map.
        PlayerLookState now = PlayerLookState.Of(Player.Current, CoopPlace.Here);
        if (!now.Equals(look.Value))
        {
            look.Value = now;
        }
    }

    // Sent when the line goes out, changes phase or reels in a step; nothing while it waits in the water.
    private void PublishFishing()
    {
        PlayerFishing rod = PlayerFishing.Current;
        FishingLineState now = default;
        if (rod != null && rod.LineOut)
        {
            FishingCast cast = rod.LastCast;
            if (cast.LandsAt != castLandsAt)
            {
                castLandsAt = cast.LandsAt;
                castAt = FishingLineState.CastTime(cast.LandsAt, NetworkManager.ServerTime.Time, Time.time);
            }

            now = FishingLineState.Of(rod.State, cast, rod.ReelCatch, castAt);
        }

        if (!now.Equals(fishing.Value))
        {
            fishing.Value = now;
        }
    }

    [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable)]
    private void PoseRpc(PoseSnapshot pose)
    {
        poses.Add(pose);
    }

    // ---- On everyone else's machine: draw them.

    private void UpdateFriend()
    {
        bool here = poses.Count > 0 && look.Value.Place.Length > 0 && look.Value.Place.Equals(CoopPlace.Key);
        if (!here)
        {
            if (body != null && body.gameObject.activeSelf)
            {
                body.gameObject.SetActive(false);
            }

            remoteFishing?.Hide();
            return;
        }

        if (body == null)
        {
            body = RemotePlayerBody.Create(transform, settings);
            body.Dress(look.Value, Content());
            // The host's creatures go for friends too (CoopCreatures sends them the blows).
            if (IsServer)
            {
                hitbox = FriendHitbox.Attach(body.gameObject, OwnerClientId);
            }

            revive = FriendRevive.Create(body.transform, look.Value.DisplayName, OwnerClientId, settings.HelpUpSeconds);
        }
        else if (!body.gameObject.activeSelf)
        {
            body.gameObject.SetActive(true);
        }

        // Down in a shared raid: friends can help them up, and creatures leave them be.
        revive.Show(look.Value.Downed, look.Value.DisplayName);
        bool fallen = look.Value.Health == 0 && GameSession.ActiveRaid != null;
        int state = (look.Value.Downed ? 1 : 0) | (fallen ? 2 : 0);
        if (state != shownState)
        {
            shownState = state;
            body.ShowState(look.Value.DisplayName, look.Value.Downed, fallen);
        }
        if (hitbox != null)
        {
            hitbox.IsDown = look.Value.Downed || fallen;
            hitbox.Visibility = look.Value.Visibility / 255f;
        }

        double renderTime = NetworkManager.ServerTime.Time - settings.InterpolationDelay;
        if (poses.Sample(renderTime, settings.TeleportDistance, out Vector3 position, out float yaw, out AnimatorFrame animation))
        {
            body.Place(position, yaw);
            body.Mirror.Apply(animation, settings.AnimationBlend);
        }

        remoteFishing ??= new RemoteFishing(body.transform);
        remoteFishing.Show(fishing.Value, NetworkManager.ServerTime.Time, Time.time, Time.deltaTime);
    }

    private void OnLookChanged(PlayerLookState before, PlayerLookState now)
    {
        if (!announced && now.Name.Length > 0)
        {
            Announce();
        }

        if (!before.Place.Equals(now.Place))
        {
            // A new place: what was received before was somewhere else.
            poses.Clear();
        }

        if (body != null)
        {
            body.Dress(now, Content());
        }
    }

    private void Announce()
    {
        announced = true;
        FriendArrived?.Invoke(this);
    }

    private static ContentDatabase content;

    private static IContentLookup Content() => content != null ? content : content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        all.Clear();
        FriendArrived = null;
        FriendLeft = null;
    }
}
