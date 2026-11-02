using UnityEngine;

/// <summary>
/// Blood from a blow: droplets thrown along the way the blow travelled, a mist where it struck, a little cast back at the attacker,
/// and stains where the droplets come down. How far and how much follow the blow's force (<see cref="StrikeRules.HitForce"/>) and its
/// direction (<see cref="StrikeRules.SprayDirection"/>), and each particle's velocity is worked out here rather than left to a
/// particle shape, so the spray really does go the way the blow went. One copy per scene with three world-space particle systems (the
/// prefab Build Combat Effects makes), so a fight makes no garbage and stains don't hold effect copies out of a pool.
/// <para>
/// The colour comes from what bleeds (Content Editor › Monsters › Blood Color); Options › Gameplay › Blood turns it all off.
/// Amounts are in Combat Feel › Blood.
/// </para>
/// </summary>
public sealed class BloodEffects : MonoBehaviour
{
    private const float GroundRayHeight = 2.5f;
    private const float GroundRayLength = 6f;
    private const float StainLift = 0.06f;
    private const int PoolLobes = 4;

    [SerializeField] private ParticleSystem droplets;
    [SerializeField] private ParticleSystem mist;
    [SerializeField] private ParticleSystem stains;
    [Tooltip("Pools that spread out under what has died. The system's Size Over Lifetime is the spreading.")]
    [SerializeField] private ParticleSystem pools;

    private static BloodEffects instance;
    private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

    /// <summary>The scene's copy, made from the prefab in Combat Feel on first use. Null when there is no prefab.</summary>
    private static BloodEffects Instance
    {
        get
        {
            if (instance == null)
            {
                BloodEffects prefab = CombatFeelSettings.Current.BloodEffects;
                instance = prefab != null ? Instantiate(prefab) : null;
            }

            return instance;
        }
    }

    /// <summary>Sprays from <paramref name="point"/> along <paramref name="direction"/> (normalised), <paramref name="force"/> 0-1.</summary>
    public static void Spray(Vector3 point, Vector3 direction, float force, Color color)
    {
        if (!GameOptions.Blood.On)
        {
            return;
        }

        BloodEffects effects = Instance;
        if (effects != null)
        {
            effects.Emit(point, direction, Mathf.Clamp01(force), color);
        }
    }

    /// <summary>A wide splash on the ground under what has just died.</summary>
    public static void Pool(Vector3 point, float size, Color color)
    {
        if (!GameOptions.Blood.On)
        {
            return;
        }

        BloodEffects effects = Instance;
        if (effects != null && effects.TryFindGround(point, out Vector3 ground, out Vector3 normal))
        {
            effects.EmitStain(ground, normal, size, color);
        }
    }

    /// <summary>One pulse from an open wound (<see cref="DeathRagdoll"/>): a jet of droplets along <paramref name="direction"/>,
    /// <paramref name="strength"/> 0-1, and the stains where it comes down around the body.</summary>
    public static void Spurt(Vector3 point, Vector3 direction, float strength, Color color)
    {
        if (!GameOptions.Blood.On)
        {
            return;
        }

        BloodEffects effects = Instance;
        if (effects != null)
        {
            effects.EmitSpurt(point, direction, Mathf.Clamp01(strength), color);
        }
    }

    /// <summary>Starts a pool of blood spreading on the ground under <paramref name="point"/>, up to about <paramref name="size"/> metres
    /// across: a wide middle with smaller ones run out from it, so its edge isn't one round shape.</summary>
    public static void PoolUnder(Vector3 point, float size, Color color)
    {
        if (!GameOptions.Blood.On)
        {
            return;
        }

        BloodEffects effects = Instance;
        if (effects == null || effects.pools == null)
        {
            return;
        }

        effects.EmitPool(point, size, color);
        for (int i = 0; i < PoolLobes; i++)
        {
            Vector2 out2 = Random.insideUnitCircle.normalized * (size * Random.Range(0.22f, 0.42f));
            effects.EmitPool(point + new Vector3(out2.x, 0f, out2.y), size * Random.Range(0.4f, 0.65f), color);
        }
    }

    private void EmitPool(Vector3 point, float size, Color color)
    {
        if (!TryFindGround(point, out Vector3 ground, out Vector3 normal))
        {
            return;
        }

        Quaternion lying = Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Quaternion.FromToRotation(Vector3.forward, normal);
        var pool = new ParticleSystem.EmitParams
        {
            position = ground + normal * (StainLift + 0.01f),
            rotation3D = lying.eulerAngles,
            startSize = size,
            startLifetime = CombatFeelSettings.Current.BloodPoolSeconds,
            startColor = Shade(color, Random.Range(0.62f, 0.8f)),
            applyShapeToPosition = false,
        };
        pools.Emit(pool, 1);
    }

    private void EmitSpurt(Vector3 point, Vector3 direction, float strength, Color color)
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        float speed = Mathf.Lerp(1.5f, feel.BloodSpurtSpeed, strength);
        int count = Mathf.RoundToInt(Mathf.Lerp(6f, 26f, strength));
        for (int i = 0; i < count; i++)
        {
            var drop = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = Scatter(direction, 22f) * (speed * Random.Range(0.45f, 1f)),
                startSize = Random.Range(0.03f, 0.075f),
                startLifetime = Random.Range(0.35f, 0.85f),
                startColor = Shade(color, Random.Range(0.75f, 1.15f)),
                applyShapeToPosition = false,
            };
            droplets.Emit(drop, 1);
        }

        int marks = Mathf.RoundToInt(Mathf.Lerp(2f, 8f, strength));
        Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
        flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
        for (int i = 0; i < marks; i++)
        {
            // Along the jet: it thins the further it reaches.
            float through = Random.value;
            Vector3 way = Quaternion.AngleAxis(Random.Range(-24f, 24f), Vector3.up) * flat;
            Vector3 landing = point + way * Mathf.Lerp(0.15f, Mathf.Lerp(0.6f, feel.BloodSpurtReach, strength), through);
            if (TryFindGround(landing, out Vector3 ground, out Vector3 normal))
            {
                EmitStain(ground, normal, Mathf.Lerp(0.34f, 0.1f, through) * Mathf.Lerp(0.7f, 1.2f, strength), color);
            }
        }
    }

    private void Emit(Vector3 point, Vector3 direction, float force, Color color)
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        float speed = Mathf.Lerp(feel.BloodSpeedMin, feel.BloodSpeedMax, force);
        float spread = Mathf.Lerp(42f, 16f, force);
        int count = Mathf.RoundToInt(Mathf.Lerp(feel.BloodDropletsMin, feel.BloodDropletsMax, force));
        Vector3 back = Vector3.ProjectOnPlane(-direction, Vector3.up).normalized + Vector3.up * 0.4f;
        for (int i = 0; i < count; i++)
        {
            // One in six is cast back the way the blade came; the rest go with the blow.
            bool castBack = i % 6 == 5;
            Vector3 way = Scatter(castBack ? back.normalized : direction, castBack ? 30f : spread);
            float pace = speed * Random.Range(0.3f, 1f) * (castBack ? 0.4f : 1f);
            var drop = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = way * pace,
                // Larger than real drops: they are seen from a camera high above the fight.
                startSize = Random.Range(0.035f, 0.085f) * Mathf.Lerp(0.9f, 1.25f, force),
                startLifetime = Random.Range(0.35f, 0.8f),
                startColor = Shade(color, Random.Range(0.75f, 1.15f)),
                applyShapeToPosition = false,
            };
            droplets.Emit(drop, 1);
        }

        int puffs = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, force));
        for (int i = 0; i < puffs; i++)
        {
            Color haze = color;
            haze.a = Random.Range(0.45f, 0.75f);
            var puff = new ParticleSystem.EmitParams
            {
                position = point + Random.insideUnitSphere * 0.06f,
                velocity = Scatter(direction, spread * 1.3f) * speed * Random.Range(0.08f, 0.28f),
                startSize = Random.Range(0.18f, 0.42f) * Mathf.Lerp(0.8f, 1.7f, force),
                startLifetime = Random.Range(0.2f, 0.42f),
                startColor = haze,
                applyShapeToPosition = false,
            };
            mist.Emit(puff, 1);
        }

        int marks = Mathf.RoundToInt(Mathf.Lerp(feel.BloodStainsMin, feel.BloodStainsMax, force));
        for (int i = 0; i < marks; i++)
        {
            // Where a drop of about this pace comes down, roughly: nearer ones are thicker.
            float through = Random.value;
            Vector3 way = Vector3.ProjectOnPlane(Scatter(direction, spread), Vector3.up).normalized;
            Vector3 landing = point + way * (Mathf.Lerp(0.15f, feel.BloodReach, through * through) * Mathf.Lerp(0.5f, 1f, force));
            if (TryFindGround(landing, out Vector3 ground, out Vector3 normal))
            {
                EmitStain(ground, normal, Mathf.Lerp(0.5f, 0.14f, through) * Mathf.Lerp(0.7f, 1.3f, force), color);
            }
        }
    }

    private void EmitStain(Vector3 ground, Vector3 normal, float size, Color color)
    {
        // The quad lies on the ground, turned at random so stains don't all look alike.
        Quaternion lying = Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * Quaternion.FromToRotation(Vector3.forward, normal);
        var stain = new ParticleSystem.EmitParams
        {
            position = ground + normal * StainLift,
            rotation3D = lying.eulerAngles,
            startSize = size,
            startLifetime = CombatFeelSettings.Current.BloodStainSeconds,
            startColor = Shade(color, Random.Range(0.55f, 0.85f)),
            applyShapeToPosition = false,
        };
        stains.Emit(stain, 1);
    }

    // The nearest solid surface under the point that isn't something alive.
    private bool TryFindGround(Vector3 point, out Vector3 ground, out Vector3 normal)
    {
        ground = point;
        normal = Vector3.up;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * GroundRayHeight, Vector3.down, GroundHits, GroundRayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.distance < nearest && hit.collider.GetComponentInParent<IDamageable>() == null)
            {
                nearest = hit.distance;
                ground = hit.point;
                normal = hit.normal;
                found = true;
            }
        }

        return found;
    }

    private static Vector3 Scatter(Vector3 direction, float degrees)
    {
        Vector2 offset = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 up = Vector3.Cross(side, direction);
        return (direction + side * offset.x + up * offset.y).normalized;
    }

    private static Color Shade(Color color, float by) => new Color(color.r * by, color.g * by, color.b * by, color.a);

    /// <summary>Wires the particle systems (editor setup).</summary>
    public void Configure(ParticleSystem drops, ParticleSystem haze, ParticleSystem marks, ParticleSystem spreading)
    {
        droplets = drops;
        mist = haze;
        stains = marks;
        pools = spreading;
    }

    /// <summary>False until Build Blood and Markers has given the prefab its pool system.</summary>
    public bool HasPools => pools != null;
}
