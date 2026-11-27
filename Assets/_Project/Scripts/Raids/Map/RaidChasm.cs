using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The ravines of a generated map take whoever falls into one: the player dies once they are a few metres below the rim, as nobody
/// climbs those walls. A neck that carries a trail across is solid ground, so it never counts. A dodge's moment of invulnerability only
/// delays it; a player kept unhurt on purpose (the cheat menu's Invincible, a test) is put back where they last stood safely instead,
/// so a test run doesn't end at the bottom. A creature that a blow would push over an edge goes over: it falls and dies, credited to
/// whoever struck it, and what it carried lies at the bottom. Creatures that can't be pushed (Hit Reaction › Knockback Scale 0) and bosses
/// hold.
/// Made by RaidMapGenerator.PlaceRavines, with the plan it reads.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidChasm : MonoBehaviour
{
    /// <summary>Metres below the rim at which a fall can no longer be stopped.</summary>
    public const float FallDepth = 5f;

    private const float SafeEvery = 0.4f;
    private const float OnGround = 0.6f;
    private const float RescueAfter = 1.5f;
    // A creature standing this much nearer the edge than the push still goes: it is off balance at the brink.
    private const float EdgeGive = 0.6f;
    // Ground this far below the rim is past the lip: from there nothing keeps its feet.
    private const float PastTheLip = 2f;
    private const float PushProbe = 0.25f;
    private const float FallSpeed = 3f;
    private const float FallLift = 1.5f;

    private readonly HashSet<Damageable> falling = new HashSet<Damageable>();
    private float lastHeight;
    private bool hasLast;

    private RaidLayoutPlan plan;
    private Terrain terrain;
    private Vector3 lastSafe;
    private bool knowsSafe;
    private float nextSafe;
    private float fallingFor;

    public RaidLayoutPlan Plan => plan;

    public void Bind(RaidLayoutPlan layout, Terrain ground)
    {
        plan = layout;
        terrain = ground;
    }

    /// <summary>
    /// True when a body at this height, over this point (both relative to the map's corner, in metres), has fallen into a ravine: it is
    /// over a ravine's cut, well below its rim, and so is the ground under it, so a neck that carries a trail across never counts.
    /// </summary>
    /// <remarks>In a ravine with a way down, walking down to the floor is no fall: there only falling counts (<paramref name="fallSpeed"/>,
    /// metres a second downwards).</remarks>
    public static bool HasFallen(RaidLayoutPlan plan, Vector2 point, float height, float groundBelow, float fallSpeed = float.PositiveInfinity)
    {
        return plan != null && plan.OverRavine(point, out RaidLayoutRavine ravine, out float rim, RaidLayoutRavine.RagReach)
            && height < rim - FallDepth && groundBelow < rim - FallDepth && (!ravine.Descent || fallSpeed >= FallingFrom);
    }

    /// <summary>Metres a second downwards from which a body is falling rather than walking down a slope.</summary>
    public const float FallingFrom = 6f;

    private void OnEnable() => Damageable.AnyDamaged += OnAnyDamaged;

    private void OnDisable() => Damageable.AnyDamaged -= OnAnyDamaged;

    private void OnAnyDamaged(Damageable body, DamageInfo hit, DamageResult result)
    {
        // A boss never goes over: its fight is the point. Nor a friend's copy of the host's creature (co-op): its fall is the host's game's
        // to decide, and the copy here, held at 1 by its floor, froze at the bottom while the host's creature fought on (review 2026-11-27).
        if (plan == null || terrain == null || body == null || !body.IsAlive || falling.Contains(body) || !body.TryGetComponent(out MonsterAI brain)
            || brain.IsRemote || body.TryGetComponent(out BossEncounter _) || !body.TryGetComponent(out HitReaction reaction) || reaction.KnockbackScale <= 0f
            || CombatFeelSettings.Current == null)
        {
            return;
        }

        CombatFeelSettings feel = CombatFeelSettings.Current;
        // By the same rule as the shove itself (HitReaction.KnockbackFor): a hammer's heavy sends a body over, a dagger's stab hardly.
        float push = HitReaction.KnockbackFor(hit, result, body) * reaction.KnockbackScale + EdgeGive;
        Vector3 direction = Vector3.ProjectOnPlane(hit.Direction, Vector3.up);
        if (direction.sqrMagnitude < 0.01f && hit.Source != null)
        {
            direction = Vector3.ProjectOnPlane(body.transform.position - hit.Source.transform.position, Vector3.up);
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        direction.Normalize();
        Vector3 origin = terrain.transform.position;
        for (float reach = PushProbe; reach <= push; reach += PushProbe)
        {
            Vector3 at = body.transform.position + direction * reach;
            if (plan.OverRavine(new Vector2(at.x - origin.x, at.z - origin.z), out _, out float rim, RaidLayoutRavine.RagReach)
                && terrain.SampleHeight(at) < rim - PastTheLip)
            {
                StartCoroutine(Topple(body, direction, hit.Source, rim + origin.y));
                return;
            }
        }
    }

    // Off the navmesh and over the edge, falling until well below the rim, where the fall kills it.
    private IEnumerator Topple(Damageable body, Vector3 direction, Combatant pusher, float rim)
    {
        falling.Add(body);
        // Its brain and feet held while it falls (CreatureHold): let go if it is still alive at the bottom.
        CreatureHold.Hold(body, this, CreatureHold.Part.Brain | CreatureHold.Part.Feet);
        Vector3 velocity = direction * FallSpeed + Vector3.up * FallLift;
        while (body != null && body.IsAlive)
        {
            velocity += Physics.gravity * Time.deltaTime;
            body.transform.position += velocity * Time.deltaTime;
            if (body.transform.position.y < rim - FallDepth)
            {
                // The end of it, past any floor (Damageable.Finish): a floor that would have it rise again held a Draug at 1, and it stood
                // at the bottom with its brain off for good. A plain blow, not a Finisher, which read as the player's brutal crit (a flash,
                // a camera kick, the pack at the rim wavering) at the bottom of a gorge (review 2026-11-27).
                body.Finish(new DamageInfo(pusher, null, body.MaxHealth, DamageType.Physical, false, body.transform.position, Vector3.down,
                    0f, HitTechnique.None));
                break;
            }

            yield return null;
        }

        // The body lands on the floor below.
        while (body != null)
        {
            float ground = terrain.SampleHeight(body.transform.position) + terrain.transform.position.y;
            if (body.transform.position.y <= ground)
            {
                body.transform.position = new Vector3(body.transform.position.x, ground, body.transform.position.z);
                break;
            }

            velocity += Physics.gravity * Time.deltaTime;
            body.transform.position += velocity * Time.deltaTime;
            yield return null;
        }

        falling.Remove(body);
        // Still alive (a floor nothing here could end): it gets up where it landed, its feet put on the nearest navmesh.
        if (body != null && body.IsAlive)
        {
            CreatureHold.Release(body, this);
        }
    }

    private void Update()
    {
        Player player = Player.Current;
        if (plan == null || terrain == null || player == null)
        {
            return;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 at = player.transform.position;
        var point = new Vector2(at.x - origin.x, at.z - origin.z);
        float ground = terrain.SampleHeight(at);
        float fallSpeed = Time.deltaTime > 0f && hasLast ? (lastHeight - at.y) / Time.deltaTime : 0f;
        lastHeight = at.y;
        hasLast = true;
        if (!HasFallen(plan, point, at.y - origin.y, ground, fallSpeed))
        {
            fallingFor = 0f;
            Remember(point, at, ground + origin.y);
            return;
        }

        if (!player.TryGetComponent(out Damageable body) || !body.IsAlive)
        {
            return;
        }

        fallingFor += Time.deltaTime;
        if (!body.IsInvulnerable)
        {
            if (fallingFor <= Time.deltaTime + 0.0001f)
            {
                GameMessages.Post("You fell into the ravine.", MessageKind.Warning);
            }

            body.ApplyDamage(new DamageInfo(null, null, body.MaxHealth * 100f, DamageType.Physical, false, at, Vector3.down));
            return;
        }

        if (fallingFor >= RescueAfter && knowsSafe && player.TryGetComponent(out PlayerPersistence persistence))
        {
            persistence.MoveTo(new PlayerPose(lastSafe, player.transform.eulerAngles.y));
            GameMessages.Post("Pulled back from the ravine (invincible).", MessageKind.Warning);
            fallingFor = 0f;
        }
    }

    // Standing on the ground and clear of every ravine: where an invincible player is put back to.
    private void Remember(Vector2 point, Vector3 at, float groundHeight)
    {
        if (Time.time < nextSafe || Mathf.Abs(at.y - groundHeight) > OnGround || plan.OverRavine(point, out _, out _, RaidLayoutRavine.RagReach))
        {
            return;
        }

        nextSafe = Time.time + SafeEvery;
        lastSafe = at;
        knowsSafe = true;
    }
}
