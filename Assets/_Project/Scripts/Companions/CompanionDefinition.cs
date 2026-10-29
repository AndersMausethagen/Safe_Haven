using System;
using UnityEngine;

/// <summary>An order a companion takes, with what the player says to give it and what the companion answers.</summary>
[Serializable]
public sealed class CompanionOrderOption
{
    public CompanionOrder order;
    public string ask;
    [TextArea(1, 3)] public string reply;

    public CompanionOrderOption(CompanionOrder order, string ask, string reply)
    {
        this.order = order;
        this.ask = ask;
        this.reply = reply;
    }
}

/// <summary>
/// How a kind of companion fights and which orders it takes (GDD §15.5): a melee fighter, an archer and so on, shared by
/// every companion of that kind and never changed while playing. Who the person is (name, lines) stays on their
/// ResidentDefinition; health and armour come from the Combatant's stats. Create > Safe Haven > Companions > Companion.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Companions/Companion", fileName = "Companion")]
public sealed class CompanionDefinition : ScriptableObject
{
    [Header("Attack")]
    [Tooltip("The swing (range, arc, hit delay, damage share). A ranged companion uses it for the arrow's hit instead.")]
    [SerializeField] private AttackDefinition attack;
    [Tooltip("Damage of the weapon behind the attack, as a player's weapon would give it.")]
    [SerializeField, Min(0f)] private float weaponDamage = 8f;
    [Tooltip("Seconds from one attack to the next.")]
    [SerializeField, Min(0.2f)] private float attackInterval = 1.6f;
    [Tooltip("Shoots this arrow instead of swinging (its projectile, damage and drop). Never runs out.")]
    [SerializeField] private ItemDefinition arrow;
    [Tooltip("An archer shoots from up to this far, in metres.")]
    [SerializeField, Min(2f)] private float shootingRange = 14f;
    [Tooltip("Launch speed of its arrows, in metres per second.")]
    [SerializeField, Min(1f)] private float arrowSpeed = 30f;
    [Tooltip("Share of the arrow's damage each shot does, so a companion helps without taking the fight from the player.")]
    [SerializeField, Range(0.1f, 2f)] private float arrowDamageShare = 0.6f;
    [SerializeField] private SoundDefinition attackSound;

    [Header("Orders")]
    [Tooltip("Following: fights enemies this close to the leader.")]
    [SerializeField, Min(1f)] private float engageRadius = 9f;
    [Tooltip("Defending: also fights enemies this close to the leader, besides those attacking the leader or itself.")]
    [SerializeField, Min(0.5f)] private float defendRadius = 3.5f;
    [Tooltip("Staying: fights enemies this close to its spot.")]
    [SerializeField, Min(1f)] private float holdRadius = 6f;
    [Tooltip("Retreating: keeps at least this far from enemies.")]
    [SerializeField, Min(2f)] private float retreatDistance = 10f;
    [Tooltip("It walks back to its place once this far from it.")]
    [SerializeField, Min(0.3f)] private float slack = 1.5f;
    [Tooltip("Enemies are noticed this far from it or its leader.")]
    [SerializeField, Min(5f)] private float awareness = 20f;
    [Tooltip("The orders the player can give in conversation, and the answers. Remove a line to refuse that order.")]
    [SerializeField] private CompanionOrderOption[] orders =
    {
        new CompanionOrderOption(CompanionOrder.Follow, "Follow me.", "Right behind you."),
        new CompanionOrderOption(CompanionOrder.Stay, "Wait here.", "I'll hold this spot."),
        new CompanionOrderOption(CompanionOrder.Defend, "Stay close and guard me.", "Nothing gets to you without going through me."),
        new CompanionOrderOption(CompanionOrder.Retreat, "Stay out of the fighting.", "I'll keep back."),
        new CompanionOrderOption(CompanionOrder.AttackTarget, "Attack what I attack.", "Point the way."),
    };

    [Header("Downed")]
    [Tooltip("Knocked down at no health, it gets up by itself after this many seconds.")]
    [SerializeField, Min(1f)] private float downedSeconds = 12f;
    [Tooltip("Share of its health it gets up with by itself.")]
    [SerializeField, Range(0.05f, 1f)] private float getUpHealth = 0.4f;
    [Tooltip("Seconds the player holds E beside it to help it up sooner.")]
    [SerializeField, Min(0.2f)] private float reviveSeconds = 3f;
    [Tooltip("Share of its health it gets up with when the player helps it up.")]
    [SerializeField, Range(0.05f, 1f)] private float reviveHealth = 0.6f;

    public AttackDefinition Attack => attack;
    public float WeaponDamage => weaponDamage;
    public float AttackInterval => attackInterval;
    public ItemDefinition Arrow => arrow != null && arrow.IsArrow ? arrow : null;
    public bool IsRanged => Arrow != null;
    public float ShootingRange => shootingRange;
    public float ArrowSpeed => arrowSpeed;
    public float ArrowDamageShare => arrowDamageShare;
    public SoundDefinition AttackSound => attackSound;
    public float Awareness => awareness;
    public CompanionOrderOption[] Orders => orders;
    public float DownedSeconds => downedSeconds;
    public float GetUpHealth => getUpHealth;
    public float ReviveSeconds => reviveSeconds;
    public float ReviveHealth => reviveHealth;

    public CompanionRanges Ranges => new CompanionRanges(engageRadius, defendRadius, holdRadius, retreatDistance, slack);

    /// <summary>How close it must be to hit: the swing's reach, or the shooting range.</summary>
    public float ReachFor(float targetRadius) =>
        IsRanged ? shootingRange : (attack != null ? attack.Range : 1.8f) * 0.85f + targetRadius;

    public bool Takes(CompanionOrder order)
    {
        foreach (CompanionOrderOption option in orders)
        {
            if (option != null && option.order == order) return true;
        }

        return false;
    }
}
