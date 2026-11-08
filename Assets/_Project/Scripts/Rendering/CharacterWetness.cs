using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How soaked one character is (the player, a resident, a companion, a creature): out in the rain under open sky they
/// soak through over half a minute; under a roof or out of the rain they dry slowly, and the player dries fast by a fire.
/// Wet, their clothes, hair and skin darken and gloss (porous stuff most, metal least). Characters use URP Lit, so the
/// look is set on copies of their materials, made the first time they get wet, put away once dry and worn again in the next shower. The copies are
/// never destroyed here: a death ragdoll takes its skin's materials (DeathRagdoll), so it may still be drawing them; the
/// scene's unload frees them. A hit flash (HitReaction) is a property block over them and still shows. Added by
/// CharacterLook; numbers in RenderingSettings › Wet characters.
/// </summary>
public sealed class CharacterWetness : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private const float Tick = 0.25f;

    private sealed class Dressed
    {
        public Renderer renderer;
        public Material[] dry;
        public Material[] wet;
        public Color[] colour;
        public float[] smoothness;
        public float[] porous;
    }

    private readonly List<Dressed> dressed = new List<Dressed>();
    private readonly List<Renderer> found = new List<Renderer>();
    private RenderingSettings settings;
    private PlayerWarmth warmth;
    private float wetness;
    private float applied;
    private float nextTick;
    private float nextShelterCheck;
    private float nextRendererScan;
    private bool sheltered;

    /// <summary>0 dry to 1 soaked through.</summary>
    public float Wetness => wetness;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        warmth = GetComponentInParent<PlayerWarmth>();
        nextTick = Time.time + Random.value * Tick; // spread the characters' ticks over frames
    }

    /// <summary>How wet after <paramref name="seconds"/>: soaks at the rain's strength out in it, otherwise dries.</summary>
    public static float Advance(float wetness, float rain, bool sheltered, bool byAFire, float soakSeconds, float drySeconds,
        float fireDrySeconds, float seconds)
    {
        if (rain > 0.02f && !sheltered && !byAFire)
        {
            return Mathf.Min(1f, wetness + rain * seconds / Mathf.Max(soakSeconds, 0.1f));
        }

        return Mathf.Max(0f, wetness - seconds / Mathf.Max(byAFire ? fireDrySeconds : drySeconds, 0.1f));
    }

    private void Update()
    {
        if (settings == null || Time.time < nextTick)
        {
            return;
        }

        nextTick = Time.time + Tick;
        WeatherSystem weather = WeatherSystem.Active;
        float rain = weather != null && !weather.Snowing ? weather.Current.Rain : 0f;
        if (rain > 0.02f && Time.time >= nextShelterCheck)
        {
            nextShelterCheck = Time.time + 1f;
            sheltered = UnderCover();
        }

        bool byAFire = warmth != null && warmth.ByAFire;
        wetness = Advance(wetness, rain, sheltered, byAFire, settings.SoakSeconds, settings.DrySeconds, settings.FireDrySeconds, Tick);
        if (Mathf.Abs(wetness - applied) > 0.02f || (wetness <= 0f && applied > 0f))
        {
            Apply();
        }
    }

    private bool UnderCover()
    {
        // From just over the head, straight up: a roof, a rock overhang, a jetty above.
        Vector3 from = transform.position + Vector3.up * settings.ShelterRayStart;
        return Physics.Raycast(from, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore);
    }

    private void Apply()
    {
        applied = wetness;
        if (wetness <= 0f)
        {
            Undress();
            return;
        }

        if (Time.time >= nextRendererScan || dressed.Count == 0)
        {
            nextRendererScan = Time.time + 2f;
            Rescan();
        }

        float darkening = settings.WetDarkening;
        float gloss = settings.WetSmoothness;
        for (int d = dressed.Count - 1; d >= 0; d--)
        {
            Dressed one = dressed[d];
            if (one.renderer == null)
            {
                dressed.RemoveAt(d);
                continue;
            }

            Material[] now = one.renderer.sharedMaterials;
            if (SameMaterials(now, one.dry))
            {
                one.renderer.sharedMaterials = one.wet; // wet again after drying: the same copies
            }
            else if (!SameMaterials(now, one.wet))
            {
                // Reskinned or re-dressed since (ModularCharacter): start again from what it wears now.
                dressed.RemoveAt(d);
                continue;
            }

            for (int i = 0; i < one.wet.Length; i++)
            {
                if (one.wet[i] == one.dry[i])
                {
                    continue; // not a material that can look wet
                }

                float porous = one.porous[i] * wetness;
                Color colour = one.colour[i];
                float keep = 1f - darkening * porous;
                one.wet[i].SetColor(BaseColorId, new Color(colour.r * keep, colour.g * keep, colour.b * keep, colour.a));
                one.wet[i].SetFloat(SmoothnessId, Mathf.Lerp(one.smoothness[i], Mathf.Max(one.smoothness[i], gloss), wetness));
            }
        }
    }

    private void Rescan()
    {
        GetComponentsInChildren(false, found);
        foreach (Renderer renderer in found)
        {
            if (!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer) || Has(renderer))
            {
                continue;
            }

            Material[] dry = renderer.sharedMaterials;
            var one = new Dressed
            {
                renderer = renderer, dry = dry, wet = new Material[dry.Length], colour = new Color[dry.Length],
                smoothness = new float[dry.Length], porous = new float[dry.Length],
            };
            bool any = false;
            for (int i = 0; i < dry.Length; i++)
            {
                Material material = dry[i];
                if (material == null || !material.HasProperty(BaseColorId) || !material.HasProperty(SmoothnessId)
                    || material.renderQueue >= 2450)
                {
                    one.wet[i] = material; // unlit eyes, see-through things
                    continue;
                }

                one.wet[i] = new Material(material) { name = material.name + " (wet)" };
                one.colour[i] = material.GetColor(BaseColorId);
                one.smoothness[i] = material.GetFloat(SmoothnessId);
                float metal = material.HasProperty(MetallicId) ? material.GetFloat(MetallicId) : 0f;
                // Cloth, hair and hide drink water and darken; polished or metal things only shine.
                float sealed_ = Mathf.Max(one.smoothness[i], metal);
                one.porous[i] = Mathf.Lerp(1f, 0.25f, sealed_ * sealed_);
                any = true;
            }

            if (any)
            {
                renderer.sharedMaterials = one.wet;
                dressed.Add(one);
            }
        }
    }

    private bool Has(Renderer renderer)
    {
        foreach (Dressed one in dressed)
        {
            if (one.renderer == renderer)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameMaterials(Material[] a, Material[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private void Undress()
    {
        foreach (Dressed one in dressed)
        {
            if (one.renderer != null && SameMaterials(one.renderer.sharedMaterials, one.wet))
            {
                one.renderer.sharedMaterials = one.dry;
            }
        }

        // Kept, to be worn again in the next shower.
    }
}
