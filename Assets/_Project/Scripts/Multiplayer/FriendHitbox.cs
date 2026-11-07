using UnityEngine;

/// <summary>
/// Co-op, in the host's game: makes a friend's body something creatures notice, chase and strike, as they do the player
/// (<see cref="MonsterTargeting.IsPriorityTarget"/>). It takes no damage here. The host's creature tells the friend's game
/// it is striking (<see cref="CoopCreatures"/>), and the blow plays out there, against where the friend really is.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
public sealed class FriendHitbox : MonoBehaviour, IDamageable
{
    /// <summary>The friend's connection, so a strike at this body can be sent to them.</summary>
    public ulong ClientId { get; private set; }

    /// <summary>Down, waiting for help: creatures leave them be.</summary>
    public bool IsDown { get; set; }

    public bool IsAlive => isActiveAndEnabled && !IsDown;

    /// <summary>How easily they are seen, 0 to 1, as their own game's stealth says (sneaking, in cover, fighting).</summary>
    public float Visibility { get; set; } = 1f;

    private static readonly System.Collections.Generic.List<FriendHitbox> active = new System.Collections.Generic.List<FriendHitbox>();

    /// <summary>Friends' bodies standing in the host's game now.</summary>
    public static System.Collections.Generic.IReadOnlyList<FriendHitbox> Active => active;

    private void OnEnable() => active.Add(this);

    private void OnDisable() => active.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();
    public Faction Faction => Faction.Player;
    public TargetCategory Category => TargetCategory.Creature;

    /// <summary>Makes a friend's body a target, about the size of the player.</summary>
    public static FriendHitbox Attach(GameObject body, ulong clientId)
    {
        if (!body.TryGetComponent(out FriendHitbox hitbox))
        {
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            hitbox = body.AddComponent<FriendHitbox>();
        }

        // On the player's layer, which creatures' senses look at.
        if (Player.Current != null)
        {
            body.layer = Player.Current.gameObject.layer;
        }

        hitbox.ClientId = clientId;
        return hitbox;
    }

    /// <summary>The friend's own game decides what a blow does to them.</summary>
    public DamageResult ApplyDamage(in DamageInfo damage) => DamageResult.None;
}
