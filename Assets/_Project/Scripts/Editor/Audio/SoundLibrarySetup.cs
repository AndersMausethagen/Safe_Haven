using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the sound library in Audio/SFX (CC0; see Audio/SFX/CREDITS.md), free packs and the sounds made for the game by
/// Tools/Audio, into the game's sounds (Safe Haven › Audio › Set Up Free Sound Library): one SoundDefinition per game moment in Audio/Sounds/Library, assigned to the game-wide
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

    private static string[] Synth(string kind) => Enumerable.Range(1, 8).Select(i => $"{Sfx}Safe Haven Footsteps/Footstep {kind} {i}.wav").ToArray();

    /// <summary>Sounds made for the game by Tools/Audio. They are made to play near their own pitch, so their ranges stay small.</summary>
    private static string[] Made(string folder, string stem, int count) => count == 1
        ? new[] { $"{Sfx}Safe Haven {folder}/{stem}.wav" }
        : Enumerable.Range(1, count).Select(i => $"{Sfx}Safe Haven {folder}/{stem} {i}.wav").ToArray();

    /// <summary>Set to rewrite the footstep surface list with the defaults on the next run (it is kept otherwise).</summary>
    public static bool ResetFootstepSurfaces;

    [MenuItem("Safe Haven/Audio/Set Up Free Sound Library")]
    public static void SetUp()
    {
        if (!AssetDatabase.IsValidFolder(SoundFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Audio/Sounds", "Library");
        }

        const string impact = "Kenney Impact", rpg = "Kenney RPG", oga = "OGA 100 CC0 SFX 2", combat = "Combat", face = "Interface", world = "World";
        var sounds = new Dictionary<string, SoundDefinition>();
        void Make(Spec spec) => sounds[spec.Name] = Sound(spec);

        // Combat.
        Make(new Spec { Name = "Swing", Clips = Made(combat, "Swing", 4), Volume = 0.51f, Pitch = new Vector2(0.92f, 1.08f), Audible = 20f });
        Make(new Spec { Name = "Heavy Swing", Clips = Made(combat, "Heavy Swing", 3), Volume = 0.74f, Pitch = new Vector2(0.94f, 1.06f), Audible = 25f });
        Make(new Spec { Name = "Hit", Clips = Made(combat, "Hit", 8), Volume = 0.68f, MaxAtOnce = 6 });
        Make(new Spec { Name = "Critical Hit", Clips = Made(combat, "Critical Hit", 5), Volume = 0.82f, Pitch = new Vector2(0.95f, 1.05f) });
        Make(new Spec { Name = "Structure Hit", Clips = Numbered(impact, "impactWood_medium_", 5), Volume = 0.8f });
        Make(new Spec { Name = "Block", Clips = Made(combat, "Block", 5), Volume = 0.55f, Pitch = new Vector2(0.95f, 1.05f) });
        Make(new Spec { Name = "Parry", Clips = Made(combat, "Parry", 5), Volume = 0.65f, Pitch = new Vector2(0.95f, 1.05f), MaxAtOnce = 2 });
        Make(new Spec { Name = "Guard Break", Clips = Made(combat, "Guard Break", 4), Volume = 0.55f, Pitch = new Vector2(0.95f, 1.05f), MaxAtOnce = 2 });
        Make(new Spec { Name = "Stagger", Clips = Made(combat, "Stagger", 4), Volume = 0.69f, Pitch = new Vector2(0.95f, 1.05f) });
        Make(new Spec { Name = "Perfect Dodge", Clips = Made(combat, "Perfect Dodge", 2), Volume = 0.77f, Pitch = new Vector2(0.95f, 1.05f), Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Counter Ready", Clips = Made(face, "Counter Ready", 2), Volume = 0.41f, Pitch = new Vector2(0.98f, 1.02f), Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Dodge", Clips = Made(combat, "Dodge", 4), Volume = 0.39f, Audible = 15f });
        Make(new Spec { Name = "Player Hurt", Clips = Made(combat, "Player Hurt", 6), Volume = 0.74f, Pitch = new Vector2(0.95f, 1.05f), Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Out of Stamina", Clips = Made(face, "Out of Stamina", 2), Volume = 0.25f, Positional = false, MaxAtOnce = 1 });
        // Footsteps.
        // Soft, faint steps made for the game (Tools/Audio/footsteps.py): barely there, like walking carefully through a forest.
        Make(new Spec { Name = "Footstep Grass", Clips = Synth("Grass"), Volume = 0.3f, Pitch = new Vector2(0.92f, 1.08f), Audible = 10f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Forest", Clips = Synth("Forest"), Volume = 0.3f, Pitch = new Vector2(0.92f, 1.08f), Audible = 10f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Dirt", Clips = Synth("Dirt"), Volume = 0.3f, Pitch = new Vector2(0.92f, 1.08f), Audible = 10f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Stone", Clips = Numbered(impact, "footstep_concrete_", 5), Volume = 0.15f, Pitch = new Vector2(0.9f, 1.05f), Audible = 10f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Snow", Clips = Numbered(impact, "footstep_snow_", 5), Volume = 0.35f, Pitch = new Vector2(0.9f, 1.1f), Audible = 12f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Wood", Clips = Numbered(impact, "footstep_wood_", 5), Volume = 0.15f, Pitch = new Vector2(0.9f, 1.05f), Audible = 10f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Footstep Water", Clips = Files(oga, "sfx100v2_footstep_wet_01", "sfx100v2_footstep_wet_02", "sfx100v2_footstep_wet_03"), Volume = 0.25f, Pitch = new Vector2(0.9f, 1.1f), Audible = 10f, MaxAtOnce = 3 });
        Make(new Spec { Name = "Land", Clips = Made(combat, "Land", 4), Volume = 0.34f, Pitch = new Vector2(0.95f, 1.05f), Audible = 12f, MaxAtOnce = 1 });
        // World and interface.
        Make(new Spec { Name = "Quest Started", Clips = Made(face, "Quest Started", 1), Volume = 0.4f, Pitch = Vector2.one, Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Quest Completed", Clips = Made(face, "Quest Completed", 1), Volume = 0.62f, Pitch = Vector2.one, Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Crafted", Clips = Made(face, "Crafted", 3), Volume = 0.5f, Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Built", Clips = Numbered(impact, "impactPlank_medium_", 5), Volume = 0.8f, Pitch = new Vector2(0.85f, 1f) });
        Make(new Spec { Name = "Click", Clips = Made(face, "Click", 3), Volume = 0.17f, Pitch = new Vector2(0.97f, 1.03f), Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Window Open", Clips = Made(face, "Window Open", 2), Volume = 0.3f, Positional = false, MaxAtOnce = 1 });
        Make(new Spec { Name = "Window Close", Clips = Made(face, "Window Close", 2), Volume = 0.3f, Positional = false, MaxAtOnce = 1 });
        // Weather and water (ambience loops).
        Make(new Spec { Name = "Rain Loop", Clips = new[] { Sfx + "OGA Rain Loops/Rain Loop 3.ogg" }, Volume = 0.6f, Pitch = Vector2.one, Positional = false, Channel = AudioChannel.Ambience, MaxAtOnce = 1 });
        Make(new Spec { Name = "Wind Loop", Clips = new[] { "Assets/_Project/Audio/Placeholder/Ambience_WildsWind.wav" }, Volume = 0.5f, Pitch = Vector2.one, Positional = false, Channel = AudioChannel.Ambience, MaxAtOnce = 1 });
        Make(new Spec { Name = "Thunder", Clips = Files(oga, "sfx100v2_thunder_01"), Volume = 0.9f, Pitch = new Vector2(0.8f, 1.05f), Positional = false, MaxAtOnce = 2 });
        Make(new Spec { Name = "Stream Loop", Clips = Files(oga, "sfx100v2_loop_water_02"), Volume = 0.55f, Pitch = Vector2.one, Audible = 18f, Channel = AudioChannel.Ambience, MaxAtOnce = 8 });
        // Gathering.
        Make(new Spec { Name = "Chop", Clips = Numbered(impact, "impactWood_heavy_", 5).Concat(Files(rpg, "chop")).ToArray(), Volume = 0.8f });
        Make(new Spec { Name = "Mine", Clips = Numbered(impact, "impactMining_", 5), Volume = 0.8f });
        Make(new Spec { Name = "Cut Fibre", Clips = Files(rpg, "handleSmallLeather", "handleSmallLeather2", "cloth3"), Volume = 0.6f });
        Make(new Spec { Name = "Glancing Blow", Clips = Made(combat, "Glancing Blow", 4), Volume = 0.33f });
        Make(new Spec { Name = "Wood Breaks", Clips = Made(world, "Wood Breaks", 3), Volume = 0.58f });
        Make(new Spec { Name = "Stone Breaks", Clips = Made(world, "Stone Breaks", 3), Volume = 0.54f });

        AssignSettings(sounds);
        AssignHarvest("WoodFeedback", sounds["Chop"], sounds["Wood Breaks"], sounds);
        AssignHarvest("StoneFeedback", sounds["Mine"], sounds["Stone Breaks"], sounds);
        AssignHarvest("OreFeedback", sounds["Mine"], sounds["Stone Breaks"], sounds);
        AssignHarvest("FibreFeedback", sounds["Cut Fibre"], sounds["Cut Fibre"], sounds);
        AssignHarvest("DefaultFeedback", sounds["Hit"], sounds["Structure Hit"], sounds);
        SetUpWeaponSounds();
        AssetDatabase.SaveAssets();
        Debug.Log($"Sound library: {sounds.Count} sounds in {SoundFolder}, set in Audio Settings and the gathering feedback.");
    }

    /// <summary>Swing and hit sounds of their own for fists and axes (the game-wide swing has a blade's ring), set on
    /// those weapons where they have none. Set Up Free Sound Library runs it too.</summary>
    [MenuItem("Safe Haven/Audio/Set Up Weapon Sounds")]
    public static void SetUpWeaponSounds()
    {
        const string combat = "Combat";
        // Fists: a quick cloth swish and a dull punch; axes: a deep whoosh and a heavy, low thud.
        SoundDefinition punchSwing = Sound(new Spec { Name = "Punch Swing", Clips = Made(combat, "Punch Swing", 4), Volume = 0.36f, Audible = 15f });
        SoundDefinition punchHit = Sound(new Spec { Name = "Punch Hit", Clips = Made(combat, "Punch Hit", 5), Volume = 0.75f, Pitch = new Vector2(0.95f, 1.05f), MaxAtOnce = 6 });
        SoundDefinition axeSwing = Sound(new Spec { Name = "Axe Swing", Clips = Made(combat, "Axe Swing", 3), Volume = 0.74f, Audible = 22f });
        SoundDefinition axeHit = Sound(new Spec { Name = "Axe Hit", Clips = Made(combat, "Axe Hit", 5), Volume = 0.8f, Pitch = new Vector2(0.95f, 1.05f), MaxAtOnce = 6 });
        AssignWeapon("Fists", punchSwing, punchHit);
        AssignWeapon("StoneAxe", axeSwing, axeHit);
        AssignWeapon("WoodsmansAxe", axeSwing, axeHit);
        AssignWeapon("Greataxe", axeSwing, axeHit);
        AssetDatabase.SaveAssets();
    }

    private static void AssignWeapon(string weapon, SoundDefinition swing, SoundDefinition hit)
    {
        var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{weapon}.asset");
        if (asset == null)
        {
            return;
        }

        var data = new SerializedObject(asset);
        foreach ((string field, SoundDefinition sound) in new[] { ("swingSound", swing), ("hitSound", hit) })
        {
            SerializedProperty property = data.FindProperty(field);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = sound;
            }
        }

        data.ApplyModifiedPropertiesWithoutUndo();
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
        Slot("footstep", "Footstep Forest");
        Slot("footstepGrass", "Footstep Grass");
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
        if (surfaces.arraySize == 0 || ResetFootstepSurfaces)
        {
            (string words, string sound)[] defaults =
            {
                ("grass, meadow, lawn", "Footstep Grass"),
                ("forest, moss, leaves, needles, soil", "Footstep Forest"),
                ("snow, frost", "Footstep Snow"),
                ("stone, rock, cliff, gravel, cobble", "Footstep Stone"),
                ("dirt, path, mud, sand, ground", "Footstep Dirt"),
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
