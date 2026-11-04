using UnityEngine;

/// <summary>
/// What sets one creature's body apart from the next of its kind: a little size and shade of its own when it wakes, an elite's colour, a boss's glow
/// and its eyes. It changes the body's own copies of its materials, as a hit's flash lays a property block over them and clears it again.
/// The copies go when the scene does, so they outlast the creature for as long as its corpse needs them.
/// </summary>
public static class CreatureLook
{
    /// <summary>How an eye's material is named, whether it lights the model's own eyes or a pair set on its bones. Built by AgentScripts/BuildCreatureModels.cs.</summary>
    public const string EyeMaterial = "Eye ";

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

    /// <summary>Gives a body its own size and shade, as its definition allows. Call before anything remembers the model's scale.</summary>
    public static void Vary(Transform model, MonsterDefinition definition)
    {
        if (model == null || definition == null)
        {
            return;
        }

        if (definition.SizeVariation > 0f)
        {
            float size = 1f + Random.Range(-definition.SizeVariation, definition.SizeVariation);
            model.localScale *= size;
            model.localPosition *= size;
        }

        if (definition.ShadeVariation > 0f)
        {
            float light = 1f + Random.Range(-definition.ShadeVariation, definition.ShadeVariation);
            float warm = Random.Range(-definition.ShadeVariation, definition.ShadeVariation) * 0.5f;
            Tint(model.gameObject, new Color(light + warm, light, light - warm, 1f));
        }
    }

    /// <summary>Multiplies the body's colour, leaving its eyes alone.</summary>
    public static void Tint(GameObject body, Color tint)
    {
        foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsBody(renderer))
            {
                continue;
            }

            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty(BaseColor) && !IsEye(material))
                {
                    material.SetColor(BaseColor, material.GetColor(BaseColor) * tint);
                }
            }
        }
    }

    /// <summary>Lights the parts of the body whose material has an emission map. A material without one stays as it is.</summary>
    public static void Glow(GameObject body, Color colour)
    {
        foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsBody(renderer))
            {
                continue;
            }

            foreach (Material material in renderer.materials)
            {
                // By its map, as a body's own copy of a material doesn't reliably say the keyword is on.
                if (material.HasProperty(EmissionMap) && material.GetTexture(EmissionMap) != null)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionColor, colour);
                }
            }
        }
    }

    public static void SetEyes(GameObject body, Color colour)
    {
        foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsBody(renderer) || !HasEyes(renderer))
            {
                continue;
            }

            foreach (Material material in renderer.materials)
            {
                if (IsEye(material))
                {
                    material.SetColor(BaseColor, colour);
                }
            }
        }
    }

    /// <summary>The light goes out of them.</summary>
    public static void CloseEyes(GameObject body) => SetEyes(body, Color.black);

    private static bool HasEyes(Renderer renderer)
    {
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null && IsEye(material))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEye(Material material) => material.name.StartsWith(EyeMaterial, System.StringComparison.Ordinal);

    private static bool IsBody(Renderer renderer) =>
        !(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer) && !(renderer is LineRenderer);
}
