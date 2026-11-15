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

    private int momentum;
    private int counterCharges;
    private ArtMotion activeMotion;
    private Vector3 dashFrom;
    private readonly List<Collider> passedThrough = new List<Collider>(8);
    private HeldItemView heldView;

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

    // Blink Behind: at once to the back of the enemy in focus, facing it, if it is within reach and there is room. False: the art strikes
    // where it stands.
    private bool BlinkBehindFocus(float reach)
    {
        if (focusTarget == null || !focusTarget.HasFocus || focusTarget.Target == null)
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
        spot.y = from.y;
        float height = controller != null ? controller.height : 1.8f;
        if (Physics.CheckCapsule(spot + Vector3.up * (BlinkClearance + 0.05f), spot + Vector3.up * (height - BlinkClearance), BlinkClearance,
                hitLayers & ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
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
                Physics.IgnoreCollision(controller, solid, true);
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
                Physics.IgnoreCollision(controller, solid, false);
            }
        }

        passedThrough.Clear();
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
