using UnityEngine;

/// <summary>
/// Work done on gear, seen from outside: what colour an item's frame takes in a slot, and the glow on a weapon in the hand.
/// The enchantment's school sets the colour; empowering makes it stronger, and on its own gives a faint warmth.
/// </summary>
public static class GearGlow
{
    private const string GlowName = "Worked Glow";

    /// <summary>The colour of the work on a stack, or black when nothing has been done to it.</summary>
    public static Color ColorFor(ItemStack stack, EssenceSettings settings)
    {
        if (stack.IsEmpty || (stack.Enchantment == null && stack.Empower <= 0))
        {
            return Color.black;
        }

        Color glow = stack.Enchantment == null ? settings.EmpowerGlow
            : stack.Enchantment.School == EssenceSchool.Cursed ? settings.CursedGlow : settings.PurifiedGlow;
        return glow * (1f + stack.Empower * settings.GlowPerLevel);
    }

    /// <summary>The frame colour of a slot holding the stack, or null to leave it alone. Empowering shows in its "+2".</summary>
    public static Color? SlotTint(ItemStack stack, UIStyle style)
    {
        if (stack.IsEmpty || stack.Enchantment == null)
        {
            return null;
        }

        return stack.Enchantment.School == EssenceSchool.Cursed ? style.Danger : style.Good;
    }

    /// <summary>The corner of a slot: the item's tier and how far it has been empowered, "III +2".</summary>
    public static string CornerText(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return "";
        }

        string tier = Tiers.ToRoman(stack.Item.Tier);
        return stack.Empower > 0 ? $"{tier} +{stack.Empower}" : tier;
    }

    /// <summary>
    /// Lights a held model with the work on its stack: a small light and motes that drift off it. Not by emission, because
    /// weapons come from asset packs with shaders of their own that have none. The motes stay where they were shed, so a
    /// swing leaves a trail.
    /// </summary>
    public static void Apply(GameObject model, ItemStack stack, EssenceSettings settings)
    {
        if (model == null)
        {
            return;
        }

        Transform glow = model.transform.Find(GlowName);
        Color color = ColorFor(stack, settings);
        if (color == Color.black)
        {
            if (glow != null)
            {
                glow.gameObject.SetActive(false);
            }

            return;
        }

        if (glow == null)
        {
            glow = Build(model, settings);
        }

        glow.gameObject.SetActive(true);
        var light = glow.GetComponent<Light>();
        // The hue only: a brighter colour on a light this close turns the player the same colour.
        light.color = color / Mathf.Max(0.01f, color.maxColorComponent);
        light.intensity = 0.12f + stack.Empower * 0.04f;
        var motes = glow.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = motes.main;
        main.startColor = color;
        ParticleSystem.EmissionModule emission = motes.emission;
        emission.rateOverTime = 6f + stack.Empower * 4f;
    }

    private static Transform Build(GameObject model, EssenceSettings settings)
    {
        var go = new GameObject(GlowName);
        go.SetActive(false);
        go.transform.SetParent(model.transform, false);
        Bounds shape = LocalBounds(model);
        go.transform.localPosition = shape.center;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 1.6f;
        light.shadows = LightShadows.None;

        var motes = go.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = motes.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
        main.gravityModifier = -0.02f;
        main.maxParticles = 40;
        ParticleSystem.ShapeModule area = motes.shape;
        area.shapeType = ParticleSystemShapeType.Box;
        area.scale = shape.size;
        ParticleSystem.ColorOverLifetimeModule fade = motes.colorOverLifetime;
        fade.enabled = true;
        var curve = new Gradient();
        curve.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        fade.color = curve;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = settings.GlowMaterial;
        go.SetActive(true);
        motes.Play();
        return go.transform;
    }

    private static Bounds LocalBounds(GameObject model)
    {
        var filter = model.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return new Bounds(Vector3.zero, Vector3.one * 0.3f);
        }

        Bounds mesh = filter.sharedMesh.bounds;
        Vector3 centre = model.transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center));
        Vector3 size = Vector3.Scale(mesh.size, filter.transform.lossyScale);
        Vector3 scale = model.transform.lossyScale;
        return new Bounds(centre, new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z));
    }
}
