using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One lobbed blow (Monster Attack Kind › Lob): aimed at the ground where its foe stands, not at the foe. A ring on the
/// ground fills while the thrown thing (the action's projectile prefab, only to be seen) arcs over; when it lands, the
/// attack strikes everything in the ring (its Range is the ring's radius) and leaves the action's hazard there. A boulder
/// from a stone troll, roots bursting from the ground under the Heartroot's foe. Keep moving and it misses; it reaches a
/// bow-user standing off.
/// </summary>
public sealed class LobStrike : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Combatant source;
    private MonsterAttack action;
    private LayerMask layers;
    private Vector3 from;
    private float startedAt;
    private Transform fill;
    private Renderer fillRenderer;
    private MaterialPropertyBlock block;
    private Transform thrown;

    /// <summary>Throws one: from <paramref name="origin"/>, landing at <paramref name="point"/> after the action's Lob Seconds. The lob in
    /// flight (null when it can't be thrown), for a thrower that may call it off.</summary>
    public static LobStrike Throw(Combatant by, MonsterAttack lob, Vector3 origin, Vector3 point, LayerMask targetLayers)
    {
        if (by == null || lob == null || lob.attack == null)
        {
            return null;
        }

        if (NavMesh.SamplePosition(point, out NavMeshHit ground, 4f, NavMesh.AllAreas))
        {
            point = ground.position;
        }

        var made = new GameObject($"{by.name} Lob");
        made.transform.position = point;
        LobStrike strike = made.AddComponent<LobStrike>();
        strike.source = by;
        strike.action = lob;
        strike.layers = targetLayers;
        strike.from = origin;
        strike.Build();
        return strike;
    }

    private void Build()
    {
        startedAt = Time.time;
        CombatFeelSettings feel = CombatFeelSettings.Current;
        float radius = action.attack.Range;
        if (GroundMarks.MaterialFor(feel) != null)
        {
            Color fillColor = action.attack.Unblockable ? feel.UnblockableColor : feel.TelegraphFillColor;
            Part("Area", feel, feel.TelegraphAreaColor, 0.06f, radius, GroundMarks.Pattern.Solid, 0.07f, 0.5f, 0f);
            fillRenderer = Part("Fill", feel, fillColor, 0.07f, radius,
                action.attack.Unblockable ? GroundMarks.Pattern.Hatched : GroundMarks.Pattern.Solid, 0f, 0.25f, 1f);
            fillRenderer.sortingOrder = 1;
            fill = fillRenderer.transform;
            block = new MaterialPropertyBlock();
        }

        if (action.projectilePrefab != null)
        {
            GameObject flying = Instantiate(action.projectilePrefab, from, Quaternion.identity);
            // Only seen: the landing does the harm.
            foreach (MonsterProjectile flight in flying.GetComponentsInChildren<MonsterProjectile>())
            {
                flight.enabled = false;
            }

            foreach (Collider collider in flying.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            thrown = flying.transform;
        }
    }

    private Renderer Part(string partName, CombatFeelSettings feel, Color color, float height, float radius, GroundMarks.Pattern pattern,
        float rim, float gradient, float front)
    {
        var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(transform, false);
        part.transform.localPosition = Vector3.up * height;
        part.transform.localScale = GroundMarks.Scale(radius);
        part.GetComponent<MeshFilter>().sharedMesh = GroundMarks.FanFor(feel, 360f);
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GroundMarks.MaterialFor(feel);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var colorBlock = new MaterialPropertyBlock();
        GroundMarks.Shape(colorBlock, 360f, 0f, rim, pattern, gradient, front);
        GroundMarks.Ground(colorBlock, transform.position);
        colorBlock.SetColor(BaseColorId, color);
        colorBlock.SetColor(ColorId, color);
        renderer.SetPropertyBlock(colorBlock);
        return renderer;
    }

    private void Update()
    {
        float through = Mathf.Clamp01((Time.time - startedAt) / action.lobSeconds);
        if (fill != null)
        {
            float radius = action.attack.Range;
            fill.localScale = new Vector3(radius * through, 1f, radius * through);
        }

        if (thrown != null)
        {
            // A high arc: up to half the distance again, highest midway.
            Vector3 flat = Vector3.Lerp(from, transform.position, through);
            float height = Mathf.Max(3f, Vector3.Distance(from, transform.position) * 0.5f);
            thrown.position = flat + Vector3.up * (height * 4f * through * (1f - through));
            thrown.Rotate(Vector3.right, 360f * Time.deltaTime, Space.Self);
        }

        if (through >= 1f)
        {
            Land();
        }
    }

    private void Land()
    {
        if (source != null && source.isActiveAndEnabled)
        {
            Vector3 toward = transform.position - from;
            toward.y = 0f;
            AttackResolver.Execute(source, action.attack, action.damage, transform.position, toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward, layers);
            GroundImpacts.Strike(action.attack, source, transform.position, toward, false, action.damage);
            if (action.hazardPrefab != null)
            {
                GroundHazard.Spawn(action.hazardPrefab, transform.position, source);
            }
        }

        AudioManager.Play(action.attack.HitSound, transform.position);
        GameObject dust = CombatFeelSettings.Current.LandingEffect;
        if (dust != null)
        {
            PrefabPool.Spawn(dust, transform.position, Quaternion.identity);
        }

        if (thrown != null)
        {
            Destroy(thrown.gameObject);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (thrown != null)
        {
            Destroy(thrown.gameObject);
        }
    }
}
