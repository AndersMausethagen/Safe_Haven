using UnityEngine;

/// <summary>
/// A loosed arrow: it flies on a falling arc, swept each frame so it can't skip through thin things, and turns along its
/// path. A hostile target takes the hit (DamageCalculator with the bow's shot attack and the arrow's damage; a fire arrow
/// also sets it burning) and keeps the arrow in its body for a while, going where that part of the body goes, into a corpse too,
/// and when that body dies some of them (the arrow's Recover Chance) go back in the bag with the rest of what it leaves; scenery
/// keeps the arrow stuck in it for a while, and as many of those can be picked up again by walking over them. Either way the strike is heard (WorldNoise), so a stray shot can lure monsters. A trail shows its flight from
/// the high camera, gold for a perfect release (Combat Feel › Arrows). Copies come from and return to <see cref="PrefabPool"/>, so
/// Launch resets everything a flight uses.
/// </summary>
public sealed class ArrowProjectile : MonoBehaviour
{
    private const float MaxFlightSeconds = 6f;
    private const float Radius = 0.04f;
    // How far the tip sinks into what it strikes.
    private const float Embed = 0.12f;
    // Walking this close to a recoverable arrow picks it up.
    private const float PickupRange = 1.6f;
    private const float RecoverableSeconds = 90f;

    [Tooltip("Seconds an arrow stays stuck in the ground or a tree.")]
    [SerializeField, Min(0f)] private float stuckSeconds = 12f;
    [Tooltip("Shown only while flying (a trail or glint). Optional.")]
    [SerializeField] private GameObject flightOnly;

    // How much further into a body the tip goes than into a tree: it has to read as buried from above.
    private const float BodyEmbed = 0.22f;
    private static readonly HumanBodyBones[] BodyBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
    };

    private TrailRenderer trail;
    private Vector3 restScale;
    private Damageable host;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private Combatant owner;
    private AttackDefinition attack;
    private ItemDefinition item;
    private ArrowDefinition arrow;
    private float damage;
    private HitTechnique technique;
    private bool critical;
    private bool canBeRecovered;
    private Vector3 velocity;
    private float gravity;
    private float flightEnds;
    private float releaseAt = -1f;
    private float tipOffset = -1f;

    /// <summary>True while in the air (false once stuck).</summary>
    public bool IsFlying => releaseAt < 0f;

    public Vector3 Velocity => velocity;

    /// <summary>True while stuck and waiting to be picked up again.</summary>
    public bool Recoverable { get; private set; }

    // Made here rather than in each arrow's prefab: every arrow flies the same.
    private void StartTrail(bool perfect)
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (feel.ArrowTrailSeconds <= 0f || feel.TelegraphMaterial == null)
        {
            return;
        }

        if (trail == null)
        {
            var holder = new GameObject("Arrow Trail");
            holder.transform.SetParent(transform, false);
            trail = holder.AddComponent<TrailRenderer>();
            trail.sharedMaterial = feel.TelegraphMaterial;
            trail.widthMultiplier = feel.ArrowTrailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.minVertexDistance = 0.1f;
        }

        Color color = perfect ? feel.ArrowTrailPerfectColor : feel.ArrowTrailColor;
        trail.time = feel.ArrowTrailSeconds * (perfect ? 1.5f : 1f);
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.Clear();
        trail.emitting = true;
    }

    private void StopTrail()
    {
        if (trail != null)
        {
            trail.emitting = false;
        }
    }

    // Stuck in a body: when that dies, the arrow may be recovered with what it leaves.
    private void AwaitDeathOf(IDamageable victim)
    {
        if (!canBeRecovered || item == null || arrow == null || !(victim is Damageable body))
        {
            return;
        }

        if (!body.IsAlive)
        {
            Recover();
            return;
        }

        host = body;
        host.Died += OnHostDied;
    }

    private void OnHostDied(Damageable body, DamageInfo killingBlow)
    {
        StopAwaitingHost();
        Recover();
    }

    private void StopAwaitingHost()
    {
        if (host != null)
        {
            host.Died -= OnHostDied;
        }

        host = null;
    }

    private void Recover()
    {
        Player player = Player.Current;
        if (player != null && player.Bag != null && Random.value < arrow.RecoverChance)
        {
            player.Bag.Give(item, 1);
        }
    }

    private void OnDestroy() => StopAwaitingHost();

    private void ReturnToPool()
    {
        StopAwaitingHost();
        // Out of whatever body it was stuck in, or the next flight would start from there.
        transform.SetParent(null, true);
        transform.localScale = restScale != Vector3.zero ? restScale : transform.localScale;
        PrefabPool.Release(gameObject);
    }

    /// <summary>Starts a flight from where the copy stands. <paramref name="arrowItem"/>'s Arrow sets the drop, burning and
    /// recovery; <paramref name="forceCritical"/> makes the hit critical (a perfect release).</summary>
    public void Launch(Combatant source, AttackDefinition shot, ItemDefinition arrowItem, float shotDamage, Vector3 launchVelocity,
        HitTechnique hitTechnique = HitTechnique.None, bool forceCritical = false, bool recoverable = false)
    {
        restScale = restScale != Vector3.zero ? restScale : transform.localScale;
        owner = source;
        attack = shot;
        item = arrowItem;
        ArrowDefinition definition = arrowItem != null ? arrowItem.Arrow : null;
        arrow = definition;
        damage = shotDamage;
        technique = hitTechnique;
        critical = forceCritical;
        canBeRecovered = recoverable;
        Recoverable = false;
        velocity = launchVelocity;
        gravity = Physics.gravity.magnitude * (definition != null ? definition.GravityScale : 1f);
        flightEnds = Time.time + MaxFlightSeconds;
        releaseAt = -1f;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        if (flightOnly != null)
        {
            flightOnly.SetActive(true);
        }

        StartTrail(forceCritical);
    }

    private void Update()
    {
        if (!IsFlying)
        {
            if (Time.time >= releaseAt || Recoverable && TryPickUp())
            {
                ReturnToPool();
            }

            return;
        }

        if (owner == null || Time.time >= flightEnds)
        {
            ReturnToPool();
            return;
        }

        Fly(Time.deltaTime);
    }

    /// <summary>Moves the arrow one step and resolves what it strikes (public for tests).</summary>
    public void Fly(float deltaTime)
    {
        Vector3 start = transform.position;
        velocity += Vector3.down * (gravity * deltaTime);
        Vector3 step = velocity * deltaTime;
        float length = step.magnitude;
        if (length < 0.0001f)
        {
            return;
        }

        Vector3 direction = step / length;
        int count = Physics.SphereCastNonAlloc(start, Radius, direction, hits, length, ~0, QueryTriggerInteraction.Ignore);
        int nearest = -1;
        for (int i = 0; i < count; i++)
        {
            Transform hit = hits[i].collider.transform;
            if (hit.IsChildOf(transform) || owner != null && hit.IsChildOf(owner.transform) || hits[i].distance <= 0f && hits[i].point == Vector3.zero)
            {
                continue;
            }

            // A friend in the way doesn't stop it: an arrow that vanished on a companion's back was a shot wasted for nothing.
            if (owner != null && hits[i].collider.GetComponentInParent<Damageable>() is Damageable body && body.IsAlive
                && body.Category == TargetCategory.Creature && !owner.Faction.WillAttack(body.Faction))
            {
                continue;
            }

            if (nearest < 0 || hits[i].distance < hits[nearest].distance)
            {
                nearest = i;
            }
        }

        if (nearest < 0)
        {
            transform.SetPositionAndRotation(start + step, Quaternion.LookRotation(direction));
            return;
        }

        Strike(hits[nearest], direction);
    }

    private void Strike(RaycastHit hit, Vector3 direction)
    {
        IDamageable victim = hit.collider.GetComponentInParent<IDamageable>();
        bool struckBody = false;
        Transform bodyPart = null;
        if (victim != null && victim.IsAlive && owner != null && owner.Faction.WillAttack(victim.Faction) && attack != null)
        {
            // Found before the blow: if it kills, the body goes off to become a corpse and takes this part with it.
            struckBody = victim.Category == TargetCategory.Creature;
            bodyPart = struckBody ? NearestBodyPart(victim, hit.point) : null;

            DamageInfo info = DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, critical ? DamageCalculator.SureCrit : Random.value);
            if (technique != HitTechnique.None || critical)
            {
                info = info.Scaled(1f, 1f, technique, critical && attack.CanCrit);
            }

            info = info.WithAmount(info.Amount * CombatFeelSettings.HighGroundFor(owner.transform.position, victim is Component struck ? struck.transform.position : hit.point)
                * PlayerProficiencies.AmbushScale(owner.Stats, victim));

            DamageResult result = victim.ApplyDamage(info);
            owner.ReportDamageDealt(victim, info, result);
            if (arrow != null && arrow.Burns && victim is Damageable body && victim.Category == TargetCategory.Creature)
            {
                Burning.Apply(body, owner, arrow.BurnDamagePerSecond, arrow.BurnSeconds);
            }
        }

        if (arrow != null)
        {
            // A body has its own sound, played with the blow (CombatFeedback); this is the sound of wood and earth.
            if (!struckBody)
            {
                AudioManager.Play(arrow.HitSound, hit.point);
            }

            WorldNoise.Emit(hit.point, arrow.ImpactNoise);
        }

        // In a body it goes where that part goes, into the corpse too, for a while.
        if (bodyPart != null && stuckSeconds > 0f)
        {
            StopTrail();
            transform.SetPositionAndRotation(hit.point + direction * BodyEmbed - direction * TipOffset, Quaternion.LookRotation(direction));
            transform.SetParent(bodyPart, true);
            AwaitDeathOf(victim);
            Recoverable = false;
            releaseAt = Time.time + stuckSeconds;
            velocity = Vector3.zero;
            if (flightOnly != null)
            {
                flightOnly.SetActive(false);
            }

            return;
        }

        // Scenery keeps it, tip buried, for a while.
        if (victim != null || hit.rigidbody != null || stuckSeconds <= 0f)
        {
            ReturnToPool();
            return;
        }

        StopTrail();
        transform.SetPositionAndRotation(hit.point + direction * Embed - direction * TipOffset, Quaternion.LookRotation(direction));
        // The shooter decides: the player's arrows can be picked up again; a companion's never run out.
        Recoverable = canBeRecovered && item != null && arrow != null && Random.value < arrow.RecoverChance;
        releaseAt = Time.time + (Recoverable ? Mathf.Max(stuckSeconds, RecoverableSeconds) : stuckSeconds);
        velocity = Vector3.zero;
        if (flightOnly != null)
        {
            flightOnly.SetActive(false);
        }
    }

    // The bone or rigid part of the body nearest the point, or null for something with no body to hold an arrow.
    private static Transform NearestBodyPart(IDamageable victim, Vector3 point)
    {
        if (!(victim is Component target))
        {
            return null;
        }

        Transform nearest = null;
        float best = float.MaxValue;
        void Consider(Transform part)
        {
            if (part != null && (part.position - point).sqrMagnitude < best)
            {
                best = (part.position - point).sqrMagnitude;
                nearest = part;
            }
        }

        Animator animator = target.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            foreach (HumanBodyBones bone in BodyBones)
            {
                Consider(animator.GetBoneTransform(bone));
            }

            return nearest;
        }

        foreach (MeshRenderer part in target.GetComponentsInChildren<MeshRenderer>())
        {
            if (!OutlineCopies.IsCopy(part) && part.GetComponentInParent<ArrowProjectile>() == null)
            {
                Consider(part.transform);
            }
        }

        return nearest;
    }

    private bool TryPickUp()
    {
        Player player = Player.Current;
        if (player == null || player.Bag == null || (player.transform.position - transform.position).sqrMagnitude > PickupRange * PickupRange)
        {
            return false;
        }

        return player.Bag.Give(item, 1) > 0;
    }

    // The tip sits this far ahead of the arrow's pivot (+Z), measured from its meshes once.
    private float TipOffset
    {
        get
        {
            if (tipOffset < 0f)
            {
                tipOffset = 0f;
                foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                    {
                        continue;
                    }

                    Bounds bounds = filter.sharedMesh.bounds;
                    Matrix4x4 toLocal = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                            (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                        tipOffset = Mathf.Max(tipOffset, toLocal.MultiplyPoint3x4(point).z);
                    }
                }
            }

            return tipOffset * transform.lossyScale.z;
        }
    }
}
