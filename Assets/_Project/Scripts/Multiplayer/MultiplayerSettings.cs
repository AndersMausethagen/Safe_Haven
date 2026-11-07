using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Co-op tuning (Resources/MultiplayerSettings): how many may play together, the port and Steam app, how often players
/// tell each other where they are and how far behind the others are drawn, and what a friend's body is built from.
/// Read by <see cref="CoopSession"/> and <see cref="NetworkPlayerAvatar"/>.
/// </summary>
[CreateAssetMenu(fileName = "MultiplayerSettings", menuName = "Safe Haven/Settings/Multiplayer Settings")]
public sealed class MultiplayerSettings : GameSettings<MultiplayerSettings>
{
    [Header("Session")]
    [Tooltip("Players in one session, the host included.")]
    [SerializeField, Range(2, 8)] private int maxPlayers = 4;
    [Tooltip("Port for direct (LAN or IP) sessions. Steam sessions don't use it.")]
    [SerializeField] private ushort port = 7777;
    [Tooltip("Steam app id. 480 is Valve's public test app (Spacewar) until Safe Haven has its own.")]
    [SerializeField] private uint steamAppId = 480;
    [Tooltip("Players can only play together on the same version. Raise it whenever what is sent over the network changes.")]
    [SerializeField] private string protocolVersion = "1";
    [Tooltip("Start Steam when playing in the Editor. Off: Editor sessions are direct (LAN/IP). While on, Steam treats the " +
             "Editor as the running game: friends see you in it, and closing Steam closes the Editor. Builds always use Steam.")]
    [SerializeField] private bool steamInEditor;

    [Header("Other players")]
    [Tooltip("The networked object every player gets (Network Player prefab: NetworkObject + NetworkPlayerAvatar).")]
    [SerializeField] private NetworkObject playerPrefab;
    [Tooltip("Where-I-am messages each player sends per second.")]
    [SerializeField, Range(5, 60)] private int posesPerSecond = 20;
    [Tooltip("Seconds other players are drawn behind the moment, so there is always a next pose to move towards. " +
             "Higher hides more lag but shows them later.")]
    [SerializeField, Range(0.02f, 0.5f)] private float interpolationDelay = 0.12f;
    [Tooltip("Metres between two poses beyond which a player is moved at once (a portal, a respawn) rather than slid.")]
    [SerializeField, Min(1f)] private float teleportDistance = 6f;
    [Tooltip("Seconds a friend's animation takes to blend into the state their own game is in.")]
    [SerializeField, Range(0f, 0.5f)] private float animationBlend = 0.12f;

    [Header("Friend's body")]
    [Tooltip("The player's Animator Controller, so a friend moves as they do on their own screen.")]
    [SerializeField] private RuntimeAnimatorController bodyAnimations;
    [Tooltip("Held item placement on the right hand (copy of the player's Held Item View).")]
    [SerializeField] private Vector3 gripPosition;
    [SerializeField] private Vector3 gripRotation;
    [Tooltip("Height of the name over a friend's head, in metres.")]
    [SerializeField, Min(0f)] private float nameHeight = 2.15f;

    [Header("Downed (raids together)")]
    [Tooltip("Seconds a knocked-down player has for a friend to help them up before the blow kills them.")]
    [SerializeField, Min(5f)] private float bleedOutSeconds = 45f;
    [Tooltip("Seconds a friend holds E beside them to help them up.")]
    [SerializeField, Min(0.5f)] private float helpUpSeconds = 3f;
    [Tooltip("Share of walking speed a downed player crawls at.")]
    [SerializeField, Range(0.1f, 0.6f)] private float crawlSpeed = 0.3f;
    [Tooltip("Experience for extracting together: this much for each friend who got out of the same run with you (within the minute, or beside you).")]
    [SerializeField, Min(0)] private int togetherExperience = 30;
    [Tooltip("Share of their health a player gets up with.")]
    [SerializeField, Range(0.05f, 1f)] private float helpedUpHealth = 0.35f;

    [Header("Raids together")]
    [Tooltip("Share of a creature's health added for each friend on the run with the host: more players, tougher creatures.")]
    [SerializeField, Range(0f, 2f)] private float creatureHealthPerFriend = 0.6f;
    [Tooltip("Each fall in the same raid leaves this share of the last one's bleed-out time.")]
    [SerializeField, Range(0.3f, 1f)] private float bleedOutPerFall = 0.7f;
    [Tooltip("The shortest bleed-out, however often a player has fallen.")]
    [SerializeField, Min(3f)] private float shortestBleedOut = 12f;

    public int MaxPlayers => maxPlayers;
    public float CreatureHealthPerFriend => creatureHealthPerFriend;
    public float BleedOutPerFall => bleedOutPerFall;
    public float ShortestBleedOut => shortestBleedOut;
    public float BleedOutSeconds => bleedOutSeconds;
    public float HelpUpSeconds => helpUpSeconds;
    public float HelpedUpHealth => helpedUpHealth;
    public float CrawlSpeed => crawlSpeed;
    public int TogetherExperience => togetherExperience;
    public ushort Port => port;
    public uint SteamAppId => steamAppId;
    /// <summary>
    /// The version friends must share: the asset's, plus the wire format's (<see cref="WireFormat"/>), so a build with
    /// different messages is turned away with "a different version" instead of failing on data it can't read.
    /// </summary>
    public string ProtocolVersion => $"{protocolVersion}.{WireFormat}";

    /// <summary>Raise whenever any co-op message or synced value changes shape.</summary>
    public const int WireFormat = 14;
    public bool SteamInEditor => steamInEditor;
    public NetworkObject PlayerPrefab => playerPrefab;
    public float PoseInterval => 1f / Mathf.Max(1, posesPerSecond);
    public float InterpolationDelay => interpolationDelay;
    public float TeleportDistance => teleportDistance;
    public float AnimationBlend => animationBlend;
    public RuntimeAnimatorController BodyAnimations => bodyAnimations;
    public Pose GripPose => new Pose(gripPosition, Quaternion.Euler(gripRotation));
    public float NameHeight => nameHeight;

#if UNITY_EDITOR
    public void EditorSet(NetworkObject prefab, RuntimeAnimatorController animations, Pose grip)
    {
        playerPrefab = prefab;
        bodyAnimations = animations;
        gripPosition = grip.position;
        gripRotation = grip.rotation.eulerAngles;
    }
#endif
}
