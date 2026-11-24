using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// The first skills dressed with effects (Docs/CombatSkillsResearch_2026-11-24.md §8, slice 1): Frost Rune, the Ash Lad's Ember and
/// Trollsbane Wrath. Makes the effect prefabs (a VisualEffect on a shared graph, its look in VfxValues) under Art/Effects/Skills/Prefabs,
/// the statuses the schools own (Frozen, Burning), and sets each skill's SkillLook and its attack's statuses. It owns only those: the
/// prefabs and statuses it makes, the three skills' Look, and their attacks' status fields (and Frost Rune's arc, a lance now).
/// Run again after changing a design here. Safe Haven › Tools › Effects › Dress the First Skills.
/// </summary>
public static class SkillFxPrefabs
{
    private const string Prefabs = "Assets/_Project/Art/Effects/Skills/Prefabs";
    private const string Graphs = SkillVfxArchetypes.Folder;
    private const string Textures = SkillVfxArchetypes.Textures;

    private sealed class Design
    {
        public string Graph;
        public readonly List<VfxValues.FloatValue> Floats = new List<VfxValues.FloatValue>();
        public readonly List<VfxValues.VectorValue> Vectors = new List<VfxValues.VectorValue>();
        public readonly List<VfxValues.ObjectValue> Objects = new List<VfxValues.ObjectValue>();

        public Design(string graph) => Graph = graph;

        public Design F(string name, float value)
        {
            Floats.Add(new VfxValues.FloatValue { name = name, value = value });
            return this;
        }

        public Design V(string name, Vector3 value)
        {
            Vectors.Add(new VfxValues.VectorValue { name = name, value = value });
            return this;
        }

        public Design T(string name, string texture)
        {
            Objects.Add(new VfxValues.ObjectValue { name = name, value = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/" + texture + ".png") });
            return this;
        }
    }

    [MenuItem("Safe Haven/Tools/Effects/Dress the First Skills")]
    public static string DressAll()
    {
        if (!AssetDatabase.IsValidFolder(Prefabs))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Art/Effects/Skills", "Prefabs");
        }

        var log = new System.Text.StringBuilder();
        StatusEffectDefinition frozen = Status("Frozen", "Held fast in ice: it can't move until it thaws.", new Color(0.8f, 0.93f, 1f), 1.5f, 0f,
            DamageType.Frost, 0f, refreshes: false, restSeconds: 4f);
        StatusEffectDefinition burning = Status("Burning", "Alight: it burns a while.", new Color(1f, 0.5f, 0.15f), 4f, 3f, DamageType.Fire, 1f,
            refreshes: true, restSeconds: 0f);
        StatusEffectDefinition chill = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Chill.asset");

        // ----- Frost Rune: a lance of ice racing out along the aim. -----
        GameObject frostCircle = Make("Frost Cast Circle", new Design("Skill Ring").T("Texture", "Rune Circle").V("Color", new Vector3(0.5f, 1.4f, 3f))
            .F("Size", 3f).F("Lifetime", 0.9f).V("Spin", new Vector3(0f, 0f, 45f)));
        GameObject frostHands = Make("Frost Hand Mist", new Design("Skill Mist").F("Rate", 36f).F("Radius", 0.3f).F("Size Min", 0.2f).F("Size Max", 0.45f)
            .F("Lifetime Min", 0.5f).F("Lifetime Max", 0.9f).V("Color", new Vector3(0.75f, 0.9f, 1f)).F("Alpha", 0.55f)
            .V("Velocity Min", new Vector3(-0.2f, 0.2f, -0.2f)).V("Velocity Max", new Vector3(0.2f, 0.6f, 0.2f)));
        GameObject frostSpikes = Make("Frost Spikes", new Design("Skill Spikes").F("Count", 3f).F("Radius", 0.4f).F("Size", 1.3f)
            .F("Lifetime Min", 1.2f).F("Lifetime Max", 1.5f).V("Color", new Vector3(0.45f, 0.72f, 1f)).F("Smoothness", 0.92f));
        GameObject frostShards = Make("Frost Shards", new Design("Skill Burst").T("Texture", "Shard").V("Color", new Vector3(1f, 2.2f, 4f)).F("Count", 18f)
            .V("Velocity Min", new Vector3(-2.5f, 2f, -2.5f)).V("Velocity Max", new Vector3(2.5f, 5f, 2.5f)).F("Size Min", 0.1f).F("Size Max", 0.22f)
            .F("Lifetime Min", 0.5f).F("Lifetime Max", 0.9f));
        GameObject frostMist = Make("Frost Mist", new Design("Skill Mist").F("Rate", 22f).F("Radius", 0.7f).V("Color", new Vector3(0.85f, 0.95f, 1f))
            .F("Alpha", 0.3f).F("Lifetime Min", 1f).F("Lifetime Max", 1.6f));
        GameObject frostHit = Make("Frost Hit", new Design("Skill Burst").T("Texture", "Shard").V("Color", new Vector3(1.2f, 2.4f, 4f)).F("Count", 24f)
            .F("Size Min", 0.08f).F("Size Max", 0.18f));
        var frost = new SkillLook
        {
            cast = { Cue(frostCircle, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.1f), Cue(frostHands, SkillFxPlace.Caster, 1, 0f, 0f, 0.8f) },
            release =
            {
                Cue(frostSpikes, SkillFxPlace.AlongReach, 6, 0f, 0.05f, 1.8f), Cue(frostShards, SkillFxPlace.AlongReach, 6, 0f, 0.05f, 1.2f),
                Cue(frostMist, SkillFxPlace.AlongReach, 3, 0.05f, 0.08f, 1.5f),
            },
            struck = { Cue(frostHit, SkillFxPlace.Struck, 1, 0.05f, 0f, 1f) },
            light = new Color(0.55f, 0.8f, 1f, 1f), lightIntensity = 6f, lightRange = 8f, lightSeconds = 0.3f, shake = 0.25f,
        };
        log.AppendLine(Dress("FrostRune", frost, chill, frozen, arc: 25f));

        // ----- The Ash Lad's Ember: a roaring cone of embers and flame. -----
        GameObject emberCircle = Make("Ember Cast Circle", new Design("Skill Ring").T("Texture", "Rune Circle").V("Color", new Vector3(3f, 1.3f, 0.3f))
            .F("Size", 2.6f).F("Lifetime", 0.8f).V("Spin", new Vector3(0f, 0f, -50f)));
        GameObject emberHands = Make("Ember Hand Sparks", new Design("Skill Burst").T("Texture", "Ember").V("Color", new Vector3(4f, 1.6f, 0.4f)).F("Count", 24f)
            .V("Velocity Min", new Vector3(-0.6f, 0.4f, -0.6f)).V("Velocity Max", new Vector3(0.6f, 1.6f, 0.6f)).V("Gravity", new Vector3(0f, 1f, 0f))
            .F("Size Min", 0.03f).F("Size Max", 0.07f).F("Lifetime Min", 0.4f).F("Lifetime Max", 0.8f));
        GameObject emberSpray = Make("Ember Spray", new Design("Skill Burst").T("Texture", "Ember").V("Color", new Vector3(5f, 2f, 0.45f)).F("Count", 140f)
            .V("Velocity Min", new Vector3(-2.2f, 0.5f, 3f)).V("Velocity Max", new Vector3(2.2f, 2.5f, 9f)).V("Gravity", new Vector3(0f, -2f, 0f))
            .F("Drag", 1f).F("Size Min", 0.07f).F("Size Max", 0.15f).F("Lifetime Min", 0.45f).F("Lifetime Max", 0.85f));
        // The roar itself: soft hot streaks thrown forward, rising and slowing, over the whole cone.
        GameObject emberBreath = Make("Ember Breath", new Design("Skill Burst").T("Texture", "Glow").V("Color", new Vector3(2f, 0.5f, 0.06f)).F("Count", 110f)
            .V("Velocity Min", new Vector3(-2.4f, 0.2f, 4f)).V("Velocity Max", new Vector3(2.4f, 1.5f, 10f)).V("Gravity", new Vector3(0f, 1.5f, 0f))
            .F("Drag", 2.2f).F("Size Min", 0.35f).F("Size Max", 0.8f).F("Lifetime Min", 0.5f).F("Lifetime Max", 0.85f));
        // Small fires left where it struck the ground (Unity's sample flames, a campfire's size).
        GameObject emberFlames = Make("Ember Flames", new Design("Skill Flames").F("Spawn Rate", 60f).F("Initial Size", 1.2f).F("Lifetime", 0.8f));
        GameObject emberHit = Make("Ember Hit", new Design("Skill Burst").T("Texture", "Ember").V("Color", new Vector3(4f, 1.5f, 0.35f)).F("Count", 22f)
            .F("Size Min", 0.04f).F("Size Max", 0.08f));
        var ember = new SkillLook
        {
            cast = { Cue(emberCircle, SkillFxPlace.CasterGround, 1, 0f, 0f, 1f), Cue(emberHands, SkillFxPlace.Caster, 1, 0f, 0f, 0.9f) },
            release =
            {
                Cue(emberBreath, SkillFxPlace.Caster, 1, 0f, 0f, 0.9f), Cue(emberSpray, SkillFxPlace.Caster, 1, 0f, 0f, 1f),
                Cue(emberFlames, SkillFxPlace.AcrossArc, 4, 0.12f, 0.03f, 1.6f),
            },
            struck = { Cue(emberHit, SkillFxPlace.Struck, 1, 0.05f, 0f, 1f) },
            light = new Color(1f, 0.55f, 0.2f, 1f), lightIntensity = 7f, lightRange = 9f, lightSeconds = 0.4f, shake = 0.2f,
        };
        log.AppendLine(Dress("AshladsEmber", ember, burning, null, arc: -1f));

        // ----- Trollsbane Wrath: the hammer comes down and the ground answers. -----
        GameObject wrathCircle = Make("Wrath Cast Circle", new Design("Skill Ring").T("Texture", "Rune Circle").V("Color", new Vector3(3f, 2.2f, 1f))
            .F("Size", 3.4f).F("Lifetime", 0.8f));
        GameObject wrathWave = Make("Wrath Shockwave", new Design("Skill Ring").T("Texture", "Ring").V("Color", new Vector3(1.6f, 1.1f, 0.45f)).F("Size", 8f)
            .F("Lifetime", 0.45f));
        GameObject wrathDust = Make("Wrath Dust", new Design("Skill Mist").F("Rate", 70f).F("Radius", 0.8f).V("Color", new Vector3(0.55f, 0.5f, 0.42f))
            .F("Alpha", 0.45f).F("Lifetime Min", 0.8f).F("Lifetime Max", 1.4f).V("Velocity Min", new Vector3(-0.8f, 0.3f, -0.8f))
            .V("Velocity Max", new Vector3(0.8f, 1.2f, 0.8f)));
        GameObject wrathSparks = Make("Wrath Sparks", new Design("Skill Burst").V("Color", new Vector3(4f, 2.8f, 1.1f)).F("Count", 50f)
            .V("Velocity Min", new Vector3(-6f, 2f, -6f)).V("Velocity Max", new Vector3(6f, 7f, 6f)));
        var wrath = new SkillLook
        {
            cast = { Cue(wrathCircle, SkillFxPlace.CasterGround, 1, 0f, 0f, 1f) },
            release =
            {
                Cue(wrathWave, SkillFxPlace.CasterGround, 1, 0f, 0f, 0.6f), Cue(wrathDust, SkillFxPlace.Ring, 8, 0.03f, 0f, 0.7f),
                Cue(wrathSparks, SkillFxPlace.CasterGround, 1, 0f, 0f, 1.2f),
            },
            light = new Color(1f, 0.8f, 0.45f, 1f), lightIntensity = 9f, lightRange = 10f, lightSeconds = 0.4f, shake = 0.6f,
        };
        log.AppendLine(Dress("TrollsbaneWrath", wrath, null, null, arc: -1f));

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static SkillFxCue Cue(GameObject prefab, SkillFxPlace place, int count, float delay, float step, float seconds) =>
        new SkillFxCue { prefab = prefab, place = place, count = count, delay = delay, stepDelay = step, seconds = seconds };

    // A prefab of one VisualEffect on its graph, with its look in VfxValues; made again from its design each run (its GUID kept).
    private static GameObject Make(string name, Design design)
    {
        string path = $"{Prefabs}/{name}.prefab";
        var go = new GameObject(name);
        try
        {
            var effect = go.AddComponent<VisualEffect>();
            effect.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>($"{Graphs}/{design.Graph}.vfx");
            effect.playRate = 1f;
            var values = go.AddComponent<VfxValues>();
            values.Floats.AddRange(design.Floats);
            values.Vectors.AddRange(design.Vectors);
            values.Objects.AddRange(design.Objects);
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static StatusEffectDefinition Status(string name, string description, Color colour, float seconds, float damagePerSecond, DamageType type,
        float moveSpeed, bool refreshes, float restSeconds)
    {
        string path = $"Assets/_Project/Data/Combat/Status/{name}.asset";
        var status = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path);
        if (status == null)
        {
            status = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            AssetDatabase.CreateAsset(status, path);
        }

        var so = new SerializedObject(status);
        so.FindProperty("id").stringValue = name.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("color").colorValue = colour;
        so.FindProperty("seconds").floatValue = seconds;
        so.FindProperty("damagePerSecond").floatValue = damagePerSecond;
        so.FindProperty("damageType").enumValueIndex = (int)type;
        so.FindProperty("moveSpeed").floatValue = moveSpeed;
        so.FindProperty("refreshes").boolValue = refreshes;
        so.FindProperty("restSeconds").floatValue = restSeconds;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(status);
        return status;
    }

    // The skill's look, and on its attack the status it leaves and the one it adds on a creature already under it; a lance's arc.
    private static string Dress(string skillName, SkillLook look, StatusEffectDefinition status, StatusEffectDefinition onRepeat, float arc)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{skillName}.asset");
        if (skill == null)
        {
            return "no skill " + skillName;
        }

        typeof(SkillDefinition).GetField("look", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(skill, look);
        EditorUtility.SetDirty(skill);
        if (skill.Attack != null && (status != null || arc > 0f))
        {
            var attack = new SerializedObject(skill.Attack);
            if (status != null)
            {
                attack.FindProperty("appliesStatus").objectReferenceValue = status;
                attack.FindProperty("statusChance").floatValue = 1f;
            }

            attack.FindProperty("onRepeatStatus").objectReferenceValue = onRepeat;
            if (arc > 0f)
            {
                attack.FindProperty("arc").floatValue = arc;
            }

            attack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill.Attack);
        }

        return $"{skillName}: {look.cast.Count} cast, {look.release.Count} release, {look.struck.Count} struck cues";
    }
}
