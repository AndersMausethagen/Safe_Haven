using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

/// <summary>
/// The reusable effect graphs the skills are dressed from (Docs/CombatSkillsResearch_2026-11-24.md §6), each with exposed values a skill's
/// prefab overrides: a Burst (sparks, shards, embers flung out), a Mist (drifting smoke), a Ring (a flat mark growing on the
/// ground), Spikes (ice crystals out of the ground) and Flames (Unity's sample). Safe Haven › Tools › Effects › Rebuild Skill VFX Graphs.
/// </summary>
public static class SkillVfxArchetypes
{
    public const string Folder = "Assets/_Project/Art/Effects/Skills/Graphs";

    public const string Textures = "Assets/_Project/Art/Effects/Skills/Textures";

    [MenuItem("Safe Haven/Tools/Effects/Rebuild Skill VFX Graphs")]
    public static void BuildAll()
    {
        Burst();
        Mist();
        Ring();
        Spikes();
        Flames();
        Debris();
        Gather();
        Pillar();
    }

    /// <summary>
    /// Power drawn in (an ultimate's wind-up, 2026-11-27): motes born on a sphere's surface, pulled to its centre and streaking as they
    /// come, a loop while the effect lives. Snow for Fimbulwinter, embers for Surtr's Brand, dust and pebbles for Trollsbane Wrath.
    /// </summary>
    public static string Gather()
    {
        EnsureFolder();
        string path = Folder + "/Skill Gather.vfx";
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock rate = b.Block(spawn, "Constant Spawn Rate");
        VFXContext init = b.Context("Initialize Particle");
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        init.GetData().SetSettingValue("capacity", 1024u);
        VFXBlock position = b.Block(init, "|Set|_Position Shape|Sphere");
        VfxBuilder.SetEnum(position, "positionMode", "Surface");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VfxBuilder.SetEnum(lifetime, "Random", "Uniform");
        VFXBlock size = b.Block(init, "|Set|_Size");
        VfxBuilder.SetEnum(size, "Random", "Uniform");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        VFXContext update = b.Context("Update Particle");
        VFXBlock pull = b.Block(update, "Attractor Shape|Sphere");
        VFXBlock turbulence = b.Block(update, "Turbulence");
        VFXContext output = b.Context("Output Particle|Unlit|Quad");
        VfxBuilder.SetEnum(output, "blendMode", "Additive");
        b.Block(output, "Orient|Along Velocity");
        VFXBlock sizeOverLife = b.Block(output, "|Set|_Size|Over Life");
        VFXBlock colourOverLife = b.Block(output, "|Set|_Color|Over Life");
        VfxBuilder.SetEnum(sizeOverLife, "Composition", "Multiply");
        VfxBuilder.SetEnum(colourOverLife, "Composition", "Multiply");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);
        b.Expose("Rate", 160f, VfxBuilder.Slot(rate, "Rate"));
        b.Expose("Radius", 4f, VfxBuilder.Slot(position, "radius"));
        b.Expose("Lifetime Min", 0.5f, VfxBuilder.Slot(lifetime, "A"));
        b.Expose("Lifetime Max", 0.9f, VfxBuilder.Slot(lifetime, "B"));
        b.Expose("Size Min", 0.08f, VfxBuilder.Slot(size, "A"));
        b.Expose("Size Max", 0.2f, VfxBuilder.Slot(size, "B"));
        b.Expose("Color", new Vector3(1.5f, 2.5f, 4f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Pull Speed", 7f, VfxBuilder.Slot(pull, "attractionSpeed"));
        b.Expose("Pull Force", 14f, VfxBuilder.Slot(pull, "attractionForce"));
        b.Expose("Turbulence", 1.5f, VfxBuilder.Slot(turbulence, "Intensity"));
        b.Expose("Texture", Tex(Textures + "/Streak.png"), VfxBuilder.Slot(output, "mainTexture"));
        // Pulled to a small sphere at the effect's middle, a little above its feet (the effect sits on the caster's chest or the ground).
        VfxBuilder.Set(pull, "radius", 0.15f);
        VfxBuilder.Set(sizeOverLife, "Size", new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f)));
        VfxBuilder.Set(colourOverLife, "Color", FadeInOut(0.25f, 0.75f));
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    /// <summary>
    /// A column of light or fire standing up from the ground (an ultimate's impact, a payoff's burst, 2026-11-27): one or a few tall quads
    /// that face the camera but keep upright, flaring wide then thinning as they fade, so it reads from the high camera as a column.
    /// </summary>
    public static string Pillar()
    {
        EnsureFolder();
        string path = Folder + "/Skill Pillar.vfx";
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock burst = b.Block(spawn, "Single Burst");
        VFXContext init = b.Context("Initialize Particle");
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        VFXBlock position = b.Block(init, "|Set|_Position");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VFXBlock size = b.Block(init, "|Set|_Size");
        VFXBlock scale = b.Block(init, "|Set|_Scale");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        VFXContext update = b.Context("Update Particle");
        VFXContext output = b.Context("Output Particle|Unlit|Quad");
        VfxBuilder.SetEnum(output, "blendMode", "Additive");
        VFXBlock orient = b.Block(output, "Orient|Face Camera Plane");
        VfxBuilder.SetEnum(orient, "mode", "FixedAxis");
        VFXBlock sizeOverLife = b.Block(output, "|Set|_Size|Over Life");
        VFXBlock colourOverLife = b.Block(output, "|Set|_Color|Over Life");
        VfxBuilder.SetEnum(sizeOverLife, "Composition", "Multiply");
        VfxBuilder.SetEnum(colourOverLife, "Composition", "Multiply");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);
        b.Expose("Count", 1f, VfxBuilder.Slot(burst, "Count"));
        // The quad is centred: lift it by half its height so it stands on the ground.
        b.Expose("Lift", new Vector3(0f, 4f, 0f), VfxBuilder.Slot(position, "_Position"));
        b.Expose("Lifetime", 0.6f, VfxBuilder.Slot(lifetime, "_Lifetime"));
        b.Expose("Width", 1.6f, VfxBuilder.Slot(size, "_Size"));
        b.Expose("Shape", new Vector3(1f, 5f, 1f), VfxBuilder.Slot(scale, "_Scale"));
        b.Expose("Color", new Vector3(4f, 3.5f, 2.5f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Texture", Tex(Textures + "/Beam.png"), VfxBuilder.Slot(output, "mainTexture"));
        VfxBuilder.Set(orient, "Up", new DirectionType { direction = Vector3.up });
        // Flares wide at once, then thins away.
        b.Expose("Width Over Life", new AnimationCurve(new Keyframe(0f, 0.4f, 0f, 12f), new Keyframe(0.1f, 1.25f), new Keyframe(1f, 0.15f)),
            VfxBuilder.Slot(sizeOverLife, "Size"));
        VfxBuilder.Set(colourOverLife, "Color", FadeInOut(0.04f, 0.35f));
        VfxBuilder.SetEnum(output, "cullMode", "Off");
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    private static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    private static Gradient FadeInOut(float inAt, float outFrom)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// <summary>Drifting smoke or mist, a loop: frost mist, heat haze, a hex's murk. Soft, alpha-blended, from the sample's smoke flipbook.</summary>
    public static string Mist()
    {
        EnsureFolder();
        string path = Folder + "/Skill Mist.vfx";
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock rate = b.Block(spawn, "Constant Spawn Rate");
        VFXContext init = b.Context("Initialize Particle");
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        init.GetData().SetSettingValue("capacity", 256u);
        VFXBlock position = b.Block(init, "|Set|_Position Shape|Sphere");
        VFXBlock velocity = b.Block(init, "|Set|_Velocity");
        VfxBuilder.SetEnum(velocity, "Random", "Uniform");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VfxBuilder.SetEnum(lifetime, "Random", "Uniform");
        VFXBlock size = b.Block(init, "|Set|_Size");
        VfxBuilder.SetEnum(size, "Random", "Uniform");
        VFXBlock angle = b.Block(init, "|Set|_Angle");
        VfxBuilder.SetEnum(angle, "Random", "Uniform");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        VFXBlock alpha = b.Block(init, "|Set|_Alpha");
        VFXContext update = b.Context("Update Particle");
        VFXBlock turbulence = b.Block(update, "Turbulence");
        b.Block(update, "Linear Drag");
        VFXBlock flipbook = b.Block(update, "Flipbook Player");
        VFXContext output = b.Context("Output Particle|Unlit|Quad");
        VfxBuilder.SetEnum(output, "blendMode", "Alpha");
        VfxBuilder.SetEnum(output, "uvMode", "Flipbook");
        output.SetSettingValue("flipbookBlendFrames", true);
        output.SetSettingValue("useSoftParticle", true);
        VFXBlock sizeOverLife = b.Block(output, "|Set|_Size|Over Life");
        VFXBlock colourOverLife = b.Block(output, "|Set|_Color|Over Life");
        VfxBuilder.SetEnum(sizeOverLife, "Composition", "Multiply");
        VfxBuilder.SetEnum(colourOverLife, "Composition", "Multiply");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);
        b.Expose("Rate", 14f, VfxBuilder.Slot(rate, "Rate"));
        b.Expose("Radius", 1.2f, VfxBuilder.Slot(position, "radius"));
        b.Expose("Velocity Min", new Vector3(-0.3f, 0.1f, -0.3f), VfxBuilder.Slot(velocity, "A"));
        b.Expose("Velocity Max", new Vector3(0.3f, 0.6f, 0.3f), VfxBuilder.Slot(velocity, "B"));
        b.Expose("Lifetime Min", 1.6f, VfxBuilder.Slot(lifetime, "A"));
        b.Expose("Lifetime Max", 2.6f, VfxBuilder.Slot(lifetime, "B"));
        b.Expose("Size Min", 0.8f, VfxBuilder.Slot(size, "A"));
        b.Expose("Size Max", 1.6f, VfxBuilder.Slot(size, "B"));
        VfxBuilder.Set(angle, "A", new Vector3(0f, 0f, 0f));
        VfxBuilder.Set(angle, "B", new Vector3(0f, 0f, 360f));
        b.Expose("Color", new Vector3(0.8f, 0.92f, 1f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Alpha", 0.35f, VfxBuilder.Slot(alpha, "_Alpha"));
        b.Expose("Turbulence", 0.6f, VfxBuilder.Slot(turbulence, "Intensity"));
        b.Expose("Texture", Tex("Assets/_Project/Art/Effects/VFX Graph/Additions/Textures/Smoke/WispySmoke03b_8x8.png"), VfxBuilder.Slot(output, "mainTexture"));
        b.Expose("Flipbook Size", new Vector2(8f, 8f), VfxBuilder.Slot(output, "flipBookSize"));
        VfxBuilder.Set(flipbook, "frameRate", 18f);
        VfxBuilder.Set(sizeOverLife, "Size", new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
        VfxBuilder.Set(colourOverLife, "Color", FadeInOut(0.25f, 0.6f));
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    /// <summary>One flat mark on the ground growing outwards: a shockwave, or (slower, turning) the rune circle a cast stands on.</summary>
    public static string Ring()
    {
        EnsureFolder();
        string path = Folder + "/Skill Ring.vfx";
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock burst = b.Block(spawn, "Single Burst");
        VFXContext init = b.Context("Initialize Particle");
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VFXBlock size = b.Block(init, "|Set|_Size");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        VFXBlock spin = b.Block(init, "|Set|_Angular Velocity");
        VFXContext update = b.Context("Update Particle");
        b.Block(update, "Integration Update|Rotation");
        VFXContext output = b.Context("Output Particle|Unlit|Quad");
        VfxBuilder.SetEnum(output, "blendMode", "Additive");
        VFXBlock orient = b.Block(output, "Orient|Advanced");
        VFXBlock sizeOverLife = b.Block(output, "|Set|_Size|Over Life");
        VFXBlock colourOverLife = b.Block(output, "|Set|_Color|Over Life");
        VfxBuilder.SetEnum(sizeOverLife, "Composition", "Multiply");
        VfxBuilder.SetEnum(colourOverLife, "Composition", "Multiply");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);
        VfxBuilder.Set(burst, "Count", 1f);
        b.Expose("Lifetime", 0.5f, VfxBuilder.Slot(lifetime, "_Lifetime"));
        b.Expose("Size", 6f, VfxBuilder.Slot(size, "_Size"));
        b.Expose("Color", new Vector3(3f, 2.2f, 1f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Spin", new Vector3(0f, 0f, 0f), VfxBuilder.Slot(spin, "_AngularVelocity"));
        b.Expose("Texture", Tex(Textures + "/Ring.png"), VfxBuilder.Slot(output, "mainTexture"));
        b.Expose("Size Over Life", new AnimationCurve(new Keyframe(0f, 0.2f, 0f, 3f), new Keyframe(1f, 1f)), VfxBuilder.Slot(sizeOverLife, "Size"));
        b.Expose("Fade", FadeInOut(0.08f, 0.45f), VfxBuilder.Slot(colourOverLife, "Color"));
        // Flat on the ground, its top along the effect's forward (the aim). The axes are DirectionType slots: a bare Vector3 was dropped
        // silently and left the quad standing upright (the user, 2026-11-25: "from above we mostly see a flat line"). Drawn from both
        // sides, so which way the normal points can't hide it from the top-down camera.
        VfxBuilder.Set(orient, "AxisZ", new DirectionType { direction = new Vector3(0f, -1f, 0f) });
        VfxBuilder.Set(orient, "AxisY", new DirectionType { direction = new Vector3(0f, 0f, 1f) });
        VfxBuilder.SetEnum(output, "cullMode", "Off");
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    /// <summary>Ice crystals breaking out of the ground in a small cluster, standing a moment and sinking: a lit mesh per particle.</summary>
    public static string Spikes()
    {
        EnsureFolder();
        string path = Folder + "/Skill Spikes.vfx";
        Mesh crystal = SkillMeshes.Crystal();
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock burst = b.Block(spawn, "Single Burst");
        VFXContext init = b.Context("Initialize Particle");
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        VFXBlock position = b.Block(init, "|Set|_Position Shape|Sphere");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VfxBuilder.SetEnum(lifetime, "Random", "Uniform");
        VFXBlock scale = b.Block(init, "|Set|_Scale");
        VfxBuilder.SetEnum(scale, "Random", "Uniform");
        VFXBlock angle = b.Block(init, "|Set|_Angle");
        VfxBuilder.SetEnum(angle, "Random", "Uniform");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        // A mesh particle is sized by Size times Scale; Size's default (0.1) made ten-centimetre crystals.
        VFXBlock size = b.Block(init, "|Set|_Size");
        VFXContext update = b.Context("Update Particle");
        VFXContext output = b.Context("Output Particle|URP Lit|Mesh");
        // Colour from the particle alone: the default map (a dot) sampled on an unwrapped crystal turned it invisible.
        VfxBuilder.SetEnum(output, "useBaseColorMap", "None");
        VFXBlock sizeOverLife = b.Block(output, "|Set|_Size|Over Life");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);
        b.Expose("Count", 5f, VfxBuilder.Slot(burst, "Count"));
        b.Expose("Size", 1f, VfxBuilder.Slot(size, "_Size"));
        b.Expose("Radius", 0.5f, VfxBuilder.Slot(position, "radius"));
        b.Expose("Lifetime Min", 1.1f, VfxBuilder.Slot(lifetime, "A"));
        b.Expose("Lifetime Max", 1.5f, VfxBuilder.Slot(lifetime, "B"));
        b.Expose("Scale Min", new Vector3(0.5f, 0.8f, 0.5f), VfxBuilder.Slot(scale, "A"));
        b.Expose("Scale Max", new Vector3(0.9f, 1.8f, 0.9f), VfxBuilder.Slot(scale, "B"));
        b.Expose("Tilt Min", new Vector3(-25f, 0f, -25f), VfxBuilder.Slot(angle, "A"));
        b.Expose("Tilt Max", new Vector3(25f, 360f, 25f), VfxBuilder.Slot(angle, "B"));
        b.Expose("Color", new Vector3(0.7f, 0.9f, 1.1f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Mesh", crystal, VfxBuilder.Slot(output, "mesh"));
        b.Expose("Smoothness", 0.85f, VfxBuilder.Slot(output, "smoothness"));
        // Up out of the ground fast, stand, then sink.
        VfxBuilder.Set(sizeOverLife, "Size", new AnimationCurve(new Keyframe(0f, 0f, 0f, 9f), new Keyframe(0.12f, 1.05f), new Keyframe(0.2f, 1f),
            new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    /// <summary>Rock chunks thrown up by a slam: lit meshes flung out and up, tumbling, falling, shrinking away as they land.</summary>
    public static string Debris()
    {
        EnsureFolder();
        string path = Folder + "/Skill Debris.vfx";
        Mesh rock = SkillMeshes.Rock();
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock burst = b.Block(spawn, "Single Burst");
        VFXContext init = b.Context("Initialize Particle");
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        VFXBlock position = b.Block(init, "|Set|_Position Shape|Sphere");
        VFXBlock velocity = b.Block(init, "|Set|_Velocity");
        VfxBuilder.SetEnum(velocity, "Random", "Uniform");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VfxBuilder.SetEnum(lifetime, "Random", "Uniform");
        VFXBlock size = b.Block(init, "|Set|_Size");
        VfxBuilder.SetEnum(size, "Random", "Uniform");
        VFXBlock spin = b.Block(init, "|Set|_Angular Velocity");
        VfxBuilder.SetEnum(spin, "Random", "Uniform");
        VFXBlock angle = b.Block(init, "|Set|_Angle");
        VfxBuilder.SetEnum(angle, "Random", "Uniform");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        VFXContext update = b.Context("Update Particle");
        VFXBlock gravity = b.Block(update, "Gravity");
        b.Block(update, "Integration Update|Rotation");
        VFXContext output = b.Context("Output Particle|URP Lit|Mesh");
        VfxBuilder.SetEnum(output, "useBaseColorMap", "None");
        VFXBlock sizeOverLife = b.Block(output, "|Set|_Size|Over Life");
        VfxBuilder.SetEnum(sizeOverLife, "Composition", "Multiply");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);
        b.Expose("Count", 10f, VfxBuilder.Slot(burst, "Count"));
        b.Expose("Radius", 0.6f, VfxBuilder.Slot(position, "radius"));
        b.Expose("Velocity Min", new Vector3(-3.5f, 3f, -3.5f), VfxBuilder.Slot(velocity, "A"));
        b.Expose("Velocity Max", new Vector3(3.5f, 7f, 3.5f), VfxBuilder.Slot(velocity, "B"));
        b.Expose("Lifetime Min", 0.7f, VfxBuilder.Slot(lifetime, "A"));
        b.Expose("Lifetime Max", 1.1f, VfxBuilder.Slot(lifetime, "B"));
        b.Expose("Size Min", 0.6f, VfxBuilder.Slot(size, "A"));
        b.Expose("Size Max", 1.4f, VfxBuilder.Slot(size, "B"));
        VfxBuilder.Set(spin, "A", new Vector3(-540f, -540f, -540f));
        VfxBuilder.Set(spin, "B", new Vector3(540f, 540f, 540f));
        VfxBuilder.Set(angle, "A", Vector3.zero);
        VfxBuilder.Set(angle, "B", new Vector3(360f, 360f, 360f));
        b.Expose("Color", new Vector3(0.42f, 0.38f, 0.32f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Gravity", new Vector3(0f, -18f, 0f), VfxBuilder.Slot(gravity, "Force"));
        b.Expose("Mesh", rock, VfxBuilder.Slot(output, "mesh"));
        b.Expose("Smoothness", 0.15f, VfxBuilder.Slot(output, "smoothness"));
        VfxBuilder.Set(sizeOverLife, "Size", new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    /// <summary>Unity's sample flames (VFX Graph Additions), copied for burning: its exposed colours, size, rate and flipbook stay.</summary>
    public static string Flames()
    {
        EnsureFolder();
        string path = Folder + "/Skill Flames.vfx";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(path) == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Art/Effects/VFX Graph/Additions/VFX/Flames.vfx", path);
        }

        return path;
    }

    /// <summary>A burst flung out from a sphere: sparks, ice shards, embers. Stretched along their flight, falling, fading.</summary>
    public static string Burst()
    {
        EnsureFolder();
        string path = Folder + "/Skill Burst.vfx";
        VfxBuilder b = VfxBuilder.Create(path);
        VFXContext spawn = b.Context("Spawn");
        VFXBlock burst = b.Block(spawn, "Single Burst");
        VFXContext init = b.Context("Initialize Particle");
        // Bounds worked out as it plays: a burst's extent depends on what a skill sets, and empty bounds cull it before it simulates.
        VfxBuilder.SetEnum(init.GetData(), "boundsMode", "Automatic");
        // Room for a dense spray (the default 128 cut a 140-ember cone short).
        init.GetData().SetSettingValue("capacity", 512u);
        VFXBlock position = b.Block(init, "|Set|_Position Shape|Sphere");
        VFXBlock velocity = b.Block(init, "|Set|_Velocity");
        VfxBuilder.SetEnum(velocity, "Random", "Uniform");
        VFXBlock lifetime = b.Block(init, "|Set|_Lifetime");
        VfxBuilder.SetEnum(lifetime, "Random", "Uniform");
        VFXBlock size = b.Block(init, "|Set|_Size");
        VfxBuilder.SetEnum(size, "Random", "Uniform");
        VFXBlock colour = b.Block(init, "|Set|_Color");
        VFXContext update = b.Context("Update Particle");
        VFXBlock gravity = b.Block(update, "Gravity");
        VFXBlock drag = b.Block(update, "Linear Drag");
        VFXContext output = b.Context("Output Particle|Unlit|Quad");
        VfxBuilder.SetEnum(output, "blendMode", "Additive");
        b.Block(output, "Orient|Along Velocity");
        b.Block(output, "|Set|_Size|Over Life");
        b.Block(output, "|Set|_Color|Over Life");
        VfxBuilder.Flow(spawn, init);
        VfxBuilder.Flow(init, update);
        VfxBuilder.Flow(update, output);

        // What a skill sets: how many, from how wide, flung how, living and sized how, in what colour (above 1 glows), falling how, drawn
        // with what. Defaults: a spray of golden sparks.
        VFXModel sizeOverLife = output.children.ElementAt(1);
        VFXModel colourOverLife = output.children.ElementAt(2);
        VfxBuilder.SetEnum(sizeOverLife, "Composition", "Multiply");
        VfxBuilder.SetEnum(colourOverLife, "Composition", "Multiply");
        b.Expose("Count", 40f, VfxBuilder.Slot(burst, "Count"));
        b.Expose("Radius", 0.3f, VfxBuilder.Slot(position, "radius"));
        b.Expose("Velocity Min", new Vector3(-3f, 1.5f, -3f), VfxBuilder.Slot(velocity, "A"));
        b.Expose("Velocity Max", new Vector3(3f, 6f, 3f), VfxBuilder.Slot(velocity, "B"));
        b.Expose("Lifetime Min", 0.35f, VfxBuilder.Slot(lifetime, "A"));
        b.Expose("Lifetime Max", 0.8f, VfxBuilder.Slot(lifetime, "B"));
        b.Expose("Size Min", 0.04f, VfxBuilder.Slot(size, "A"));
        b.Expose("Size Max", 0.1f, VfxBuilder.Slot(size, "B"));
        b.Expose("Color", new Vector3(4f, 2.6f, 1f), VfxBuilder.Slot(colour, "_Color"));
        b.Expose("Gravity", new Vector3(0f, -9.81f, 0f), VfxBuilder.Slot(gravity, "Force"));
        b.Expose("Drag", 1.5f, VfxBuilder.Slot(drag, "dragCoefficient"));
        b.Expose("Texture", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Effects/Combat/Combat Star.png"), VfxBuilder.Slot(output, "mainTexture"));
        VfxBuilder.Set(sizeOverLife, "Size", new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        VfxBuilder.Set(colourOverLife, "Color", fade);
        b.Save();
        return VfxGraphAuthoring.Describe(path);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Effects/Skills"))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Art/Effects", "Skills");
        }

        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Art/Effects/Skills", "Graphs");
        }
    }
}
