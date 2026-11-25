using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What makes each kind of weapon its own (Docs/CombatResearch.md §3.5): the weapon arts that move the player (a dash through, a blink
/// behind, a leap, a throw that comes back; AttackDefinition › How a weapon art moves) and the weapon's Signature (WeaponDefinition):
/// a sword's chain of counters, a dagger's deeper backstab and cheaper quickstep, an axe breaking shields, a greataxe's momentum, a
/// greatsword pushing through hits, a hammer's crush, a spear stopping a charge, fists' flurry. Part of PlayerCombat, in its own file.
/// </summary>
public sealed partial class PlayerCombat
{
    // Momentum (greataxes): per stack, degrees added to the swing's arc and share added to its poise.
    private const float MomentumArcPerStack = 20f;
    private const float MomentumPoisePerStack = 0.15f;
    // Counter Chain (swords): the counter window is this much longer.
    private const float CounterChainWindow = 1.5f;
    // Flurry (fists): rhythm stacks this deep.
    private const int FlurryRhythm = 3;
    // Assassin (daggers): a dodge costs this share of its stamina.
    private const float AssassinDodgeCost = 0.8f;
    // How far behind its body a blink lands, and how close the spot must be clear.
    private const float BlinkGap = 0.9f;
    private const float BlinkClearance = 0.35f;
    // How far above or below where the player stands the blink may land.
    private const float BlinkStep = 1.2f;
    private static readonly Collider[] BlinkColliders = new Collider[16];
    private static readonly RaycastHit[] BlinkHits = new RaycastHit[16];

    private int momentum;
    private float momentumAt = float.NegativeInfinity;
    // Momentum (greataxes): seconds past the chain's grace it holds without a landed swing.
    private const float MomentumHoldSeconds = 1f;
    private int counterCharges;
    private ArtMotion activeMotion;
    private Vector3 dashFrom;
    private readonly List<Collider> passedThrough = new List<Collider>(8);
    private HeldItemView heldView;
    private float artReadyAt;
    // Where an art's blow met what it struck, for its look (SkillFx.ArtLand).
    private readonly List<Vector3> artStruck = new List<Vector3>(8);

    /// <summary>True while the weapon is out on a throw and not yet back in the hand.</summary>
    public bool WeaponInFlight
    {
        get
        {
            if (heldView == null)
            {
                heldView = GetComponentInChildren<HeldItemView>();
            }

            return heldView != null && heldView.Thrown;
        }
    }

    /// <summary>Raised when an art is asked for before it can start (its cooldown, or the weapon still out on a throw): nothing is
    /// spent, and attacking out of the guard leaves the guard up.</summary>
    public event Action ArtNotReady;

    // The art can't start yet: still cooling down from the last one, or the weapon is out on a throw.
    private bool ArtCoolingDown => (Time.time < artReadyAt && !ArtFollowUpOpen) || WeaponInFlight;

    // Follow-ups (Docs/SkillsImprovementResearch_2026-11-24.md 11, Elden Ring's): once an art lands, pressing it again within the window
    // (which runs on past the art's recovery, so slow arts get one too) strikes it again for no Focus, up to twice; the cooldown still
    // runs from the first. Only the same art of the same weapon follows up: a swap or the rune's art is a new art.
    private const float ArtFollowUpWindow = 0.8f;
    private const int ArtFollowUps = 2;
    // The Focus the art being struck took: a circle, nothing for a follow-up.
    private float artPaid;
    private float artFollowUpUntil = -1f;
    private int artFollowUpsLeft;
    private WeaponDefinition artFollowUpWeapon;
    private bool artFollowUpFromRune;
    // Whether the art beginning now is a follow-up (decided as it is paid for, before the rune choice is forgotten).
    private bool artIsFollowUp;

    /// <summary>An art just landed and may be struck again now, for nothing (a follow-up).</summary>
    public bool ArtFollowUpOpen => artFollowUpsLeft > 0 && Time.time < artFollowUpUntil && weapon == artFollowUpWeapon
        && artFromRune == artFollowUpFromRune;

    /// <summary>Raised when the weapon's signature does its thing to a creature (a crush, a stopped charge, a broken guard), for feedback.</summary>
    public event Action<WeaponSignature, MonsterAI> SignatureUsed;

    /// <summary>Raised when a weapon art moves the player: a blink (from, to), the start of a dash, a throw.</summary>
    public event Action<ArtMotion, Vector3, Vector3> ArtMoved;

    /// <summary>Momentum stacks right now (greataxes).</summary>
    public int Momentum => momentum;

    /// <summary>Share of its stamina a dodge costs with this weapon in hand (a dagger's quickstep is cheaper).</summary>
    public float DodgeCostScale => Has(WeaponSignature.Assassin) ? AssassinDodgeCost : 1f;

    private bool Has(WeaponSignature signature) => weapon != null && weapon.Signature == signature;

    private int MaxRhythm => Has(WeaponSignature.Flurry) ? Mathf.Max(FlurryRhythm, Rules.MaxRhythm) : Rules.MaxRhythm;

    // Scenery, not the player and not a creature (the enemy blinked behind stands right there).
    private bool BlinkSolid(Collider other) =>
        other != null && !other.isTrigger && !other.transform.IsChildOf(transform) && other.GetComponentInParent<MonsterAI>() == null;

    private bool BlinkBlocked(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (BlinkSolid(BlinkColliders[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool BlinkSightBlocked(Vector3 a, Vector3 b)
    {
        Vector3 way = b - a;
        int count = Physics.RaycastNonAlloc(a, way.normalized, BlinkHits, way.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (BlinkSolid(BlinkHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool BlinkGround(Vector3 spot, float standing, out float ground)
    {
        ground = standing;
        float best = float.PositiveInfinity;
        Vector3 top = new Vector3(spot.x, standing + BlinkStep + 0.5f, spot.z);
        int count = Physics.RaycastNonAlloc(top, Vector3.down, BlinkHits, BlinkStep * 2f + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (BlinkSolid(BlinkHits[i].collider) && BlinkHits[i].distance < best)
            {
                best = BlinkHits[i].distance;
                ground = BlinkHits[i].point.y;
            }
        }

        return !float.IsPositiveInfinity(best);
    }

    // Blink Behind: at once to the back of the enemy in focus, facing it, if it is within reach and there is room. False: the art strikes
    // where it stands.
    private bool BlinkBehindFocus(float reach)
    {
        if (focusTarget == null || !focusTarget.HasFocus || focusTarget.Target == null || (movement != null && movement.IsSeated))
        {
            return false;
        }

        Vector3 from = transform.position;
        Damageable enemy = focusTarget.Target;
        Vector3 at = enemy.transform.position;
        if ((at - from).sqrMagnitude > reach * reach)
        {
            return false;
        }

        float body = enemy.TryGetComponent(out CapsuleCollider capsule) ? capsule.radius * Mathf.Max(enemy.transform.lossyScale.x, enemy.transform.lossyScale.z) : 0.5f;
        Vector3 spot = DodgeRules.SpotBehind(at, enemy.transform.forward, body + BlinkGap);
        float height = controller != null ? controller.height : 1.8f;
        // The ground there, not the height left behind: on a slope the spot was in the hill or in the air.
        if (!BlinkGround(spot, from.y, out float ground))
        {
            return false;
        }

        spot.y = ground;
        // Room to stand, and a clear way round the enemy. Everything is on one layer here, so the player's own colliders and creatures
        // are told apart by who they belong to: a layer mask that left out the player's layer left out the whole world, and the
        // blink went into rocks, walls and through gates.
        Vector3 chest = Vector3.up * (height * 0.6f);
        if (BlinkBlocked(Physics.OverlapCapsuleNonAlloc(spot + Vector3.up * (BlinkClearance + 0.05f), spot + Vector3.up * (height - BlinkClearance),
                BlinkClearance, BlinkColliders, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            || BlinkSightBlocked(from + chest, spot + chest) || BlinkSightBlocked(at + chest, spot + chest))
        {
            return false;
        }

        if (controller != null) controller.enabled = false;
        transform.position = spot;
        if (controller != null) controller.enabled = true;
        Vector3 facing = at - spot;
        facing.y = 0f;
        activeDirection = facing.sqrMagnitude > 0.0001f ? facing.normalized : activeDirection;
        ArtMoved?.Invoke(ArtMotion.BlinkBehind, from, spot);
        return true;
    }

    // A dash goes through bodies, as a dodge does; they are solid again when the swing ends.
    private void PassThroughBodies()
    {
        if (controller == null)
        {
            return;
        }

        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && monster.TryGetComponent(out Collider solid) && solid.enabled
                && (monster.transform.position - transform.position).sqrMagnitude <= (activeLunge + 4f) * (activeLunge + 4f))
            {
                BodyPassThrough.Ignore(controller, solid);
                passedThrough.Add(solid);
            }
        }

        ArtMoved?.Invoke(ArtMotion.DashThrough, transform.position, transform.position + activeDirection * activeLunge);
    }

    private void GiveBodiesBack()
    {
        foreach (Collider solid in passedThrough)
        {
            if (solid != null && controller != null)
            {
                BodyPassThrough.Restore(controller, solid);
            }
        }

        passedThrough.Clear();
    }

    /// <summary>Raised as a cast's bolts leave the hand: its attack, where from, and where aimed (co-op shows it to friends).</summary>
    public event System.Action<AttackDefinition, Vector3, Vector3> CastLoosed;

    private Damageable castTarget;
    private int castTargetFrame = -1;
    private static readonly RaycastHit[] CastSweep = new RaycastHit[16];

    /// <summary>True through the wind-up of a cast (a staff's bolt or burst): it aims, and creatures read it as shooting.</summary>
    public bool IsCasting => IsWindingUp && activeAttack != null && activeAttack.CastProjectile != null;

    /// <summary>Where the swing in progress is aimed (flat).</summary>
    public Vector3 ActiveDirection => activeDirection;

    /// <summary>The creature a cast in its wind-up would strike first along its way, or null: outlined as a bow's target is.</summary>
    public Damageable CastTarget
    {
        get
        {
            if (!IsCasting)
            {
                return null;
            }

            if (castTargetFrame != Time.frameCount)
            {
                castTargetFrame = Time.frameCount;
                castTarget = null;
                Vector3 from = transform.position + Vector3.up * 1.3f;
                int count = Physics.SphereCastNonAlloc(from, Mathf.Max(0.3f, activeAttack.CastRadius * 2f), activeDirection, CastSweep,
                    Mathf.Max(1f, activeAttack.CastRange), hitLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    Damageable body = CastSweep[i].collider.GetComponentInParent<Damageable>();
                    if (body != null && body.IsAlive && body.Category == TargetCategory.Creature && combatant.Faction.WillAttack(body.Faction)
                        && CastSweep[i].distance < nearest)
                    {
                        nearest = CastSweep[i].distance;
                        castTarget = body;
                    }
                }
            }

            return castTarget;
        }
    }

    /// <summary>
    /// Whether the weapon's opening swing, struck now from the shadows, would be a Silent Takedown of <paramref name="body"/>: the perk's own
    /// arithmetic, so the focus ring can say so before the blow (Docs/RangedStealthResearch.md §3.2).
    /// </summary>
    public bool WouldTakedown(Damageable body)
    {
        if (body == null || weapon == null || weapon.Combo.Count == 0 || weapon.Combo[0] == null || !Techniques.Has(PerkTechnique.SilentTakedown)
            || !body.TryGetComponent(out MonsterAI monster) || !monster.IsUnawareOf(transform.position)
            || body.GetComponent<EliteCreature>() != null || body.GetComponentInParent<BossEncounter>() != null)
        {
            return false;
        }

        // The first swing as SilentTakedown judges it as it lands (with the Melee skill and Ambush Damage): one rule for both.
        AttackDefinition attack = weapon.Combo[0];
        PlayerBlows.FromTheShadows(combatant, body, transform.position, false, out float ambush);
        float blow = PlayerBlows.UnawareBlowWorth(combatant, body, attack, weapon.Damage, Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.MeleeDamage)) * ambush);
        return StrikeRules.IsTakedown(body.CurrentHealth, blow, TechniqueSettings.Current.TakedownReach);
    }

    // A staff's cast: its bolts fly from the hand as the blow would land (MonsterProjectile: straight, swept, stopped by scenery), striking
    // as the attack would at the weapon's damage; a fan when it casts several. Channel (staves): every cast earns Focus.
    private void Cast(float damageMultiplier, float poiseMultiplier)
    {
        AttackDefinition attack = activeAttack;
        // The swing's own techniques go with its bolts (a heavy cast is a heavy blow to a shield, a counter cast a sure critical), as a
        // blade's and a thrown weapon's do.
        var volley = new CastVolley
        {
            Multiplier = damageMultiplier, Poise = poiseMultiplier, Technique = HitTechnique.Spell | activeTechnique, Critical = activeForceCritical,
        };
        var bolt = new MonsterAttack
        {
            attack = attack, damage = activeWeaponDamage, projectileSpeed = attack.CastSpeed, projectileRadius = attack.CastRadius,
            maximumRange = attack.CastRange, hazardPrefab = attack.CastHazard,
        };
        Vector3 from = transform.position + Vector3.up * 1.3f + activeDirection * 0.7f;
        int count = Mathf.Max(1, attack.CastCount);
        for (int i = 0; i < count; i++)
        {
            float turn = (i - (count - 1) * 0.5f) * attack.CastSpread;
            Vector3 aim = Quaternion.Euler(0f, turn, 0f) * activeDirection;
            GameObject flight = PrefabPool.Spawn(attack.CastProjectile, from, Quaternion.LookRotation(aim));
            if (flight.TryGetComponent(out MonsterProjectile projectile))
            {
                projectile.Initialize(combatant, bolt, aim, volley);
            }
            else
            {
                PrefabPool.Release(flight);
            }
        }

        if (Has(WeaponSignature.Channel) && focus != null)
        {
            focus.Earn(Rules.FocusPerHit * weapon.SignaturePower);
        }

        ArtMoved?.Invoke(ArtMotion.Throw, from, from + activeDirection * attack.CastRange);
        CastLoosed?.Invoke(attack, from, activeDirection);
    }

    // Throw: the weapon in hand flies out and back (ThrownWeapon). False with nothing in the hand to throw, or one already out: the art
    // then lands as an ordinary blow.
    private bool ThrowWeapon(float damage, float poise)
    {
        if (heldView == null)
        {
            heldView = GetComponentInChildren<HeldItemView>();
        }

        if (heldView == null || heldView.Thrown || heldView.Current == null || heldView.Shown == null)
        {
            return false;
        }

        Transform hand = heldView.Current.transform.parent;
        GameObject copy = HeldItemView.CreateModel(heldView.Shown, hand);
        if (copy == null)
        {
            return false;
        }

        float scale = damage * Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.MeleeDamage));
        Vector3 from = hand.position;
        HeldItemView view = heldView;
        view.Thrown = true;
        ThrownWeapon.Throw(copy, combatant, hand, activeAttack, activeWeaponDamage, scale, poise, activeTechnique, activeForceCritical,
            from, activeDirection, hitLayers, () => { if (view != null) view.Thrown = false; });
        ArtMoved?.Invoke(ArtMotion.Throw, from, from + activeDirection * activeAttack.MotionDistance);
        return true;
    }
}
