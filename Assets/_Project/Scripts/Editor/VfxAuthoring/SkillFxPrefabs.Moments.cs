using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The skills' big moments and the newer skills' own looks (the user, 2026-11-27: "make sure that ultimates look, feel and sound
/// satisfying. Go crazy with particle effects, vfx, sfx needed to make them stand out. Give them that AAA-feel ... Do the same with the
/// other abilities as well. but non-ultimates can be a bit more grounded"). Runs at the end of Dress the First Skills, or on its own.
/// <para>
/// The four ultimates get the lot: power gathering in through the wind-up (the Gather graph), the camera pushing in, a flare and two
/// shockwaves at the impact, the ground cracking and glowing, a column of light or fire (the Pillar graph), a storm of shards or embers,
/// spikes of ice or rock in a ring, a screen punch with slow time and an afterglow (SkillMomentLook), a quake (a crater, a gust through the
/// grass, a stumble), and layered sound (a riser, an impact with a sub drop, a tail: Tools/Audio/skill_moments.py). Trollsbane Wrath and
/// War Cry keep their own looks and gain these on top (the cues named "Ult ..." are made again each run).
/// </para>
/// <para>
/// The newer skills (the weapons' techniques, the three powers, Raven's Mark and Bear-Hide) get looks of their own, grounded: dust, steel,
/// sparks and blood for the weapons, their school's colour for the powers, a short hit stop and a kick by their weight. And every payoff
/// (a combo using a status up) bursts on its foe with its status's own look and sound (Consume Look/Sound), so a set-up paying off is felt.
/// </para>
/// </summary>
public static partial class SkillFxPrefabs
{
    [MenuItem("Safe Haven/Tools/Effects/Dress the Moments and Newer Skills")]
    public static string DressMomentsMenu()
    {
        string log = DressMoments();
        AssetDatabase.SaveAssets();
        Debug.Log(log);
        return log;
    }

    private const string Sounds = "Assets/_Project/Audio/Sounds/Skills";

    private static GameObject Old(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{name}.prefab");

    private static SoundDefinition Sound(string name) => AssetDatabase.LoadAssetAtPath<SoundDefinition>($"{Sounds}/{name}.asset");

    private static SkillSoundCue Layer(string sound, SkillSoundPhase phase, float delay = 0f) =>
        new SkillSoundCue { sound = Sound(sound), phase = phase, delay = delay };

    private static Vector3 Hdr(float r, float g, float b, float strength) => new Vector3(r, g, b) * strength;

    // A prefab holding several effects at once (a payoff's burst: shards and a flare and a ring), each a prefab of its own inside it.
    private static GameObject Composite(string name, params GameObject[] parts)
    {
        string path = $"{Prefabs}/{name}.prefab";
        var root = new GameObject(name);
        try
        {
            foreach (GameObject part in parts)
            {
                if (part != null)
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(part, root.transform);
                    child.transform.localPosition = Vector3.zero;
                }
            }

            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // A ground impact for an ultimate: Ground Slam's (or another's) copied, grown by Scale, its element set.
    private static GroundImpactDefinition Quake(string name, string from, float scale, HazardElement element)
    {
        const string Folder = "Assets/_Project/Data/Combat/GroundImpacts";
        string path = $"{Folder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<GroundImpactDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset($"{Folder}/{from}.asset", path);
        }

        var source = new SerializedObject(AssetDatabase.LoadAssetAtPath<GroundImpactDefinition>($"{Folder}/{from}.asset"));
        var impact = AssetDatabase.LoadAssetAtPath<GroundImpactDefinition>(path);
        var data = new SerializedObject(impact);
        foreach (string field in new[] { "gustRadius", "dustRadius", "markRadius", "decalSize", "pushRadius", "stumbleRadius" })
        {
            data.FindProperty(field).floatValue = source.FindProperty(field).floatValue * scale;
        }

        data.FindProperty("gustStrength").floatValue = Mathf.Min(1.5f, source.FindProperty("gustStrength").floatValue * 1.6f);
        data.FindProperty("dust").intValue = Mathf.RoundToInt(source.FindProperty("dust").intValue * scale);
        data.FindProperty("debris").intValue = Mathf.RoundToInt(source.FindProperty("debris").intValue * scale);
        data.FindProperty("clippings").intValue = Mathf.RoundToInt(source.FindProperty("clippings").intValue * scale);
        data.FindProperty("markDepth").floatValue = Mathf.Min(0.4f, source.FindProperty("markDepth").floatValue * 1.8f);
        data.FindProperty("treeShake").floatValue = source.FindProperty("treeShake").floatValue * 2f;
        data.FindProperty("shake").floatValue = Mathf.Min(1f, source.FindProperty("shake").floatValue * 1.8f);
        data.FindProperty("element").enumValueIndex = (int)element;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(impact);
        return impact;
    }

    private static void NoLeaves(string skillName)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{skillName}.asset");
        var data = new SerializedObject(skill);
        data.FindProperty("leaves").objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void SetImpact(string skillName, GroundImpactDefinition impact)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{skillName}.asset");
        if (skill != null && skill.Attack != null)
        {
            var attack = new SerializedObject(skill.Attack);
            attack.FindProperty("groundImpact").objectReferenceValue = impact;
            attack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill.Attack);
        }
    }

    // A skill's whole look, set: sounds picked by ear are kept unless the look names its own (Cast Sound, Release Sound).
    private static string Look(string skillName, SkillLook look, bool keepCastSound = true)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{skillName}.asset");
        if (skill == null)
        {
            return "no skill " + skillName + "\n";
        }

        if (skill.Look != null)
        {
            look.castSound = look.castSound != null || !keepCastSound ? look.castSound : skill.Look.castSound;
            look.releaseSound = look.releaseSound != null ? look.releaseSound : skill.Look.releaseSound;
            if (look.releaseSoundDelay <= 0f)
            {
                look.releaseSoundDelay = skill.Look.releaseSoundDelay;
            }
        }

        // An ultimate's own rune circle and gathering power say where it lands: the flat area disc buried its wind-up (Play, 2026-11-27).
        if (skill.Kind == SkillKind.Ultimate)
        {
            look.showArea = false;
        }

        typeof(SkillDefinition).GetField("look", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(skill, look);
        EditorUtility.SetDirty(skill);
        return $"{skillName}: {look.cast.Count} cast, {look.release.Count} release, {look.struck.Count} struck, {look.sounds.Count} layers" +
               (look.moment.IsEmpty ? "" : ", a moment") + "\n";
    }

    // A skill's own look kept (Trollsbane Wrath, War Cry: dressed above), less any "Ult" cues of an earlier run, with these added.
    private static string Extend(string skillName, Action<SkillLook> add)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{skillName}.asset");
        if (skill == null || skill.Look == null)
        {
            return "no skill " + skillName + "\n";
        }

        var look = JsonUtility.FromJson<SkillLook>(JsonUtility.ToJson(skill.Look));
        foreach (List<SkillFxCue> cues in new[] { look.cast, look.release, look.struck })
        {
            cues.RemoveAll(c => c == null || c.prefab == null || c.prefab.name.StartsWith("Ult ", StringComparison.Ordinal));
        }

        look.sounds = new List<SkillSoundCue>();
        look.moment = new SkillMomentLook();
        add(look);
        return Look(skillName, look);
    }

    private static string DressMoments()
    {
        var log = new System.Text.StringBuilder();
        log.Append(DressUltimates());
        log.Append(DressNewerSkills());
        log.Append(DressPayoffs());
        // Last: the VFX pack's effects in place of ours where they are better (SkillFxPrefabs.Pack), so remaking a look keeps them.
        log.Append(DressFromPack());
        return log.ToString();
    }

    // ----- The ultimates -----

    private static string DressUltimates()
    {
        var log = new System.Text.StringBuilder();

        // ----- Fimbulwinter: the great winter called down for a breath. -----
        GameObject frostRune = Make("Ult Frost Rune", new Design("Skill Ring").T("Texture", "Rune Circle").V("Color", Hdr(0.5f, 1.3f, 3f, 1.4f))
            .F("Size", 9f).F("Lifetime", 1.3f).V("Spin", new Vector3(0f, 0f, 70f)));
        GameObject snowGather = Make("Ult Snow Gather", new Design("Skill Gather").T("Texture", "Streak").V("Color", Hdr(0.8f, 1.2f, 2f, 2f))
            .F("Rate", 260f).F("Radius", 6f).F("Size Min", 0.1f).F("Size Max", 0.28f).F("Lifetime Min", 0.5f).F("Lifetime Max", 0.9f)
            .F("Pull Speed", 9f).F("Pull Force", 18f).F("Turbulence", 2f));
        GameObject coldBreath = Make("Ult Cold Breath", new Design("Skill Mist").F("Rate", 40f).F("Radius", 2.2f).V("Color", new Vector3(0.8f, 0.92f, 1f))
            .F("Alpha", 0.45f).F("Lifetime Min", 1f).F("Lifetime Max", 1.6f).F("Size Min", 1.2f).F("Size Max", 2.2f)
            .V("Velocity Min", new Vector3(-0.3f, 0.3f, -0.3f)).V("Velocity Max", new Vector3(0.3f, 1.2f, 0.3f)));
        GameObject frostFlare = Make("Ult Frost Flare", new Design("Skill Burst").T("Texture", "Flare").V("Color", Hdr(0.7f, 0.9f, 1.2f, 6f)).F("Count", 1f)
            .V("Velocity Min", Vector3.zero).V("Velocity Max", Vector3.zero).V("Gravity", Vector3.zero).F("Size Min", 9f).F("Size Max", 9f)
            .F("Lifetime Min", 0.3f).F("Lifetime Max", 0.3f));
        GameObject frostWave = Make("Ult Frost Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(0.6f, 1f, 1.6f, 3f))
            .F("Size", 19f).F("Lifetime", 0.55f));
        GameObject frostCracks = Make("Ult Frost Cracks", new Design("Skill Ring").T("Texture", "Cracks").V("Color", Hdr(0.4f, 0.9f, 1.8f, 2.6f))
            .F("Size", 15f).F("Lifetime", 3.2f));
        GameObject giantIce = Make("Ult Giant Ice", new Design("Skill Spikes").F("Count", 4f).F("Radius", 0.9f).F("Size", 2.4f)
            .F("Lifetime Min", 2.6f).F("Lifetime Max", 3.2f).V("Scale Min", new Vector3(0.6f, 1.2f, 0.6f)).V("Scale Max", new Vector3(1.1f, 2.6f, 1.1f))
            .V("Color", new Vector3(0.5f, 0.78f, 1.05f)).F("Smoothness", 0.95f));
        GameObject iceHeart = Make("Ult Ice Heart", new Design("Skill Spikes").F("Count", 9f).F("Radius", 1.6f).F("Size", 2.8f)
            .F("Lifetime Min", 2.8f).F("Lifetime Max", 3.4f).V("Scale Min", new Vector3(0.7f, 1.4f, 0.7f)).V("Scale Max", new Vector3(1.2f, 3f, 1.2f))
            .V("Tilt Min", new Vector3(-35f, 0f, -35f)).V("Tilt Max", new Vector3(35f, 360f, 35f)).V("Color", new Vector3(0.6f, 0.85f, 1.1f)).F("Smoothness", 0.95f));
        GameObject shardStorm = Make("Ult Shard Storm", new Design("Skill Burst").T("Texture", "Shard").V("Color", Hdr(0.7f, 1.2f, 2f, 2.5f)).F("Count", 260f)
            .F("Radius", 1.2f).V("Velocity Min", new Vector3(-10f, 3f, -10f)).V("Velocity Max", new Vector3(10f, 12f, 10f)).V("Gravity", new Vector3(0f, -14f, 0f))
            .F("Drag", 0.8f).F("Size Min", 0.2f).F("Size Max", 0.5f).F("Lifetime Min", 0.8f).F("Lifetime Max", 1.6f));
        GameObject blizzard = Make("Ult Blizzard", new Design("Skill Mist").F("Rate", 110f).F("Radius", 6.5f).V("Color", new Vector3(0.88f, 0.95f, 1f))
            .F("Alpha", 0.5f).F("Lifetime Min", 1.8f).F("Lifetime Max", 2.8f).F("Size Min", 1.8f).F("Size Max", 3.4f).F("Turbulence", 2.4f)
            .V("Velocity Min", new Vector3(-2f, 0.1f, -2f)).V("Velocity Max", new Vector3(2f, 0.8f, 2f)));
        GameObject snowFall = Make("Ult Snowfall", new Design("Skill Burst").T("Texture", "Snowflake").V("Color", Hdr(1f, 1f, 1.1f, 1.6f)).F("Count", 220f)
            .F("Radius", 7f).V("Velocity Min", new Vector3(-1.2f, -1.6f, -1.2f)).V("Velocity Max", new Vector3(1.2f, 0.4f, 1.2f)).V("Gravity", new Vector3(0f, -1.2f, 0f))
            .F("Drag", 0.6f).F("Size Min", 0.12f).F("Size Max", 0.24f).F("Lifetime Min", 2f).F("Lifetime Max", 3.5f));
        GameObject frostColumn = Make("Ult Frost Column", new Design("Skill Pillar").V("Color", Hdr(0.6f, 1f, 1.8f, 3.5f)).F("Width", 3f)
            .V("Shape", new Vector3(1f, 4.5f, 1f)).V("Lift", new Vector3(0f, 6.75f, 0f)).F("Lifetime", 0.75f));
        GameObject frostHitBig = Make("Ult Frost Hit", new Design("Skill Burst").T("Texture", "Shard").V("Color", Hdr(0.8f, 1.4f, 2.4f, 2.4f)).F("Count", 40f)
            .F("Size Min", 0.12f).F("Size Max", 0.3f).V("Velocity Min", new Vector3(-4f, 1f, -4f)).V("Velocity Max", new Vector3(4f, 6f, 4f)));
        var fimbul = new SkillLook
        {
            cast =
            {
                Cue(frostRune, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.4f), Cue(snowGather, SkillFxPlace.Caster, 1, 0f, 0f, 0.7f),
                Cue(coldBreath, SkillFxPlace.CasterGround, 1, 0.05f, 0f, 1.2f),
            },
            release =
            {
                Cue(frostFlare, SkillFxPlace.Caster, 1, 0f, 0f, 0.5f), Cue(frostColumn, SkillFxPlace.CasterGround, 1, 0f, 0f, 1f),
                Cue(frostWave, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.8f), Scaled(Cue(frostWave, SkillFxPlace.CasterGround, 1, 0.14f, 0f, 0.8f), 1.3f),
                Cue(frostCracks, SkillFxPlace.CasterGround, 1, 0f, 0f, 3.4f), Cue(iceHeart, SkillFxPlace.CasterGround, 1, 0.02f, 0f, 3.6f),
                Cue(giantIce, SkillFxPlace.Ring, 14, 0.04f, 0.012f, 3.6f), Scaled(Cue(giantIce, SkillFxPlace.AcrossArc, 9, 0.02f, 0.012f, 3.6f), 0.8f),
                Cue(shardStorm, SkillFxPlace.Caster, 1, 0f, 0f, 2f), Cue(blizzard, SkillFxPlace.CasterGround, 1, 0.05f, 0f, 3.6f),
                Cue(snowFall, SkillFxPlace.Caster, 1, 0.1f, 0f, 3.8f),
            },
            struck = { Cue(frostHitBig, SkillFxPlace.Struck, 1, 0.03f, 0f, 1.2f) },
            light = new Color(0.6f, 0.85f, 1f, 1f), lightIntensity = 26f, lightRange = 24f, lightSeconds = 0.9f,
            shake = 1.4f, hitStop = 0.12f, kick = 0.3f,
            sounds =
            {
                Layer("Skill Ult Frost Rise", SkillSoundPhase.Cast), Layer("Skill Ult Frost Impact", SkillSoundPhase.Release),
                Layer("Skill Ult Frost Tail", SkillSoundPhase.Release, 0.3f),
            },
            moment = new SkillMomentLook
            {
                windUpZoom = 0.84f, slowScale = 0.3f, slowSeconds = 0.45f, punchSeconds = 0.9f, exposure = 0.5f, chromatic = 0.9f, distortion = -0.38f,
                tint = new Color(0.85f, 0.97f, 1.25f), afterglow = new Color(0.78f, 0.9f, 1.12f), afterglowStrength = 0.55f, afterglowSeconds = 3.2f,
                afterglowSaturation = -35f,
            },
        };
        log.Append(Look("Fimbulwinter", fimbul));
        NoLeaves("Fimbulwinter");
        SetImpact("Fimbulwinter", Quake("Fimbul Quake", "Ground Slam", 2.2f, HazardElement.None));

        // ----- Surtr's Brand: the fire giant's sword brought down where you land. -----
        GameObject fireRune = Make("Ult Fire Rune", new Design("Skill Ring").T("Texture", "Rune Circle").V("Color", Hdr(3f, 1.2f, 0.25f, 1.5f))
            .F("Size", 8f).F("Lifetime", 1.1f).V("Spin", new Vector3(0f, 0f, -90f)));
        GameObject emberGather = Make("Ult Ember Gather", new Design("Skill Gather").T("Texture", "Ember").V("Color", Hdr(5f, 2f, 0.4f, 1.2f))
            .F("Rate", 240f).F("Radius", 5.5f).F("Size Min", 0.06f).F("Size Max", 0.16f).F("Lifetime Min", 0.45f).F("Lifetime Max", 0.8f)
            .F("Pull Speed", 10f).F("Pull Force", 20f).F("Turbulence", 2.5f));
        GameObject heatSmoke = Make("Ult Heat Smoke", new Design("Skill Mist").F("Rate", 45f).F("Radius", 1.6f).V("Color", new Vector3(0.2f, 0.15f, 0.12f))
            .F("Alpha", 0.5f).F("Lifetime Min", 0.9f).F("Lifetime Max", 1.5f).F("Size Min", 1.2f).F("Size Max", 2.2f)
            .V("Velocity Min", new Vector3(-0.4f, 0.8f, -0.4f)).V("Velocity Max", new Vector3(0.4f, 2.2f, 0.4f)));
        GameObject fireFlare = Make("Ult Fire Flare", new Design("Skill Burst").T("Texture", "Flare").V("Color", Hdr(1.4f, 0.75f, 0.35f, 4f)).F("Count", 1f)
            .V("Velocity Min", Vector3.zero).V("Velocity Max", Vector3.zero).V("Gravity", Vector3.zero).F("Size Min", 10f).F("Size Max", 10f)
            .F("Lifetime Min", 0.32f).F("Lifetime Max", 0.32f));
        GameObject fireWave = Make("Ult Fire Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(2.4f, 1f, 0.3f, 2.4f))
            .F("Size", 18f).F("Lifetime", 0.55f));
        GameObject lavaCracks = Make("Ult Lava Cracks", new Design("Skill Ring").T("Texture", "Cracks").V("Color", Hdr(2.8f, 0.9f, 0.15f, 2.6f))
            .F("Size", 14f).F("Lifetime", 3.6f));
        GameObject fireSword = Make("Ult Fire Sword", new Design("Skill Pillar").V("Color", Hdr(2.6f, 1.1f, 0.3f, 3.2f)).F("Width", 2.2f)
            .V("Shape", new Vector3(1f, 7f, 1f)).V("Lift", new Vector3(0f, 7.7f, 0f)).F("Lifetime", 0.9f));
        GameObject fireGlow = Make("Ult Fire Glow", new Design("Skill Pillar").V("Color", Hdr(2f, 0.7f, 0.15f, 0.9f)).F("Width", 6f)
            .V("Shape", new Vector3(1f, 2.2f, 1f)).V("Lift", new Vector3(0f, 6.6f, 0f)).F("Lifetime", 1.1f));
        GameObject flameRing = Make("Ult Flame Ring", new Design("Skill Flames").F("Spawn Rate", 80f).F("Initial Size", 3.2f).F("Lifetime", 0.9f)
            .V("Effect Bounds_center", new Vector3(0f, 2.5f, 0f)).V("Effect Bounds_size", new Vector3(8f, 8f, 8f)).F("Smokiness", 0.25f));
        GameObject fireball = Make("Ult Fireball", new Design("Skill Burst").T("Texture", "Glow").V("Color", Hdr(2.5f, 0.9f, 0.18f, 1.1f)).F("Count", 60f)
            .F("Radius", 1f).V("Velocity Min", new Vector3(-8f, 2f, -8f)).V("Velocity Max", new Vector3(8f, 9f, 8f)).V("Gravity", new Vector3(0f, 1.5f, 0f))
            .F("Drag", 2.2f).F("Size Min", 0.7f).F("Size Max", 1.6f).F("Lifetime Min", 0.5f).F("Lifetime Max", 1.1f));
        GameObject emberStorm = Make("Ult Ember Storm", new Design("Skill Burst").T("Texture", "Ember").V("Color", Hdr(5f, 2f, 0.4f, 1.3f)).F("Count", 320f)
            .F("Radius", 1.5f).V("Velocity Min", new Vector3(-10f, 4f, -10f)).V("Velocity Max", new Vector3(10f, 15f, 10f)).V("Gravity", new Vector3(0f, 1f, 0f))
            .F("Drag", 1.2f).F("Size Min", 0.05f).F("Size Max", 0.13f).F("Lifetime Min", 1.5f).F("Lifetime Max", 3f));
        GameObject smokeColumn = Make("Ult Smoke Column", new Design("Skill Mist").F("Rate", 60f).F("Radius", 3f).V("Color", new Vector3(0.12f, 0.1f, 0.09f))
            .F("Alpha", 0.55f).F("Lifetime Min", 2.4f).F("Lifetime Max", 3.8f).F("Size Min", 2.2f).F("Size Max", 4.2f).F("Turbulence", 1.2f)
            .V("Velocity Min", new Vector3(-0.6f, 1f, -0.6f)).V("Velocity Max", new Vector3(0.6f, 3.2f, 0.6f)));
        GameObject fireHit = Make("Ult Fire Hit", new Design("Skill Burst").T("Texture", "Ember").V("Color", Hdr(5f, 2f, 0.4f, 1.4f)).F("Count", 40f)
            .F("Size Min", 0.06f).F("Size Max", 0.14f).V("Velocity Min", new Vector3(-3f, 1f, -3f)).V("Velocity Max", new Vector3(3f, 6f, 3f)));
        var surtr = new SkillLook
        {
            cast =
            {
                Cue(fireRune, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.2f), Cue(emberGather, SkillFxPlace.Caster, 1, 0f, 0f, 0.65f),
                Cue(heatSmoke, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.1f),
            },
            release =
            {
                Cue(fireFlare, SkillFxPlace.Caster, 1, 0f, 0f, 0.5f), Cue(fireSword, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.2f),
                Cue(fireGlow, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.3f), Cue(fireWave, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.8f),
                Scaled(Cue(fireWave, SkillFxPlace.CasterGround, 1, 0.14f, 0f, 0.8f), 1.3f), Cue(lavaCracks, SkillFxPlace.CasterGround, 1, 0f, 0f, 3.8f),
                Cue(flameRing, SkillFxPlace.Ring, 12, 0.05f, 0.02f, 2.8f), Scaled(Cue(flameRing, SkillFxPlace.AcrossArc, 8, 0.03f, 0.02f, 2.6f), 0.75f),
                Cue(fireball, SkillFxPlace.Caster, 1, 0f, 0f, 1.6f), Cue(emberStorm, SkillFxPlace.Caster, 1, 0.02f, 0f, 3.2f),
                Cue(smokeColumn, SkillFxPlace.CasterGround, 1, 0.2f, 0f, 4f),
            },
            struck = { Cue(fireHit, SkillFxPlace.Struck, 1, 0.03f, 0f, 1f) },
            light = new Color(1f, 0.55f, 0.2f, 1f), lightIntensity = 30f, lightRange = 26f, lightSeconds = 1.1f,
            shake = 1.5f, hitStop = 0.13f, kick = 0.3f,
            sounds =
            {
                Layer("Skill Ult Fire Rise", SkillSoundPhase.Cast), Layer("Skill Ult Fire Impact", SkillSoundPhase.Release),
                Layer("Skill Ult Fire Tail", SkillSoundPhase.Release, 0.35f),
            },
            moment = new SkillMomentLook
            {
                windUpZoom = 0.84f, slowScale = 0.28f, slowSeconds = 0.5f, punchSeconds = 0.9f, exposure = 0.45f, chromatic = 1f, distortion = -0.42f,
                tint = new Color(1.15f, 0.97f, 0.85f), afterglow = new Color(1.15f, 0.86f, 0.66f), afterglowStrength = 0.5f, afterglowSeconds = 2.6f,
                afterglowSaturation = 10f,
            },
        };
        log.Append(Look("SurtrsBrand", surtr));
        NoLeaves("SurtrsBrand");
        SetImpact("SurtrsBrand", Quake("Surtr Quake", "Ground Slam", 2.2f, HazardElement.Fire));

        // ----- Trollsbane Wrath: its hammer-fall, now with the ground answering for real. -----
        GameObject earthGather = Make("Ult Earth Gather", new Design("Skill Gather").T("Texture", "Ember").V("Color", Hdr(2.2f, 1.4f, 0.7f, 1.4f))
            .F("Rate", 170f).F("Radius", 4.5f).F("Size Min", 0.05f).F("Size Max", 0.12f).F("Pull Speed", 8f).F("Pull Force", 16f).F("Turbulence", 1f));
        GameObject pebbles = Make("Ult Pebbles", new Design("Skill Debris").F("Count", 28f).F("Radius", 3.5f).V("Velocity Min", new Vector3(-0.5f, 1.5f, -0.5f))
            .V("Velocity Max", new Vector3(0.5f, 3.5f, 0.5f)).F("Size Min", 0.15f).F("Size Max", 0.35f).F("Lifetime Min", 0.4f).F("Lifetime Max", 0.7f));
        GameObject earthFlare = Make("Ult Earth Flare", new Design("Skill Burst").T("Texture", "Flare").V("Color", Hdr(1.3f, 1.1f, 0.85f, 4.5f)).F("Count", 1f)
            .V("Velocity Min", Vector3.zero).V("Velocity Max", Vector3.zero).V("Gravity", Vector3.zero).F("Size Min", 8f).F("Size Max", 8f)
            .F("Lifetime Min", 0.25f).F("Lifetime Max", 0.25f));
        GameObject earthWave = Make("Ult Earth Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(1.6f, 1.3f, 1f, 2f))
            .F("Size", 15f).F("Lifetime", 0.5f));
        GameObject earthCracks = Make("Ult Earth Cracks", new Design("Skill Ring").T("Texture", "Cracks").V("Color", Hdr(2.4f, 1.1f, 0.35f, 1.6f))
            .F("Size", 12f).F("Lifetime", 2.8f));
        GameObject rockSpikes = Make("Ult Rock Spikes", new Design("Skill Spikes").F("Count", 3f).F("Radius", 0.7f).F("Size", 2f)
            .F("Lifetime Min", 1.8f).F("Lifetime Max", 2.4f).V("Scale Min", new Vector3(0.7f, 0.9f, 0.7f)).V("Scale Max", new Vector3(1.2f, 1.8f, 1.2f))
            .V("Color", new Vector3(0.36f, 0.32f, 0.28f)).F("Smoothness", 0.1f));
        GameObject dustWall = Make("Ult Dust Wall", new Design("Skill Mist").F("Rate", 70f).F("Radius", 1.4f).V("Color", new Vector3(0.5f, 0.45f, 0.38f))
            .F("Alpha", 0.6f).F("Lifetime Min", 1.2f).F("Lifetime Max", 2f).F("Size Min", 1.6f).F("Size Max", 3f)
            .V("Velocity Min", new Vector3(-1f, 0.3f, -1f)).V("Velocity Max", new Vector3(1f, 1.6f, 1f)));
        log.Append(Extend("TrollsbaneWrath", look =>
        {
            look.cast.Add(Cue(earthGather, SkillFxPlace.Caster, 1, 0f, 0f, 0.9f));
            look.cast.Add(Cue(pebbles, SkillFxPlace.CasterGround, 1, 0.35f, 0f, 1f));
            look.release.Add(Cue(earthFlare, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.4f));
            look.release.Add(Cue(earthWave, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.7f));
            look.release.Add(Scaled(Cue(earthWave, SkillFxPlace.CasterGround, 1, 0.12f, 0f, 0.7f), 1.35f));
            look.release.Add(Cue(earthCracks, SkillFxPlace.CasterGround, 1, 0f, 0f, 3f));
            look.release.Add(Cue(rockSpikes, SkillFxPlace.Ring, 12, 0.03f, 0.015f, 2.6f));
            look.release.Add(Cue(dustWall, SkillFxPlace.Ring, 10, 0.05f, 0.01f, 2.2f));
            look.light = new Color(1f, 0.82f, 0.6f, 1f);
            look.lightIntensity = 20f;
            look.lightRange = 18f;
            look.lightSeconds = 0.6f;
            look.shake = 1.3f;
            look.hitStop = 0.11f;
            look.kick = 0.3f;
            look.sounds.Add(Layer("Skill Ult Earth Rise", SkillSoundPhase.Cast));
            look.sounds.Add(Layer("Skill Ult Earth Impact", SkillSoundPhase.Release));
            look.sounds.Add(Layer("Skill Ult Earth Tail", SkillSoundPhase.Release, 0.25f));
            look.moment = new SkillMomentLook
            {
                windUpZoom = 0.86f, slowScale = 0.32f, slowSeconds = 0.4f, punchSeconds = 0.8f, exposure = 0.5f, chromatic = 0.7f, distortion = -0.35f,
                tint = new Color(1.12f, 1f, 0.9f), afterglow = new Color(1.08f, 0.98f, 0.9f), afterglowStrength = 0.35f, afterglowSeconds = 1.6f,
                afterglowSaturation = -15f,
            };
        }));
        SetImpact("TrollsbaneWrath", Quake("Wrath Quake", "Ground Slam", 2f, HazardElement.None));

        // ----- War Cry: the roar rolls out in waves, with the horn and the drum under it. -----
        GameObject cryWave = Make("Ult Cry Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(2.2f, 0.8f, 0.45f, 1.6f))
            .F("Size", 16f).F("Lifetime", 0.6f));
        GameObject cryFlare = Make("Ult Cry Flare", new Design("Skill Burst").T("Texture", "Flare").V("Color", Hdr(1.4f, 0.7f, 0.45f, 3f)).F("Count", 1f)
            .V("Velocity Min", Vector3.zero).V("Velocity Max", Vector3.zero).V("Gravity", Vector3.zero).F("Size Min", 6f).F("Size Max", 6f)
            .F("Lifetime Min", 0.25f).F("Lifetime Max", 0.25f));
        log.Append(Extend("WarCry", look =>
        {
            look.release.Add(Cue(cryFlare, SkillFxPlace.Caster, 1, 0.55f, 0f, 0.5f));
            look.release.Add(Cue(cryWave, SkillFxPlace.CasterGround, 1, 0.55f, 0f, 0.8f));
            look.release.Add(Scaled(Cue(cryWave, SkillFxPlace.CasterGround, 1, 0.72f, 0f, 0.8f), 1.25f));
            look.release.Add(Scaled(Cue(cryWave, SkillFxPlace.CasterGround, 1, 0.9f, 0f, 0.8f), 1.5f));
            look.shake = 0.9f;
            look.kick = 0.15f;
            look.sounds.Add(Layer("Skill War Horn", SkillSoundPhase.Release, 0.45f));
            look.moment = new SkillMomentLook
            {
                windUpZoom = 0.9f, impactDelay = 0.55f, punchSeconds = 0.75f, exposure = 0.35f, chromatic = 0.65f, distortion = -0.32f,
                tint = new Color(1.2f, 0.88f, 0.82f),
            };
        }));
        return log.ToString();
    }

    // ----- The newer skills: grounded -----

    private static string DressNewerSkills()
    {
        var log = new System.Text.StringBuilder();
        GameObject slash = Old("Phys Slash"), windRing = Old("Phys Wind Ring"), dust = Old("Phys Dust"), debris = Old("Phys Debris"), flash = Old("Phys Flash");
        GameObject sparks = Old("Phys Hit Sparks"), streaksBack = Old("Phys Speed Lines"), streaksAhead = Old("Phys Throw Lines"), smoke = Old("Phys Smoke Puff");
        GameObject punchRing = Old("Phys Punch Ring"), bloodHit = Old("Phys Blood Hit");
        GameObject splinters = Make("Phys Splinters", new Design("Skill Debris").F("Count", 16f).F("Radius", 0.4f).V("Color", new Vector3(0.45f, 0.3f, 0.18f))
            .V("Velocity Min", new Vector3(-3f, 2f, -1f)).V("Velocity Max", new Vector3(3f, 6f, 5f)).F("Size Min", 0.15f).F("Size Max", 0.35f));
        GameObject bloodSpray = Make("Phys Blood Spray", new Design("Skill Burst").T("Texture", "Glow").V("Color", new Vector3(0.75f, 0.05f, 0.04f)).F("Count", 36f)
            .V("Velocity Min", new Vector3(-2.5f, 1f, 1f)).V("Velocity Max", new Vector3(2.5f, 4f, 6f)).V("Gravity", new Vector3(0f, -14f, 0f))
            .F("Size Min", 0.08f).F("Size Max", 0.2f).F("Lifetime Min", 0.4f).F("Lifetime Max", 0.8f));
        GameObject smallCracks = Make("Phys Small Cracks", new Design("Skill Ring").T("Texture", "Cracks").V("Color", Hdr(1.6f, 1.2f, 0.8f, 0.8f))
            .F("Size", 4.5f).F("Lifetime", 1.6f));
        GameObject earthSpikesSmall = Make("Phys Rock Spikes", new Design("Skill Spikes").F("Count", 2f).F("Radius", 0.5f).F("Size", 1.3f)
            .F("Lifetime Min", 1.2f).F("Lifetime Max", 1.6f).V("Color", new Vector3(0.36f, 0.32f, 0.28f)).F("Smoothness", 0.1f));
        GameObject thrustLine = Make("Phys Thrust Line", new Design("Skill Burst").T("Texture", "Streak").V("Color", Hdr(1.6f, 1.6f, 1.7f, 1.2f)).F("Count", 10f)
            .F("Radius", 0.2f).V("Velocity Min", new Vector3(-0.2f, 0f, 14f)).V("Velocity Max", new Vector3(0.2f, 0.3f, 20f)).V("Gravity", Vector3.zero)
            .F("Drag", 4f).F("Size Min", 0.35f).F("Size Max", 0.6f).F("Lifetime Min", 0.15f).F("Lifetime Max", 0.22f));

        Func<float, float, float, SkillLook> weight = (stop, push, shake) => new SkillLook { hitStop = stop, kick = push, shake = shake };

        SkillLook crossing = weight(0.05f, 0.1f, 0.25f);
        crossing.cast.Add(Cue(streaksBack, SkillFxPlace.Caster, 1, 0f, 0f, 0.4f));
        crossing.release.Add(Cue(slash, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.3f));
        crossing.release.Add(Scaled(Cue(slash, SkillFxPlace.CasterGround, 1, 0.08f, 0f, 0.3f), 1.1f));
        crossing.struck.Add(Cue(bloodSpray, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        crossing.struck.Add(Cue(sparks, SkillFxPlace.Struck, 1, 0f, 0f, 0.7f));
        log.Append(Look("CrossingCut", crossing));

        SkillLook vein = weight(0.05f, 0.1f, 0.2f);
        vein.cast.Add(Cue(smoke, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.45f));
        vein.release.Add(Scaled(Cue(slash, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.3f), 0.7f));
        vein.struck.Add(Cue(bloodSpray, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        vein.struck.Add(Cue(bloodHit, SkillFxPlace.Struck, 1, 0f, 0f, 0.7f));
        log.Append(Look("OpenTheVein", vein));

        SkillLook splitter = weight(0.07f, 0.15f, 0.35f);
        splitter.release.Add(Cue(flash, SkillFxPlace.AlongReach, 1, 0f, 0f, 0.25f));
        splitter.release.Add(Cue(dust, SkillFxPlace.AcrossArc, 2, 0.02f, 0.02f, 0.6f));
        splitter.struck.Add(Cue(splinters, SkillFxPlace.Struck, 1, 0f, 0f, 1f));
        splitter.struck.Add(Cue(sparks, SkillFxPlace.Struck, 1, 0f, 0f, 0.7f));
        splitter.releaseSound = Sound("Payoff Sunder");
        log.Append(Look("ShieldSplitter", splitter, keepCastSound: false));

        SkillLook reaping = weight(0.05f, 0.12f, 0.3f);
        reaping.release.Add(Scaled(Cue(slash, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.35f), 1.6f));
        reaping.release.Add(Scaled(Cue(windRing, SkillFxPlace.CasterGround, 1, 0.02f, 0f, 0.4f), 1.1f));
        reaping.release.Add(Cue(dust, SkillFxPlace.AcrossArc, 5, 0.03f, 0.015f, 0.6f));
        reaping.struck.Add(Cue(bloodSpray, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        log.Append(Look("ReapingArc", reaping));

        SkillLook headsman = weight(0.08f, 0.2f, 0.55f);
        headsman.cast.Add(Cue(dust, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.6f));
        headsman.release.Add(Cue(flash, SkillFxPlace.AlongReach, 1, 0f, 0f, 0.25f));
        headsman.release.Add(Cue(smallCracks, SkillFxPlace.AlongReach, 1, 0f, 0f, 1.8f));
        headsman.release.Add(Cue(debris, SkillFxPlace.AlongReach, 1, 0f, 0f, 1.2f));
        headsman.release.Add(Cue(dust, SkillFxPlace.AcrossArc, 4, 0.02f, 0.01f, 0.7f));
        headsman.struck.Add(Cue(sparks, SkillFxPlace.Struck, 1, 0f, 0f, 0.8f));
        headsman.releaseSound = Sound("Skill Leap Land");
        log.Append(Look("HeadsmansDrop", headsman, keepCastSound: false));

        SkillLook earthbreaker = weight(0.08f, 0.2f, 0.6f);
        earthbreaker.cast.Add(Cue(dust, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.6f));
        earthbreaker.release.Add(Cue(flash, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.25f));
        earthbreaker.release.Add(Scaled(Cue(smallCracks, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.8f), 1.5f));
        earthbreaker.release.Add(Cue(debris, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.3f));
        earthbreaker.release.Add(Cue(earthSpikesSmall, SkillFxPlace.Ring, 6, 0.02f, 0.02f, 1.8f));
        earthbreaker.release.Add(Cue(dust, SkillFxPlace.Ring, 8, 0.03f, 0f, 0.8f));
        earthbreaker.release.Add(Cue(windRing, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.4f));
        earthbreaker.struck.Add(Cue(sparks, SkillFxPlace.Struck, 1, 0f, 0f, 0.8f));
        earthbreaker.releaseSound = Sound("Skill Wrath Slam");
        log.Append(Look("Earthbreaker", earthbreaker, keepCastSound: false));

        SkillLook through = weight(0.05f, 0.14f, 0.25f);
        through.cast.Add(Cue(streaksBack, SkillFxPlace.Caster, 1, 0f, 0f, 0.4f));
        through.release.Add(Cue(thrustLine, SkillFxPlace.Caster, 1, 0f, 0f, 0.3f));
        through.release.Add(Cue(punchRing, SkillFxPlace.AlongReach, 1, 0.03f, 0f, 0.3f));
        through.release.Add(Cue(dust, SkillFxPlace.Trail, 3, 0f, 0.02f, 0.6f));
        through.struck.Add(Cue(bloodSpray, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        log.Append(Look("RunThrough", through));

        SkillLook ribs = weight(0.06f, 0.12f, 0.3f);
        ribs.release.Add(Cue(punchRing, SkillFxPlace.AlongReach, 1, 0f, 0f, 0.3f));
        ribs.release.Add(Cue(flash, SkillFxPlace.AlongReach, 1, 0f, 0f, 0.2f));
        ribs.release.Add(Cue(dust, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.5f));
        ribs.struck.Add(Cue(sparks, SkillFxPlace.Struck, 1, 0f, 0f, 0.7f));
        log.Append(Look("RibBreaker", ribs));

        SkillLook snare = weight(0f, 0.08f, 0.15f);
        snare.cast.Add(Cue(dust, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.5f));
        snare.release.Add(Cue(streaksAhead, SkillFxPlace.Caster, 1, 0f, 0f, 0.4f));
        snare.struck.Add(Cue(dust, SkillFxPlace.Struck, 1, 0f, 0f, 0.7f));
        log.Append(Look("HuntersSnare", snare));

        // The school powers: their colour, gathered a little, landing with a flash.
        GameObject emberCircle = Old("Ember Cast Circle"), emberSparks = Old("Ember Hand Sparks"), emberHit = Old("Ember Hit");
        GameObject frostCircle = Old("Frost Cast Circle"), frostMist = Old("Frost Mist"), frostHit = Old("Frost Hit"), wyrdCircle = Old("Wyrd Cast Circle");
        GameObject kindleSpray = Make("Kindle Spray", new Design("Skill Burst").T("Texture", "Ember").V("Color", Hdr(5f, 2f, 0.45f, 1f)).F("Count", 40f)
            .V("Velocity Min", new Vector3(-1f, 0.4f, 2f)).V("Velocity Max", new Vector3(1f, 1.6f, 6f)).V("Gravity", new Vector3(0f, -1f, 0f))
            .F("Size Min", 0.05f).F("Size Max", 0.1f).F("Lifetime Min", 0.3f).F("Lifetime Max", 0.6f));
        SkillLook kindling = weight(0f, 0.06f, 0.1f);
        kindling.cast.Add(Cue(emberSparks, SkillFxPlace.Caster, 1, 0f, 0f, 0.7f));
        kindling.release.Add(Cue(kindleSpray, SkillFxPlace.Caster, 1, 0f, 0f, 0.7f));
        kindling.struck.Add(Cue(emberHit, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        kindling.light = new Color(1f, 0.55f, 0.2f, 1f);
        kindling.lightIntensity = 4f;
        kindling.lightRange = 6f;
        log.Append(Look("KindlingBolt", kindling));

        GameObject wildGather = Make("Wildfire Gather", new Design("Skill Gather").T("Texture", "Ember").V("Color", Hdr(5f, 2f, 0.4f, 1f))
            .F("Rate", 110f).F("Radius", 3.2f).F("Size Min", 0.05f).F("Size Max", 0.12f).F("Pull Speed", 7f).F("Pull Force", 14f));
        GameObject wildFlames = Make("Wildfire Flames", new Design("Skill Flames").F("Spawn Rate", 60f).F("Initial Size", 2.4f).F("Lifetime", 0.8f)
            .V("Effect Bounds_center", new Vector3(0f, 2f, 0f)).V("Effect Bounds_size", new Vector3(6f, 6f, 6f)).F("Smokiness", 0.25f));
        GameObject wildWave = Make("Wildfire Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(2.4f, 1f, 0.3f, 1.4f))
            .F("Size", 12f).F("Lifetime", 0.45f));
        SkillLook wildfire = weight(0.05f, 0.1f, 0.45f);
        wildfire.cast.Add(Cue(emberCircle, SkillFxPlace.CasterGround, 1, 0f, 0f, 1f));
        wildfire.cast.Add(Cue(wildGather, SkillFxPlace.Caster, 1, 0f, 0f, 0.5f));
        wildfire.release.Add(Cue(wildWave, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.6f));
        wildfire.release.Add(Cue(wildFlames, SkillFxPlace.Ring, 8, 0.03f, 0.02f, 1.8f));
        wildfire.release.Add(Cue(kindleSpray, SkillFxPlace.Caster, 1, 0f, 0f, 0.7f));
        wildfire.struck.Add(Cue(emberHit, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        wildfire.light = new Color(1f, 0.55f, 0.2f, 1f);
        wildfire.lightIntensity = 10f;
        wildfire.lightRange = 12f;
        wildfire.lightSeconds = 0.5f;
        log.Append(Look("Wildfire", wildfire));

        GameObject hoarWave = Make("Hoarfrost Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(0.6f, 1f, 1.6f, 1.6f))
            .F("Size", 11f).F("Lifetime", 0.5f));
        GameObject hoarFlakes = Make("Hoarfrost Flakes", new Design("Skill Burst").T("Texture", "Snowflake").V("Color", Hdr(1f, 1f, 1.1f, 1.4f)).F("Count", 70f)
            .F("Radius", 1f).V("Velocity Min", new Vector3(-5f, 0.5f, -5f)).V("Velocity Max", new Vector3(5f, 2f, 5f)).V("Gravity", new Vector3(0f, -1.5f, 0f))
            .F("Drag", 1.4f).F("Size Min", 0.1f).F("Size Max", 0.2f).F("Lifetime Min", 0.8f).F("Lifetime Max", 1.4f));
        SkillLook hoar = weight(0.04f, 0.08f, 0.3f);
        hoar.cast.Add(Cue(frostCircle, SkillFxPlace.CasterGround, 1, 0f, 0f, 1f));
        hoar.release.Add(Cue(hoarWave, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.6f));
        hoar.release.Add(Scaled(Cue(Old("Frost Spikes"), SkillFxPlace.Ring, 10, 0.02f, 0.015f, 1.8f), 0.9f));
        hoar.release.Add(Cue(frostMist, SkillFxPlace.Ring, 6, 0.04f, 0f, 1.4f));
        hoar.release.Add(Cue(hoarFlakes, SkillFxPlace.Caster, 1, 0f, 0f, 1.6f));
        hoar.struck.Add(Cue(frostHit, SkillFxPlace.Struck, 1, 0f, 0f, 0.9f));
        hoar.light = new Color(0.6f, 0.85f, 1f, 1f);
        hoar.lightIntensity = 9f;
        hoar.lightRange = 11f;
        log.Append(Look("Hoarfrost", hoar));

        GameObject elfGather = Make("Elf Motes", new Design("Skill Gather").T("Texture", "Glow").V("Color", Hdr(0.8f, 2.4f, 0.8f, 1.2f))
            .F("Rate", 70f).F("Radius", 2.2f).F("Size Min", 0.05f).F("Size Max", 0.1f).F("Pull Speed", 5f).F("Pull Force", 10f));
        GameObject elfFlare = Make("Elf Flare", new Design("Skill Burst").T("Texture", "Flare").V("Color", Hdr(0.7f, 1.6f, 0.7f, 2.4f)).F("Count", 1f)
            .V("Velocity Min", Vector3.zero).V("Velocity Max", Vector3.zero).V("Gravity", Vector3.zero).F("Size Min", 2.6f).F("Size Max", 2.6f)
            .F("Lifetime Min", 0.2f).F("Lifetime Max", 0.2f));
        SkillLook elf = weight(0.03f, 0.06f, 0.1f);
        elf.cast.Add(Cue(wyrdCircle, SkillFxPlace.CasterGround, 1, 0f, 0f, 1f));
        elf.cast.Add(Cue(elfGather, SkillFxPlace.Caster, 1, 0f, 0f, 0.6f));
        elf.release.Add(Cue(elfFlare, SkillFxPlace.Caster, 1, 0f, 0f, 0.4f));
        elf.struck.Add(Cue(elfFlare, SkillFxPlace.Struck, 1, 0f, 0f, 0.4f));
        log.Append(Look("ElfShot", elf));

        // Raven's Mark: feathers drawn in, loosed down the line, and a dark mark where it strikes.
        GameObject featherGather = Make("Raven Feathers Gather", new Design("Skill Gather").T("Texture", "Feather").V("Color", Hdr(0.55f, 0.35f, 1f, 1.1f))
            .F("Rate", 40f).F("Radius", 2.6f).F("Size Min", 0.25f).F("Size Max", 0.45f).F("Pull Speed", 4f).F("Pull Force", 8f).F("Turbulence", 1.5f));
        GameObject featherLoose = Make("Raven Feathers", new Design("Skill Burst").T("Texture", "Feather").V("Color", Hdr(0.5f, 0.3f, 0.9f, 1.4f)).F("Count", 36f)
            .F("Radius", 0.4f).V("Velocity Min", new Vector3(-1.2f, -0.2f, 8f)).V("Velocity Max", new Vector3(1.2f, 1f, 16f)).V("Gravity", new Vector3(0f, -1f, 0f))
            .F("Drag", 1.6f).F("Size Min", 0.25f).F("Size Max", 0.5f).F("Lifetime Min", 0.6f).F("Lifetime Max", 1f));
        GameObject ravenMark = Make("Raven Mark", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(0.7f, 0.35f, 1.3f, 1.8f))
            .F("Size", 3.2f).F("Lifetime", 0.45f));
        SkillLook raven = weight(0.05f, 0.08f, 0.2f);
        raven.castSound = Sound("Skill Raven Caw");
        raven.cast.Add(Cue(featherGather, SkillFxPlace.Caster, 1, 0f, 0f, 0.7f));
        raven.release.Add(Cue(featherLoose, SkillFxPlace.Caster, 1, 0f, 0f, 1.2f));
        raven.release.Add(Cue(thrustLine, SkillFxPlace.Caster, 1, 0f, 0f, 0.3f));
        raven.struck.Add(Cue(ravenMark, SkillFxPlace.Struck, 1, 0f, 0f, 0.6f));
        raven.struck.Add(Cue(featherLoose, SkillFxPlace.Struck, 1, 0f, 0f, 1.1f));
        raven.light = new Color(0.6f, 0.35f, 1f, 1f);
        raven.lightIntensity = 5f;
        raven.lightRange = 8f;
        raven.releaseSound = Sound("Skill Lunge");
        log.Append(Look("RavensMark", raven));

        // Bear-Hide: the bear's spirit settling over the shoulders, amber, with a growl.
        GameObject bearAura = Make("Bear Aura", new Design("Skill Mist").T("Texture", "Glow").V("Flipbook Size", new Vector3(1f, 1f, 0f)).F("Rate", 50f)
            .F("Radius", 0.8f).V("Color", Hdr(2.5f, 1.4f, 0.5f, 1f)).F("Alpha", 0.6f).F("Lifetime Min", 0.6f).F("Lifetime Max", 1f).F("Size Min", 0.25f)
            .F("Size Max", 0.5f).V("Velocity Min", new Vector3(-0.2f, 0.4f, -0.2f)).V("Velocity Max", new Vector3(0.2f, 1.2f, 0.2f)));
        GameObject bearWave = Make("Bear Wave", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(2.2f, 1.3f, 0.5f, 1.2f))
            .F("Size", 6f).F("Lifetime", 0.45f));
        SkillLook bear = weight(0f, 0f, 0.35f);
        bear.castSound = Sound("Skill Bear Growl");
        bear.cast.Add(Cue(dust, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.6f));
        bear.release.Add(Cue(bearWave, SkillFxPlace.CasterGround, 1, 0.4f, 0f, 0.6f));
        bear.release.Add(Cue(bearAura, SkillFxPlace.Caster, 1, 0.3f, 0f, 3f));
        bear.light = new Color(1f, 0.65f, 0.3f, 1f);
        bear.lightIntensity = 4f;
        bear.lightRange = 6f;
        bear.releaseSound = Sound("Skill Leap Land");
        bear.releaseSoundDelay = 0.4f;
        log.Append(Look("BearHide", bear));
        return log.ToString();
    }

    // ----- The payoffs: each status bursts as it is used up -----

    private static string DressPayoffs()
    {
        var log = new System.Text.StringBuilder();
        GameObject Flare(string name, Vector3 colour, float size) => Make(name, new Design("Skill Burst").T("Texture", "Flare").V("Color", colour).F("Count", 1f)
            .V("Velocity Min", Vector3.zero).V("Velocity Max", Vector3.zero).V("Gravity", Vector3.zero).F("Size Min", size).F("Size Max", size)
            .F("Lifetime Min", 0.22f).F("Lifetime Max", 0.22f));

        GameObject bleed = Composite("Payoff Bleed",
            Make("Payoff Blood Burst", new Design("Skill Burst").T("Texture", "Glow").V("Color", new Vector3(0.8f, 0.04f, 0.03f)).F("Count", 70f).F("Radius", 0.3f)
                .V("Velocity Min", new Vector3(-4f, 1f, -4f)).V("Velocity Max", new Vector3(4f, 6f, 4f)).V("Gravity", new Vector3(0f, -15f, 0f))
                .F("Size Min", 0.1f).F("Size Max", 0.26f).F("Lifetime Min", 0.5f).F("Lifetime Max", 0.9f)),
            Flare("Payoff Blood Flare", Hdr(1.4f, 0.3f, 0.25f, 3f), 3f));
        GameObject sunder = Composite("Payoff Sunder",
            Make("Payoff Steel Shards", new Design("Skill Burst").T("Texture", "Shard").V("Color", Hdr(1.6f, 1.6f, 1.7f, 1.6f)).F("Count", 40f).F("Radius", 0.3f)
                .V("Velocity Min", new Vector3(-5f, 1f, -5f)).V("Velocity Max", new Vector3(5f, 6f, 5f)).F("Size Min", 0.12f).F("Size Max", 0.28f)
                .F("Lifetime Min", 0.4f).F("Lifetime Max", 0.8f)),
            Old("Phys Hit Sparks"), Flare("Payoff Steel Flare", Hdr(1.4f, 1.3f, 1.1f, 3f), 3.2f));
        GameObject roots = Composite("Payoff Roots",
            Make("Payoff Root Splinters", new Design("Skill Debris").F("Count", 18f).F("Radius", 0.5f).V("Color", new Vector3(0.4f, 0.28f, 0.16f))
                .V("Velocity Min", new Vector3(-4f, 2f, -4f)).V("Velocity Max", new Vector3(4f, 7f, 4f)).F("Size Min", 0.2f).F("Size Max", 0.45f)),
            Old("Phys Dust"), Flare("Payoff Earth Flare", Hdr(1.3f, 1.1f, 0.8f, 2.6f), 3f));
        GameObject cowed = Composite("Payoff Cowed",
            Make("Payoff Dread Ring", new Design("Skill Ring").T("Texture", "Shockwave").V("Color", Hdr(0.8f, 0.35f, 1.3f, 1.8f)).F("Size", 4.5f).F("Lifetime", 0.4f)),
            Flare("Payoff Dread Flare", Hdr(0.9f, 0.5f, 1.4f, 2.6f), 3f));
        GameObject burn = Composite("Payoff Burn",
            Make("Payoff Fire Column", new Design("Skill Pillar").V("Color", Hdr(2.6f, 1.1f, 0.3f, 2.4f)).F("Width", 1.6f).V("Shape", new Vector3(1f, 3f, 1f))
                .V("Lift", new Vector3(0f, 2.4f, 0f)).F("Lifetime", 0.6f)),
            Make("Payoff Fire Burst", new Design("Skill Burst").T("Texture", "Glow").V("Color", Hdr(2.5f, 0.9f, 0.18f, 1.6f)).F("Count", 45f).F("Radius", 0.4f)
                .V("Velocity Min", new Vector3(-4f, 1f, -4f)).V("Velocity Max", new Vector3(4f, 6f, 4f)).V("Gravity", new Vector3(0f, 1.5f, 0f)).F("Drag", 2.2f)
                .F("Size Min", 0.4f).F("Size Max", 0.9f).F("Lifetime Min", 0.4f).F("Lifetime Max", 0.8f)),
            Old("Ember Hit"));

        var payoffs = new (string status, GameObject look, string sound)[]
        {
            ("Rend", bleed, "Payoff Bleed"), ("Sundered", sunder, "Payoff Sunder"), ("Roots", roots, "Payoff Roots"), ("Cowed", cowed, "Payoff Cowed"),
            ("Burning", burn, "Payoff Burn"),
        };
        foreach ((string name, GameObject look, string sound) in payoffs)
        {
            var status = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>($"Assets/_Project/Data/Combat/Status/{name}.asset");
            if (status == null)
            {
                log.AppendLine("no status " + name);
                continue;
            }

            var data = new SerializedObject(status);
            data.FindProperty("consumeLook").objectReferenceValue = look;
            data.FindProperty("consumeSound").objectReferenceValue = Sound(sound);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(status);
            log.AppendLine($"payoff {name}: {look.name}, {sound}");
        }

        return log.ToString();
    }
}
