using UnityEngine;

/// <summary>
/// A loosed arrow: it flies on a falling arc, swept each frame so it can't skip through thin things, and turns along its
/// path. A hostile target takes the hit (DamageCalculator with the bow's shot attack and the arrow's damage); scenery
/// keeps the arrow stuck in it for a while. Either way the strike is heard (WorldNoise), so a stray shot can lure
/// monsters. Copies come from and return to <see cref="PrefabPool"/>, so Launch resets everything a flight uses.
/// </summary>
public sealed class ArrowProjectile : MonoBehaviour
{
    private const float MaxFlightSeconds = 6f;
    private const float Radius = 0.04f;
    // How far the tip sinks into what it strikes.
    private const float Embed = 0.12f;

    [Tooltip("Seconds an arrow stays stuck in the ground or a tree.")]
    [SerializeField, Min(0f)] private float stuckSeconds = 12f;
    [Tooltip("Shown only while flying (a trail or glint). Optional.")]
    [SerializeField] private GameObject flightOnly;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private Combatant owner;
    private AttackDefinition attack;
    private ArrowDefinition arrow;
    private float damage;
    private Vector3 velocity;
    private float gravity;
    private float flightEnds;
    private float releaseAt = -1f;
    private float tipOffset = -1f;

    /// <summary>True while in the air (false once stuck).</summary>
    public bool IsFlying => releaseAt < 0f;

    public Vector3 Velocity => velocity;

    /// <summary>Starts a flight from where the copy stands.</summary>
    public void Launch(Combatant source, AttackDefinition shot, ArrowDefinition definition, float shotDamage, Vector3 launchVelocity)
    {
        owner = source;
        attack = shot;
        arrow = definition;
        damage = shotDamage;
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
    }

    private void Update()
    {
        if (!IsFlying)
        {
            if (Time.time >= releaseAt)
            {
                PrefabPool.Release(gameObject);
            }

            return;
        }

        if (owner == null || Time.time >= flightEnds)
        {
            PrefabPool.Release(gameObject);
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
        if (victim != null && victim.IsAlive && owner != null && owner.Faction.WillAttack(victim.Faction) && attack != null)
        {
            DamageInfo info = DamageCalculator.Build(owner, attack, damage, victim.Category, hit.point, direction, Random.value);
            DamageResult result = victim.ApplyDamage(info);
            owner.ReportDamageDealt(victim, info, result);
        }

        if (arrow != null)
        {
            AudioManager.Play(arrow.HitSound, hit.point);
            WorldNoise.Emit(hit.point, arrow.ImpactNoise);
        }

        // Creatures move and die: the arrow is spent. Scenery keeps it, tip buried, for a while.
        if (victim != null || hit.rigidbody != null || stuckSeconds <= 0f)
        {
            PrefabPool.Release(gameObject);
            return;
        }

        transform.SetPositionAndRotation(hit.point + direction * Embed - direction * TipOffset, Quaternion.LookRotation(direction));
        releaseAt = Time.time + stuckSeconds;
        velocity = Vector3.zero;
        if (flightOnly != null)
        {
            flightOnly.SetActive(false);
        }
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
