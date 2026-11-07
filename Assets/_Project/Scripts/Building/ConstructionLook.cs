using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a <see cref="Construction"/> looks: the model rising out of the ground (and a ruin leaning) as work is done, the
/// darkening of a ruin or a broken wall, the scaffold, each level's parts (a model's own "Tier N" children, or made-up
/// dressing: a stone plinth and lanterns, then banners and a flower box), the dust of hammer blows and the pop when work
/// is finished. Looks and sounds come from ConstructionSettings. Owned by the Construction; plain C#.
/// </summary>
public sealed class ConstructionLook
{
    private const string Dressing = "Dressing Level ";
    private const string TierPart = "Tier ";
    private const float PopSeconds = 0.45f;

    private readonly Construction owner;
    private readonly BuildingObject building;
    private readonly List<(Transform part, Vector3 position, Quaternion rotation)> model = new List<(Transform, Vector3, Quaternion)>();
    private readonly List<Renderer> modelRenderers = new List<Renderer>();
    private readonly Dictionary<int, Transform> tierParts = new Dictionary<int, Transform>();
    private readonly MaterialPropertyBlock shade = new MaterialPropertyBlock();
    private Transform scaffold;
    private ParticleSystem dust;
    private float popUntil = -1f;

    /// <summary>Notes the model as it stands, before the site, scaffold and dust are added (they don't rise with it).</summary>
    public ConstructionLook(Construction owner, BuildingObject building)
    {
        this.owner = owner;
        this.building = building;
        foreach (Transform child in Root)
        {
            model.Add((child, child.localPosition, child.localRotation));
        }

        foreach (Transform part in owner.GetComponentsInChildren<Transform>(true))
        {
            if (part.name.StartsWith(TierPart) && int.TryParse(part.name.Substring(TierPart.Length), out int tier))
            {
                tierParts[tier] = part;
            }
        }

        foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is ParticleSystemRenderer))
            {
                modelRenderers.Add(renderer);
            }
        }
    }

    private Transform Root => owner.transform;

    private float BoundsRadius => Mathf.Max(building.LocalBounds.extents.x, building.LocalBounds.extents.z) + 0.5f;

    /// <summary>The scaffold (hidden until work begins) and the dust the work raises.</summary>
    public void BuildSite()
    {
        scaffold = BuildScaffold(building.LocalBounds);
        scaffold.gameObject.SetActive(false);
        dust = CreateDust(Root);
    }

    public void ShowScaffold(bool show)
    {
        if (scaffold != null)
        {
            scaffold.gameObject.SetActive(show);
        }
    }

    /// <summary>A building going up rises out of the ground; a ruin also leans until it is repaired.</summary>
    public void ShowRise(bool rising, float share, bool ruined)
    {
        float height = Mathf.Max(0.5f, building.LocalBounds.size.y);
        float sink = rising ? (1f - share) * height * (ruined ? 0.4f : 0.85f) : 0f;
        Quaternion lean = rising && ruined ? Quaternion.Euler(Mathf.Lerp(7f, 0f, share), 0f, Mathf.Lerp(-4f, 0f, share)) : Quaternion.identity;
        foreach ((Transform part, Vector3 position, Quaternion rotation) in model)
        {
            if (part != null)
            {
                part.localPosition = position + Vector3.down * sink;
                part.localRotation = lean * rotation;
            }
        }

        if (rising)
        {
            Shade(Mathf.Lerp(ConstructionSettings.Current.RuinShade, 1f, share));
        }
    }

    /// <summary>Darkens the model (a ruin, a broken wall); 1 is its own colour.</summary>
    public void Shade(float brightness)
    {
        foreach (Renderer renderer in modelRenderers)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            if (brightness >= 0.999f)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            Material material = renderer.sharedMaterial;
            Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            shade.Clear();
            shade.SetColor("_BaseColor", new Color(colour.r * brightness, colour.g * brightness, colour.b * brightness, colour.a));
            renderer.SetPropertyBlock(shade);
        }
    }

    /// <summary>Shows each level's parts up to <paramref name="level"/>: the model's own tier parts, or dressing made
    /// for it (once the building stands, so a save's loading doesn't build it twice).</summary>
    public void ShowLevels(int level, int maxLevel, bool canDress)
    {
        for (int tier = 2; tier <= Mathf.Max(maxLevel, 3); tier++)
        {
            // A model made for its tiers shows them itself: the parts of each tier are switched on as it is reached.
            if (tierParts.TryGetValue(tier, out Transform authored))
            {
                authored.gameObject.SetActive(level >= tier);
                continue;
            }

            Transform dressing = Root.Find(Dressing + tier);
            if (level >= tier && dressing == null && canDress)
            {
                dressing = BuildDressing(tier);
            }

            if (dressing != null)
            {
                dressing.gameObject.SetActive(level >= tier);
            }
        }
    }

    /// <summary>A hammer blow: its sound, a nudge of the camera and dust off a side of the building.</summary>
    public void HammerBlow()
    {
        ConstructionSettings settings = ConstructionSettings.Current;
        AudioManager.Play(settings.HammerSound, Root.position);
        CameraShake.Add(settings.BlowShake);
        Bounds bounds = building.LocalBounds;
        Vector3 local = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(0.2f, Mathf.Max(0.4f, bounds.max.y * 0.6f)),
            Random.value < 0.5f ? bounds.min.z : bounds.max.z);
        Burst(Root.TransformPoint(local), 10, 0.3f);
    }

    /// <summary>Work finished: the building pops, dust rings it, the camera shakes and the finish sounds.</summary>
    public void Celebrate()
    {
        popUntil = Time.time + PopSeconds;
        Burst(Root.position + Vector3.up * 0.3f, 40, BoundsRadius);
        CameraShake.Add(ConstructionSettings.Current.FinishShake);
        AudioManager.Play(ConstructionSettings.Current.FinishSound, Root.position);
    }

    /// <summary>Every frame: the finish pop settles back to the building's size.</summary>
    public void Tick()
    {
        if (popUntil < 0f)
        {
            return;
        }

        float t = 1f - Mathf.Clamp01((popUntil - Time.time) / PopSeconds);
        Root.localScale = Vector3.one * Mathf.LerpUnclamped(ConstructionSettings.Current.FinishPop, 1f, EaseOutBack(t));
        if (t >= 1f)
        {
            popUntil = -1f;
            Root.localScale = Vector3.one;
        }
    }

    // Posts at the corners, two rings of beams, a brace and a plank walk at the front.
    private Transform BuildScaffold(Bounds bounds)
    {
        Material wood = ConstructionSettings.Current.ScaffoldMaterial;
        var root = new GameObject("Scaffold").transform;
        root.SetParent(Root, false);
        float height = bounds.size.y + 0.4f;
        Vector3 min = bounds.min - new Vector3(0.35f, 0f, 0.35f);
        Vector3 max = bounds.max + new Vector3(0.35f, 0f, 0.35f);
        for (int corner = 0; corner < 4; corner++)
        {
            float x = (corner & 1) == 0 ? min.x : max.x;
            float z = (corner & 2) == 0 ? min.z : max.z;
            Piece(root, "Post", new Vector3(x, height * 0.5f, z), new Vector3(0.12f, height, 0.12f), Quaternion.identity, wood);
        }

        foreach (float y in new[] { height * 0.45f, height * 0.92f })
        {
            Piece(root, "Beam", new Vector3((min.x + max.x) * 0.5f, y, min.z), new Vector3(max.x - min.x, 0.09f, 0.09f), Quaternion.identity, wood);
            Piece(root, "Beam", new Vector3((min.x + max.x) * 0.5f, y, max.z), new Vector3(max.x - min.x, 0.09f, 0.09f), Quaternion.identity, wood);
            Piece(root, "Beam", new Vector3(min.x, y, (min.z + max.z) * 0.5f), new Vector3(0.09f, 0.09f, max.z - min.z), Quaternion.identity, wood);
            Piece(root, "Beam", new Vector3(max.x, y, (min.z + max.z) * 0.5f), new Vector3(0.09f, 0.09f, max.z - min.z), Quaternion.identity, wood);
        }

        float span = max.x - min.x;
        float angle = Mathf.Atan2(height * 0.45f, span) * Mathf.Rad2Deg;
        Piece(root, "Brace", new Vector3((min.x + max.x) * 0.5f, height * 0.23f, max.z), new Vector3(Mathf.Sqrt(span * span + height * height * 0.2f), 0.07f, 0.07f),
            Quaternion.Euler(0f, 0f, angle), wood);
        Piece(root, "Planks", new Vector3((min.x + max.x) * 0.5f, height * 0.45f + 0.05f, max.z + 0.2f), new Vector3(span, 0.05f, 0.45f), Quaternion.identity, wood);
        return root;
    }

    // Level 2: a stone plinth round the base and two lanterns at the front. Level 3: banners at the back and a flower box.
    private Transform BuildDressing(int level)
    {
        ConstructionSettings settings = ConstructionSettings.Current;
        Bounds bounds = building.LocalBounds;
        var root = new GameObject(Dressing + level).transform;
        root.SetParent(Root, false);
        var random = new System.Random(owner.Definition.Id.GetHashCode() * 31 + level);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        Vector3 min = bounds.min - new Vector3(0.25f, 0f, 0.25f);
        Vector3 max = bounds.max + new Vector3(0.25f, 0f, 0.25f);

        if (level == 2)
        {
            float perimeter = 2f * ((max.x - min.x) + (max.z - min.z));
            int stones = Mathf.Clamp(Mathf.RoundToInt(perimeter / 0.55f), 8, 40);
            for (int i = 0; i < stones; i++)
            {
                Vector3 at = Perimeter(min, max, i / (float)stones);
                float size = Range(0.32f, 0.48f);
                Piece(root, "Plinth Stone", new Vector3(at.x, size * 0.45f, at.z), new Vector3(size, size * 0.95f, size * Range(0.8f, 1.2f)),
                    Quaternion.Euler(Range(-6f, 6f), Range(0f, 90f), Range(-6f, 6f)), settings.StoneMaterial);
            }

            foreach (float x in new[] { min.x - 0.2f, max.x + 0.2f })
            {
                Lantern(root, new Vector3(x, 0f, max.z + 0.2f), settings);
            }
        }
        else
        {
            foreach (float x in new[] { min.x - 0.15f, max.x + 0.15f })
            {
                float pole = Mathf.Max(2.4f, bounds.size.y + 0.8f);
                Piece(root, "Banner Pole", new Vector3(x, pole * 0.5f, min.z - 0.15f), new Vector3(0.08f, pole, 0.08f), Quaternion.identity, settings.ScaffoldMaterial);
                Piece(root, "Banner", new Vector3(x + (x < 0f ? 0.32f : -0.32f), pole - 0.6f, min.z - 0.15f), new Vector3(0.55f, 0.95f, 0.03f), Quaternion.identity, settings.ClothMaterial);
            }

            float boxX = Range(min.x + 0.3f, max.x - 0.9f);
            Piece(root, "Flower Box", new Vector3(boxX + 0.3f, 0.15f, max.z + 0.35f), new Vector3(0.7f, 0.3f, 0.28f), Quaternion.identity, settings.ScaffoldMaterial);
            for (int i = 0; i < 5; i++)
            {
                Transform bloom = Piece(root, "Bloom", new Vector3(boxX + 0.06f + i * 0.12f, 0.36f, max.z + 0.35f + Range(-0.06f, 0.06f)),
                    Vector3.one * Range(0.1f, 0.15f), Quaternion.identity, settings.ClothMaterial, PrimitiveType.Sphere);
                bloom.GetComponent<Renderer>().SetPropertyBlock(Tint(Color.HSVToRGB(Range(0f, 1f) < 0.5f ? Range(0.95f, 1f) : Range(0.1f, 0.16f), 0.6f, 1f)));
            }
        }

        return root;
    }

    private static MaterialPropertyBlock Tint(Color colour)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", colour);
        return block;
    }

    private static void Lantern(Transform parent, Vector3 at, ConstructionSettings settings)
    {
        Piece(parent, "Lantern Post", at + Vector3.up * 0.8f, new Vector3(0.09f, 1.6f, 0.09f), Quaternion.identity, settings.ScaffoldMaterial);
        Transform lamp = Piece(parent, "Lantern", at + Vector3.up * 1.62f, new Vector3(0.2f, 0.26f, 0.2f), Quaternion.identity, settings.LanternMaterial);
        var light = new GameObject("Lantern Light").AddComponent<Light>();
        light.transform.SetParent(lamp, false);
        light.type = LightType.Point;
        light.color = settings.LanternLight;
        light.intensity = 1.6f;
        light.range = 5f;
        light.shadows = LightShadows.None;
    }

    // A point on the rectangle's edge, 0..1 round it.
    private static Vector3 Perimeter(Vector3 min, Vector3 max, float t)
    {
        float w = max.x - min.x;
        float d = max.z - min.z;
        float along = t * 2f * (w + d);
        if (along < w) return new Vector3(min.x + along, 0f, min.z);
        along -= w;
        if (along < d) return new Vector3(max.x, 0f, min.z + along);
        along -= d;
        if (along < w) return new Vector3(max.x - along, 0f, max.z);
        along -= w;
        return new Vector3(min.x, 0f, max.z - along);
    }

    private static Transform Piece(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Material material,
        PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        // At once, so nothing that gathers colliders in Awake (stations, nodes) keeps a dead one.
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = size;
        if (material != null)
        {
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        return piece.transform;
    }

    private static ParticleSystem CreateDust(Transform parent)
    {
        var go = new GameObject("Construction Dust");
        go.transform.SetParent(parent, false);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.48f, 0.38f, 0.7f), new Color(0.75f, 0.68f, 0.55f, 0.5f));
        main.gravityModifier = -0.05f;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        Material material = ConstructionSettings.Current.DustMaterial;
        if (material != null)
        {
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        return particles;
    }

    private void Burst(Vector3 at, int count, float radius)
    {
        if (dust == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 ring = Random.insideUnitCircle.normalized * radius * Random.Range(0.6f, 1f);
            var emit = new ParticleSystem.EmitParams
            {
                position = at + new Vector3(ring.x, Random.Range(0f, 0.3f), ring.y),
                velocity = new Vector3(ring.x, 0.8f, ring.y).normalized * Random.Range(0.6f, 1.8f),
                applyShapeToPosition = false,
            };
            dust.Emit(emit, 1);
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
