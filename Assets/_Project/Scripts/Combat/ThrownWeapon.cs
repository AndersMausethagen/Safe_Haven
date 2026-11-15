using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A weapon art's throw (AttackDefinition › Motion: Throw): a copy of the weapon in hand flies straight out, spinning, striking each
/// hostile it passes (up to the attack's Max Targets) as the swing would, until it has gone Motion Distance or meets something solid;
/// then it flies back to the thrower's hand, striking what it passes on the way, and the hand has it again. A javelin and a hurled axe.
/// Plain flight, no physics: it never gets lost.
/// </summary>
public sealed class ThrownWeapon : MonoBehaviour
{
    private const float OutSpeed = 24f;
    private const float BackSpeed = 28f;
    private const float Radius = 0.55f;
    private const float SpinDegrees = 1080f;
    private const float MostSeconds = 4f;

    private readonly HashSet<IDamageable> struck = new HashSet<IDamageable>();
    private readonly List<HitDetector.Hit> found = new List<HitDetector.Hit>(8);
    private Combatant source;
    private Transform hand;
    private AttackDefinition attack;
    private float weaponDamage;
    private float damageScale;
    private float poiseScale;
    private HitTechnique technique;
    private bool critical;
    private Vector3 direction;
    private float distanceLeft;
    private bool returning;
    private int targetLayers;
    private float until;
    private bool spins;
    private Action caught;

    /// <summary>Raised when it strikes something, for feedback (the hit itself goes through AttackResolver).</summary>
    public static event Action<ThrownWeapon, IDamageable> Struck;

    /// <summary>Sends <paramref name="model"/> (a visual copy of the weapon) out from <paramref name="from"/> toward
    /// <paramref name="towards"/>; <paramref name="onCaught"/> runs when it is back in <paramref name="returnTo"/>'s hand (or lost).</summary>
    public static ThrownWeapon Throw(GameObject model, Combatant thrower, Transform returnTo, AttackDefinition art, float weaponDamage,
        float damage, float poise, HitTechnique technique, bool critical, Vector3 from, Vector3 towards, int layers, Action onCaught)
    {
        var flight = model.AddComponent<ThrownWeapon>();
        model.transform.SetParent(null, true);
        model.transform.position = from;
        towards.y = 0f;
        flight.direction = towards.sqrMagnitude > 0.0001f ? towards.normalized : thrower.transform.forward;
        model.transform.rotation = Quaternion.LookRotation(flight.direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        flight.source = thrower;
        flight.hand = returnTo;
        flight.attack = art;
        flight.weaponDamage = weaponDamage;
        flight.damageScale = damage;
        flight.poiseScale = poise;
        flight.technique = technique | HitTechnique.WeaponArt | HitTechnique.Melee;
        flight.critical = critical;
        flight.distanceLeft = Mathf.Max(1f, art.MotionDistance);
        flight.targetLayers = layers;
        flight.until = Time.time + MostSeconds;
        flight.caught = onCaught;
        // A spear flies point first; anything else spins end over end.
        flight.spins = art.Strike != StrikeKind.Stab;
        if (!flight.spins)
        {
            model.transform.rotation = Quaternion.LookRotation(flight.direction, Vector3.up);
        }

        return flight;
    }

    private void Update()
    {
        if (source == null || hand == null || Time.time >= until)
        {
            Catch();
            return;
        }

        Vector3 from = transform.position;
        Vector3 to;
        if (!returning)
        {
            float step = Mathf.Min(OutSpeed * Time.deltaTime, distanceLeft);
            to = from + direction * step;
            distanceLeft -= step;
            // Something solid in the way stops it short: it glances off and comes back.
            if (Physics.Raycast(from, direction, out RaycastHit wall, step + 0.1f, ~0, QueryTriggerInteraction.Ignore)
                && wall.collider.GetComponentInParent<IDamageable>() == null && !wall.collider.transform.IsChildOf(source.transform))
            {
                to = wall.point - direction * 0.1f;
                HitMaterials.Glance(from, direction, step + 0.1f, source.gameObject);
                returning = true;
            }
            else if (distanceLeft <= 0f || (attack.MaxTargets > 0 && struck.Count >= attack.MaxTargets))
            {
                returning = true;
            }
        }
        else
        {
            Vector3 home = hand.position;
            Vector3 toHand = home - from;
            float step = BackSpeed * Time.deltaTime;
            if (toHand.magnitude <= step + 0.3f)
            {
                Catch();
                return;
            }

            to = from + toHand.normalized * step;
        }

        Strike(from, to);
        transform.position = to;
        if (spins)
        {
            transform.Rotate(Vector3.right, SpinDegrees * Time.deltaTime, Space.Self);
        }
        else if (returning)
        {
            Vector3 heading = to - from;
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
        }
    }

    // Each hostile along this stretch of the flight, once each, as the swing would strike it.
    private void Strike(Vector3 from, Vector3 to)
    {
        Vector3 feetFrom = from - Vector3.up, feetTo = to - Vector3.up;
        HitDetector.FindAlong(feetFrom, feetTo, Radius, source.Faction, targetLayers, 0, found);
        foreach (HitDetector.Hit hit in found)
        {
            if (attack.MaxTargets > 0 && struck.Count >= attack.MaxTargets && !returning)
            {
                break;
            }

            if (!struck.Add(hit.Target))
            {
                continue;
            }

            Vector3 heading = to - from;
            AttackResolver.Apply(source, attack, weaponDamage, hit, from, heading.sqrMagnitude > 0.0001f ? heading.normalized : direction,
                damageScale, poiseScale, technique, critical);
            Struck?.Invoke(this, hit.Target);
        }
    }

    private void Catch()
    {
        Action done = caught;
        caught = null;
        done?.Invoke();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Gone some other way (the scene unloading): the hand must not stay empty.
        caught?.Invoke();
        caught = null;
    }
}
