using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a falling tree does to the world on its way down. Everything under it is crushed, in proportion to how fast that part
/// of it is moving, so the end of the fall kills and the start doesn't: creatures are credited to whoever felled it, allies
/// step clear, and the feller takes a share if they stand under their own tree. Standing trees it meets are knocked, shake
/// down leaves and take speed out of the fall in turn, the heavier the more. Runs only while the tree is falling.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class TreeImpacts : MonoBehaviour
{
    private const int MostNearby = 48;
    private const float BrushRadius = 0.6f;
    private const float SlowestKnock = 0.8f;
    private const float StaggerSeconds = 1.2f;
    private const float StrikeShake = 0.18f;

    private struct Neighbour
    {
        public ChoppableTree Tree;
        public TerrainTreeFelling Terrain;
        public int Index;
        public Vector3 Foot;
        public float Height;
        public float Radius;
        public bool Struck;
    }

    private static readonly Collider[] Nearby = new Collider[MostNearby];

    private readonly List<Neighbour> neighbours = new List<Neighbour>();
    private readonly HashSet<Damageable> crushed = new HashSet<Damageable>();
    private Rigidbody body;
    private TreeParts parts;
    private Combatant feller;
    private float size;
    private float weight;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        enabled = false;
    }

    /// <summary>Starts watching for what the tree meets on its way down.</summary>
    public void Begin(TreeParts parts, float size, GameObject feller)
    {
        this.parts = parts;
        this.size = size;
        this.feller = feller != null ? feller.GetComponent<Combatant>() : null;
        weight = TreeContact.Mass(parts.TrunkRadius, parts.Height);
        FindNeighbours();
        enabled = true;
    }

    /// <summary>It has hit the ground.</summary>
    public void End() => enabled = false;

    private void FindNeighbours()
    {
        float reach = parts.Height + 3f;
        foreach (Collider other in Physics.OverlapSphere(parts.Base, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            ChoppableTree tree = other.GetComponentInParent<ChoppableTree>();
            if (tree != null && !tree.IsStandIn && (tree.transform.position - parts.Base).sqrMagnitude > 0.25f)
            {
                neighbours.Add(new Neighbour { Tree = tree, Foot = tree.transform.position, Height = tree.TrunkHeight, Radius = tree.TrunkRadius });
            }
        }

        foreach (TerrainTreeFelling terrain in TerrainTreeFelling.Active)
        {
            foreach (TerrainTreeFelling.Standing tree in terrain.StandingNear(parts.Base, reach))
            {
                neighbours.Add(new Neighbour { Terrain = terrain, Index = tree.Index, Foot = tree.Foot, Height = tree.Height, Radius = tree.Radius });
            }
        }
    }

    private void FixedUpdate()
    {
        Vector3 from = transform.TransformPoint(Vector3.up * FelledTree.CutHeight);
        Vector3 to = transform.TransformPoint(Vector3.up * parts.Height);
        for (int i = 0; i < neighbours.Count; i++)
        {
            Neighbour other = neighbours[i];
            if (other.Struck)
            {
                continue;
            }

            float distance = TreeContact.Distance(from, to, other.Foot, other.Foot + Vector3.up * other.Height, out _, out Vector3 point);
            if (distance > parts.TrunkRadius + other.Radius + BrushRadius)
            {
                continue;
            }

            Vector3 velocity = body.GetPointVelocity(point);
            if (velocity.magnitude < SlowestKnock)
            {
                continue;
            }

            other.Struck = true;
            neighbours[i] = other;
            Strike(other, point, velocity);
        }

        Crush(from, to);
    }

    private void Strike(in Neighbour other, Vector3 point, Vector3 velocity)
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        float standing = TreeContact.Mass(other.Radius, other.Height);
        float degrees = TreeContact.KnockDegrees(velocity.magnitude, weight, standing, rules.MostKnockDegrees, rules.CrushFullSpeed);
        TreeParts struck = other.Tree != null ? other.Tree.Knocked(velocity, degrees) : other.Terrain.Knocked(other.Index, velocity, degrees);
        if (struck != null && struck.HasFoliage)
        {
            TreeEffects.Play(rules.LeafFall, struck.Base + struck.Crown.center, Quaternion.identity, struck.Crown.size, struck.LeafTint, Mathf.RoundToInt(10f + degrees * 8f));
        }

        // All of its motion, not only its spin: on the hinge, whatever speed is left in its middle would turn straight back into spin.
        float kept = TreeContact.SpinKept(weight, standing);
        body.angularVelocity *= kept;
        body.linearVelocity *= kept;
        AudioManager.Play(rules.StrikeSound, point, size);
        CameraShake.Add(StrikeShake * Mathf.Clamp01(degrees / rules.MostKnockDegrees));
    }

    private void Crush(Vector3 from, Vector3 to)
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        float radius = parts.TrunkRadius + rules.CrushReach;
        int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, Nearby, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Damageable victim = Nearby[i].GetComponentInParent<Damageable>();
            if (victim == null || !victim.IsAlive || victim.Category == TargetCategory.Structure || crushed.Contains(victim))
            {
                continue;
            }

            Vector3 at = victim.transform.position + Vector3.up;
            TreeContact.Distance(from, to, at, at, out _, out Vector3 point);
            Vector3 velocity = body.GetPointVelocity(point);
            float damage = rules.CrushDamageTo(victim.Faction, size, velocity.magnitude, victim.MaxHealth);
            if (rules.CrushShare(victim.Faction) <= 0f)
            {
                crushed.Add(victim);
            }
            else if (damage > 0f)
            {
                crushed.Add(victim);
                Hit(victim, damage, point, velocity);
            }
        }
    }

    private void Hit(Damageable victim, float damage, Vector3 point, Vector3 velocity)
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        // Only what the feller brought down on someone else counts as their kill; their own tree on their own head is an accident.
        Combatant source = victim.Faction.IsPlayerSide() ? null : feller;
        DamageResult result = victim.ApplyDamage(new DamageInfo(source, null, damage, DamageType.Physical, false, point, velocity.normalized, damage, HitTechnique.None));
        AudioManager.Play(rules.CrushSound, point, size);
        if (victim.IsAlive)
        {
            if (victim.TryGetComponent(out IStaggerable staggered))
            {
                staggered.Stagger(StaggerSeconds);
            }
        }
        else if (result.Killed && victim.TryGetComponent(out MonsterAI monster) && monster.Definition != null)
        {
            GameMessages.Post($"The tree crushes the {monster.Definition.DisplayName}!", MessageKind.Gain);
        }
    }
}
