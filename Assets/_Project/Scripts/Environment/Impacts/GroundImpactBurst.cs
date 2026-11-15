using UnityEngine;

/// <summary>
/// What a heavy blow throws up from the ground: a ring of dust rolling out low, clods and pebbles flung up that fall back
/// and bounce, grass bits where grass grows, and mud splashed onto wet ground (as splats). Every part takes the colour of the
/// ground it is thrown from (GroundImpacts reads it). On the root of the burst prefab, beside a PooledEffect, which returns
/// it to the pool; each part is a particle system that only emits when told.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundImpactBurst : MonoBehaviour
{
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem debris;
    [SerializeField] private ParticleSystem clippings;

    /// <summary>What the ground is like where it lands.</summary>
    public struct Ground
    {
        public Color Colour;
        public bool Grassy;
        public bool Wet;
        public bool Snowy;
    }

    /// <summary>Throws it all up: <paramref name="scale"/> sizes everything (1 for the blow's own body size),
    /// <paramref name="arc"/> degrees round <paramref name="forward"/> (360: all round).</summary>
    public void Throw(Ground ground, int dustCount, int debrisCount, int clippingCount, float radius, float scale, Vector3 forward, float arc)
    {
        forward.y = 0f;
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        Color dustColour = Color.Lerp(ground.Colour, Color.white, ground.Snowy ? 0.2f : 0.35f);
        dustColour.a = ground.Wet ? 0.25f : 0.55f;
        // Wet ground throws little dust; lying snow throws a great deal of powder and no stones.
        int dusts = Mathf.RoundToInt(dustCount * (ground.Wet ? 0.35f : ground.Snowy ? 1.6f : 1f));
        for (int i = 0; i < dusts; i++)
        {
            Vector3 out_ = Outward(forward, arc);
            var p = new ParticleSystem.EmitParams
            {
                position = transform.position + out_ * Random.Range(0.1f, 0.5f) * radius + Vector3.up * 0.2f,
                velocity = (out_ * Random.Range(2.5f, 5.5f) + Vector3.up * Random.Range(0.6f, 2f)) * Mathf.Sqrt(scale) * (radius / 2f + 0.5f),
                startSize = Random.Range(0.8f, 1.8f) * scale,
                startColor = dustColour,
                startLifetime = Random.Range(1.4f, 2.6f),
                rotation = Random.Range(0f, 360f),
            };
            Emit(dust, p);
        }

        if (!ground.Snowy)
        {
            Color stone = Color.Lerp(ground.Colour, Color.black, 0.3f);
            for (int i = 0; i < debrisCount; i++)
            {
                Vector3 out_ = Outward(forward, arc);
                var p = new ParticleSystem.EmitParams
                {
                    position = transform.position + out_ * Random.Range(0f, 0.4f) * radius + Vector3.up * 0.15f,
                    velocity = (out_ * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(3f, 7f)) * Mathf.Sqrt(scale),
                    startSize = Random.Range(0.05f, 0.16f) * Mathf.Sqrt(scale),
                    startColor = Color.Lerp(stone, Color.white, Random.Range(0f, 0.25f)),
                    startLifetime = Random.Range(1.6f, 2.4f),
                    rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                    angularVelocity3D = Random.insideUnitSphere * 540f,
                };
                Emit(debris, p);
            }
        }

        if (ground.Grassy && !ground.Snowy)
        {
            GroundImpactSettings settings = GroundImpactSettings.Current;
            for (int i = 0; i < clippingCount; i++)
            {
                Vector3 out_ = Outward(forward, arc);
                var p = new ParticleSystem.EmitParams
                {
                    position = transform.position + out_ * Random.Range(0.2f, 1f) * radius + Vector3.up * 0.25f,
                    velocity = out_ * Random.Range(1f, 3f) + Vector3.up * Random.Range(1.5f, 4f),
                    startSize = Random.Range(0.07f, 0.14f),
                    startColor = Color.Lerp(settings.Grass, Color.white, Random.Range(0f, 0.3f)),
                    startLifetime = Random.Range(1.8f, 3f),
                    rotation = Random.Range(0f, 360f),
                    angularVelocity = Random.Range(-360f, 360f),
                };
                Emit(clippings, p);
            }
        }

        if (ground.Wet && !ground.Snowy)
        {
            SplashMud(debrisCount, radius, scale, forward, arc);
        }
    }

    // Wet ground: mud splashed onto the ground round the blow, as splats (the blood splat atlas tinted mud brown, BloodDecals).
    // The clots used to fly as particles on the built-in sphere mesh: brown balls "like large soccer balls, not mud" (the
    // user, 2026-11-15). The prefab's Mud particle system is unused since.
    private void SplashMud(int debrisCount, float radius, float scale, Vector3 forward, float arc)
    {
        if (!BloodDecals.Available)
        {
            return;
        }

        GroundImpactSettings settings = GroundImpactSettings.Current;
        int splats = Mathf.Clamp(debrisCount / 2, 2, 6);
        for (int i = 0; i < splats; i++)
        {
            Vector3 out_ = Outward(forward, arc);
            Vector3 at = transform.position + out_ * Random.Range(0.2f, 1.1f) * Mathf.Max(radius, 0.5f);
            Vector3 normal = Vector3.up;
            // The ground under it, not whoever stands there.
            int count = Physics.RaycastNonAlloc(at + Vector3.up * MudRayHeight, Vector3.down, MudHits, MudRayHeight * 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int h = 0; h < count; h++)
            {
                if (MudHits[h].distance < nearest && MudHits[h].collider.GetComponentInParent<IDamageable>() == null)
                {
                    nearest = MudHits[h].distance;
                    at = MudHits[h].point;
                    normal = MudHits[h].normal;
                }
            }

            // One colour, so one tinted copy of the atlas (BloodDecals keeps one per colour).
            BloodDecals.Splat(at, normal, Random.Range(0.35f, 0.8f) * Mathf.Sqrt(scale), settings.Mud, MudSeconds);
        }
    }

    private const float MudRayHeight = 2f;
    private const float MudSeconds = 90f;
    private static readonly RaycastHit[] MudHits = new RaycastHit[8];

    private static void Emit(ParticleSystem system, ParticleSystem.EmitParams parameters)
    {
        if (system != null)
        {
            system.Emit(parameters, 1);
        }
    }

    // A way out from the middle on the flat, within the arc round forward.
    private static Vector3 Outward(Vector3 forward, float arc)
    {
        float turn = arc >= 359f ? Random.Range(0f, 360f) : Random.Range(-arc * 0.5f, arc * 0.5f);
        return Quaternion.Euler(0f, turn, 0f) * forward;
    }
}
