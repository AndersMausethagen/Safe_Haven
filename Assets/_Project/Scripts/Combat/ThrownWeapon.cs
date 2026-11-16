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
    private Quaternion pointFirst = Quaternion.identity;
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
            flight.pointFirst = PointFirst(model);
            model.transform.rotation = Quaternion.LookRotation(flight.direction, Vector3.up) * flight.pointFirst;
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
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up) * pointFirst;
        }
    }

    // A held weapon's model runs along whichever of its own axes it was made on (a spear usually up its Y, held upright in the hand):
    // the longest axis of its meshes, toward the end furthest from the grip at its origin, is the point. This turns that onto +Z, so
    // LookRotation(flight) * this flies it point first instead of shaft-up.
    private static Quaternion PointFirst(GameObject model)
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        Matrix4x4 toModel = model.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null)
            {
                Encapsulate(filter.sharedMesh.bounds, toModel * filter.transform.localToWorldMatrix, ref min, ref max);
            }
        }

        foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh != null)
            {
                Encapsulate(skinned.sharedMesh.bounds, toModel * skinned.transform.localToWorldMatrix, ref min, ref max);
            }
        }

        if (float.IsInfinity(min.x))
        {
            return Quaternion.identity;
        }

        Vector3 size = max - min;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 point = Vector3.zero;
        point[axis] = Mathf.Abs(max[axis]) >= Mathf.Abs(min[axis]) ? 1f : -1f;
        Vector3 side = axis == 1 ? Vector3.forward : Vector3.up;
        return Quaternion.Inverse(Quaternion.LookRotation(point, side));
    }

    private static void Encapsulate(Bounds bounds, Matrix4x4 into, ref Vector3 min, ref Vector3 max)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = into.MultiplyPoint3x4(corner);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
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
            Vector3 along = heading.sqrMagnitude > 0.0001f ? heading.normalized : direction;
            // From the shadows as a blade's or an arrow's: a sure critical on a creature caught unaware, and Ambush Damage.
            Vector3 cameFrom = hit.Point - along * 2f;
            bool sure = PlayerBlows.FromTheShadows(source, hit.Target, cameFrom, critical, out float ambush);
            AttackResolver.Apply(source, attack, weaponDamage, hit, from, along, damageScale * ambush, poiseScale, technique, sure);
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
