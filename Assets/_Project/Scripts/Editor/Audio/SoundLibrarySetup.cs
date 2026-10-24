using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the free sound library in Audio/SFX (CC0; see Audio/SFX/CREDITS.md) into the game's sounds (Safe Haven › Audio ›
/// Set Up Free Sound Library): one SoundDefinition per game moment in Audio/Sounds/Library, assigned to the game-wide
/// slots in Audio Settings (combat, footsteps, world, interface, weather and water) and to the gathering feedback
/// profiles. Slots that already hold a sound are left alone, so designers' choices survive running it again; the sound
/// assets it made are updated in place.
/// </summary>
public static class SoundLibrarySetup
{
    private const string Sfx = "Assets/_Project/Audio/SFX/";
    private const string SoundFolder = "Assets/_Project/Audio/Sounds/Library";
    private const string SettingsPath = "Assets/_Project/Resources/AudioSettings.asset";
    private const string FeedbackFolder = "Assets/_Project/Data/Gathering/Feedback/";

    private sealed class Spec
    {
        public string Name;
        public string[] Clips;
        public float Volume = 0.8f;
        public Vector2 Pitch = new Vector2(0.94f, 1.06f);
        public bool Positional = true;
        public float Audible = 35f;
        public AudioChannel Channel = AudioChannel.Effects;
        public int MaxAtOnce = 4;
    }

    private static string[] Numbered(string folder, string prefix, int count, string format = "000")
    {
        return Enumerable.Range(0, count).Select(i => $"{Sfx}{folder}/{prefix}{i.ToString(format)}.ogg").ToArray();
    }

    private static string[] Files(string folder, params string[] names) => names.Select(n => $"{Sfx}{folder}/{n}.ogg").ToArray();

    [MenuItem("Safe Haven/Audio/Set Up Free Sound Library")]
    public static void SetUp()
    {
        if (!AssetDatabase.IsValidFolder(SoundFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Audio/Sounds", "Library");
        }

        const string impact = "Kenney Impact", rpg = "Kenney RPG", ui = "Kenney Interface", clicks = "Kenney UI", oga = "OGA 100 CC0 SFX 2";
        var sounds = new Dictionary<string, SoundDefinition>();
        void Make(Spec spec) => sounds[spec.Name] = Sound(spec);

        // Combat.
        Make(new Spec { Name = "Swing", Clips = Files(oga, "sfx100v2_air_03").Concat(Files(rpg, "knifeSlice", "knifeSlice2")).ToArray(), Volume = 0.55f, Pitch = new Vector2(0.9f, 1.15f), Audible = 20f });
        Make(new Spec { Name = "Heavy Swing", Clips = Files(oga, "sfx100v2_air_02"), Volume = 0.7f, Pitch = new Vector2(0.72f, 0.85f), Audible = 25f });
        Make(new Spec { Name = "Hit", Clips = Numbered(impact, "impactPunch_medium_", 5).Concat(Files(oga, "sfx100v2_hit_01", "sfx100v2_hit_02", "sfx100v2_hit_03")).ToArray(), Volume = 0.8f, MaxAtOnce = 6 });
        Make(new Spec { Name = "Critical Hit", Clips = Numbered(impact, "impactPunch_heavy_", 5), Volume = 0.85f, Pitch = new Vector2(0.85f, 0.95f) });
        Make(new Spec { Name = "Structure Hit", Clips = Numbered(impact, "impactWood_medium_", 5), Volume = 0.8f });
        Make(new Spec { Name = "Block", Clips = Numbered(impact, "impactMetal_medium_", 5), Volume = 0.75f, Pitch = new Vector2(0.95f, 1.1f) });
        Make(new Spec { Name = "Parry", Clips = Numbered(impact, "impactBell_heavy_", 5), Volume = 0.85f, Pitch = new Vector2(1.1f, 1.25f), MaxAtOnce = 2 });
        Make(new Spec { Name = "Guard Break", Clips = Numbered(impact, "impactPlate_heavy_", 5), Volume = 0.9f, Pitch = new Vector2(0.75f, 0.85f), MaxAtOnce = 2 });
        Make(new Spec { Name = "Stagger", Clips = Numbered(impact, "impactSoft_heavy_", 5), Volume = 0.85f, Pitch = new Vector2(0.75f, 0.9f) });
        Make(new Spec { Name = "Perfect Dodge", Clips = Files(oga, "sfx100v2_air_02"), Volume = 0.7f, Pitch = new Vector2(1.25f, 1.4f), Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Counter Ready", Clips = Files(ui, "confirmation_001"), Volume = 0.45f, Pitch = new Vector2(1f, 1f), Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Dodge", Clips = Files(rpg, "cloth1", "cloth2", "cloth3", "cloth4"), Volume = 0.6f, Audible = 15f });
        Make(new Spec { Name = "Player Hurt", Clips = Numbered(impact, "impactSoft_medium_", 5), Volume = 0.8f, Pitch = new Vector2(0.85f, 1f), Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Out of Stamina", Clips = Files(ui, "error_004"), Volume = 0.35f, Positional = false, MaxAtOnce = 1 });
        // Footsteps.
        Make(new Spec { Name = "Footstep Grass", Clips = Numbered(impact, "footstep_grass_", 5), Volume = 0.35f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Dirt", Clips = Numbered(rpg, "footstep0", 10, "0"), Volume = 0.35f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Stone", Clips = Numbered(impact, "footstep_concrete_", 5), Volume = 0.35f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Snow", Clips = Numbered(impact, "footstep_snow_", 5), Volume = 0.35f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Wood", Clips = Numbered(impact, "footstep_wood_", 5), Volume = 0.4f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Water", Clips = Files(oga, "sfx100v2_footstep_wet_01", "sfx100v2_footstep_wet_02", "sfx100v2_footstep_wet_03"), Volume = 0.45f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Land", Clips = Numbered(impact, "impactGeneric_light_", 5), Volume = 0.5f, Pitch = new Vector2(0.7f, 0.85f), Audible = 12f, MaxAtOnce = 1 });
        // World and interface.
        Make(new Spec { Name = "Quest Started", Clips = Files(ui, "maximize_008"), Volume = 0.5f, Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Quest Completed", Clips = Files(ui, "confirmation_004"), Volume = 0.6f, Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Crafted", Clips = Files(rpg, "metalPot1", "metalPot2", "metalPot3"), Volume = 0.55f, Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Built", Clips = Numbered(impact, "impactPlank_medium_", 5), Volume = 0.8f, Pitch = new Vector2(0.85f, 1f) });
        Make(new Spec { Name = "Click", Clips = Files(clicks, "click3", "click4"), Volume = 0.35f, Pitch = new Vector2(0.97f, 1.03f), Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Window Open", Clips = Files(ui, "open_001", "open_002"), Volume = 0.35f, Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Window Close", Clips = Files(ui, "close_001", "close_002"), Volume = 0.35f, Positional = false, MaxAtOnce = 1 });
        // Weather and water (ambience loops).
        Make(new Spec { Name = "Rain Loop", Clips = new[] { Sfx + "OGA Rain Loops/Rain Loop 3.ogg" }, Volume = 0.6f, Pitch = Vector2.one, Positional = false, Channel = AudioChannel.Ambience, MaxAtOnce = 1 });
        Make(new Spec { Name = "Wind Loop", Clips = new[] { "Assets/_Project/Audio/Placeholder/Ambience_WildsWind.wav" }, Volume = 0.5f, Pitch = Vector2.one, Positional = false, Channel = AudioChannel.Ambience, MaxAtOnce = 1 });
        Make(new Spec { Name = "Thunder", Clips = Files(oga, "sfx100v2_thunder_01"), Volume = 0.9f, Pitch = new Vector2(0.8f, 1.05f), Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Stream Loop", Clips = Files(oga, "sfx100v2_loop_water_02"), Volume = 0.55f, Pitch = Vector2.one, Audible = 18f, Channel = AudioChannel.Ambience, MaxAtOnce = 8 });
        // Gathering.
        Make(new Spec { Name = "Chop", Clips = Numbered(impact, "impactWood_heavy_", 5).Concat(Files(rpg, "chop")).ToArray(), Volume = 0.8f });
        Make(new Spec { Name = "Mine", Clips = Numbered(impact, "impactMining_", 5), Volume = 0.8f });
        Make(new Spec { Name = "Cut Fibre", Clips = Files(rpg, "handleSmallLeather", "handleSmallLeather2", "cloth3"), Volume = 0.6f });
        Make(new Spec { Name = "Glancing Blow", Clips = Numbered(impact, "impactGeneric_light_", 5), Volume = 0.6f, Pitch = new Vector2(1.05f, 1.2f) });
        Make(new Spec { Name = "Wood Breaks", Clips = Files(oga, "sfx100v2_wood_hit_01", "sfx100v2_wood_hit_02", "sfx100v2_wood_hit_03"), Volume = 0.9f, Pitch = new Vector2(0.8f, 0.95f) });
        Make(new Spec { Name = "Stone Breaks", Clips = Files(oga, "sfx100v2_stones_01", "sfx100v2_stones_02", "sfx100v2_stones_03"), Volume = 0.9f });

        AssignSettings(sounds);
        AssignHarvest("WoodFeedback", sounds["Chop"], sounds["Wood Breaks"], sounds);
        AssignHarvest("StoneFeedback", sounds["Mine"], sounds["Stone Breaks"], sounds);
        AssignHarvest("OreFeedback", sounds["Mine"], sounds["Stone Breaks"], sounds);
        AssignHarvest("FibreFeedback", sounds["Cut Fibre"], sounds["Cut Fibre"], sounds);
        AssignHarvest("DefaultFeedback", sounds["Hit"], sounds["Structure Hit"], sounds);
        AssetDatabase.SaveAssets();
        Debug.Log($"Sound library: {sounds.Count} sounds in {SoundFolder}, set in Audio Settings and the gathering feedback.");
    }

    private static SoundDefinition Sound(Spec spec)
    {
        string path = $"{SoundFolder}/{spec.Name}.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var data = new SerializedObject(sound);
        data.FindProperty("displayName").stringValue = spec.Name;
        SerializedProperty clips = data.FindProperty("clips");
        List<AudioClip> loaded = spec.Clips.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToList();
        if (loaded.Count < spec.Clips.Length)
        {
            Debug.LogWarning($"Sound library: {spec.Name} found {loaded.Count} of {spec.Clips.Length} clips.");
        }

        clips.arraySize = loaded.Count;
        for (int i = 0; i < loaded.Count; i++)
        {
            clips.GetArrayElementAtIndex(i).objectReferenceValue = loaded[i];
        }

        data.FindProperty("channel").enumValueIndex = (int)spec.Channel;
        data.FindProperty("volume").floatValue = spec.Volume;
        data.FindProperty("pitchRange").vector2Value = spec.Pitch;
        data.FindProperty("positional").boolValue = spec.Positional;
        data.FindProperty("audibleDistance").floatValue = spec.Audible;
        data.FindProperty("maxAtOnce").intValue = spec.MaxAtOnce;
        data.ApplyModifiedPropertiesWithoutUndo();
        return sound;
    }

    private static void AssignSettings(Dictionary<string, SoundDefinition> sounds)
    {
        var settings = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(SettingsPath);
        var data = new SerializedObject(settings);
        void Slot(string field, string sound)
        {
            SerializedProperty property = data.FindProperty(field);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = sounds[sound];
            }
        }

        Slot("swing", "Swing");
        Slot("heavySwing", "Heavy Swing");
        Slot("hit", "Hit");
        Slot("criticalHit", "Critical Hit");
        Slot("structureHit", "Structure Hit");
        Slot("block", "Block");
        Slot("parry", "Parry");
        Slot("guardBreak", "Guard Break");
        Slot("stagger", "Stagger");
        Slot("perfectDodge", "Perfect Dodge");
        Slot("counterReady", "Counter Ready");
        Slot("dodge", "Dodge");
        Slot("playerHurt", "Player Hurt");
        Slot("outOfStamina", "Out of Stamina");
        Slot("footstep", "Footstep Dirt");
        Slot("footstepWood", "Footstep Wood");
        Slot("footstepWater", "Footstep Water");
        Slot("land", "Land");
        Slot("questStarted", "Quest Started");
        Slot("questCompleted", "Quest Completed");
        Slot("crafted", "Crafted");
        Slot("built", "Built");
        Slot("click", "Click");
        Slot("windowOpen", "Window Open");
        Slot("windowClose", "Window Close");
        Slot("rainLoop", "Rain Loop");
        Slot("windLoop", "Wind Loop");
        Slot("thunder", "Thunder");
        Slot("waterLoop", "Stream Loop");

        SerializedProperty surfaces = data.FindProperty("footstepSurfaces");
        if (surfaces.arraySize == 0)
        {
            (string words, string sound)[] defaults =
            {
                ("grass, moss, meadow, forest floor, leaves", "Footstep Grass"),
                ("snow, frost", "Footstep Snow"),
                ("stone, rock, cliff, gravel, cobble", "Footstep Stone"),
                ("dirt, path, soil, mud, sand, ground", "Footstep Dirt"),
            };
            surfaces.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                SerializedProperty entry = surfaces.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("layerWords").stringValue = defaults[i].words;
                entry.FindPropertyRelative("sound").objectReferenceValue = sounds[defaults[i].sound];
            }
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignHarvest(string profile, SoundDefinition hit, SoundDefinition harvested, Dictionary<string, SoundDefinition> sounds)
    {
        var asset = AssetDatabase.LoadAssetAtPath<HarvestFeedbackProfile>($"{FeedbackFolder}{profile}.asset");
        if (asset == null)
        {
            return;
        }

        var data = new SerializedObject(asset);
        void Moment(string field, SoundDefinition sound)
        {
            SerializedProperty property = data.FindProperty(field).FindPropertyRelative("sound");
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = sound;
            }
        }

        Moment("critical", hit);
        Moment("normal", hit);
        Moment("glancing", sounds["Glancing Blow"]);
        Moment("harvested", harvested);
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
