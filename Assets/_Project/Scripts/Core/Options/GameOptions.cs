using System.Collections.Generic;

/// <summary>
/// Every setting in the game, by tab. Defaults are the game as designed: full graphics quality, so nothing here lowers it
/// until the player chooses to. To add a setting: a GameOption here, in its tab's list; read it where it matters, and if it
/// must take effect at once, handle it in OptionsSystem. The Options screen draws it by itself. Audio has its own volumes
/// in AudioManager, the camera its own in CameraPreferences and the controls in InputRebinds.
/// </summary>
public static class GameOptions
{
    // ----- Gameplay -----

    public static readonly GameOption DamageNumbers = GameOption.Toggle("DamageNumbers", "Damage Numbers", "Numbers over what you hit.", true);
    public static readonly GameOption AutoGather = GameOption.Toggle("AutoGather", "Auto-Gather",
        "Tools swing by themselves instead of the timing bar. No critical swings, but no glancing blows.", false);
    public static readonly GameOption ToggleSneak = GameOption.Toggle("ToggleSneak", "Toggle Sneak", "Press once to sneak and again to stop, instead of holding the key.", false);
    public static readonly GameOption FocusTarget = GameOption.Toggle("FocusTarget", "Focus on Enemy",
        "In a melee fight you face the enemy you are fighting and your swings follow it. Off, you always face the cursor.", true);
    public static readonly GameOption PoiseBars = GameOption.Toggle("PoiseBars", "Poise Bars", "A thin bar under an enemy's health: when it runs out, the enemy staggers.", true);
    public static readonly GameOption Blood = GameOption.Toggle("Blood", "Blood", "Blood from blows, and the stains it leaves.", true);
    public static readonly GameOption MinimapDistance = GameOption.Choice("MinimapDistance", "Minimap Distance",
        "How far around you the minimap reaches. The mouse wheel over the minimap changes it too.", 2, "Hidden", "30 m", "50 m", "75 m", "110 m");

    public static readonly IReadOnlyList<GameOption> Gameplay = new[] { DamageNumbers, AutoGather, ToggleSneak, FocusTarget, PoiseBars, Blood, MinimapDistance };

    /// <summary>The minimap's reach in metres for each Minimap Distance (0 for Hidden).</summary>
    public static readonly float[] MinimapReaches = { 0f, 30f, 50f, 75f, 110f };

    // ----- Graphics: picture -----

    public const int PresetLow = 0, PresetMedium = 1, PresetHigh = 2, PresetUltra = 3, PresetCustom = 4;

    public static readonly GameOption Preset = GameOption.Choice("Preset", "Quality Preset",
        "Sets everything below at once. Ultra is the game as it was made.", PresetUltra, "Low", "Medium", "High", "Ultra", "Custom");
    public static readonly GameOption RenderScale = GameOption.Slider("RenderScale", "Render Scale",
        "How many pixels are drawn, as a share of the screen. Lowering it is the biggest gain on a slow graphics card.", 1f, 0.5f, 1f, 0.05f);
    // DLSS and FSR 2 (2026-11-12, the user asked for NVIDIA's and AMD's): URP's own, with the NVIDIA and AMD modules and the
    // ENABLE_UPSCALER_FRAMEWORK define. New choices go last, so a saved index keeps its meaning.
    public static readonly GameOption Upscaling = GameOption.Choice("Upscaling", "Upscaling",
        "How the picture is brought up to the screen. Bilinear, FSR and STP work below 100% Render Scale. DLSS (NVIDIA RTX cards) " +
        "and FSR 2 (AMD, most cards, DirectX 12 or Vulkan only) rebuild a sharp picture from several frames, often a big gain in " +
        "frame rate; with them Render Scale picks the quality: 100% DLAA (DLSS) or Quality, 75% Quality, 60% Balanced, 50% " +
        "Performance. Where they can't run, STP is used.", 1, "Bilinear", "FSR", "STP", "DLSS", "FSR 2");
    public static readonly GameOption AntiAliasing = GameOption.Choice("AntiAliasing", "Anti-Aliasing", "Smooths jagged edges. Higher MSAA costs more; TAA also smooths shimmering grass, leaves and fine detail, a little softer in motion.", 2, "Off", "2x MSAA", "4x MSAA", "8x MSAA", "TAA");
    // Ultra (2026-11-09): an 8K sun shadow map. At the usual top-down zoom a 4K map was too coarse for the ground's own
    // relief (stones, roots, clumps a few centimetres high): its shadows only showed when zoomed in.
    public static readonly GameOption Shadows = GameOption.Choice("Shadows", "Shadows", "Sharpness and reach of shadows. Ultra is the game as it was made: the ground's own small shadows show at every zoom. High uses less video memory.", 3, "Low", "Medium", "High", "Ultra");
    public static readonly GameOption AmbientOcclusion = GameOption.Toggle("AmbientOcclusion", "Ambient Occlusion", "Soft shade where things meet the ground.", true);
    public static readonly GameOption ContactShadows = GameOption.Toggle("ContactShadows", "Contact Shadows",
        "The small shadows the sun's shadows miss from up here: under pebbles, grass clumps, feet and logs.", true);
    public static readonly GameOption VolumetricFog = GameOption.Choice("VolumetricFog", "Volumetric Fog", "Detail of the fog and light shafts. Costly in the raid forests.", 2, "Low", "Medium", "High");
    public static readonly GameOption TerrainDetail = GameOption.Choice("TerrainDetail", "Terrain Detail", "How much the ground's surface is shaped by its stones and roots.", 3, "Off", "Low", "Balanced", "Ultra");
    public static readonly GameOption GroundImpacts = GameOption.Choice("GroundImpacts", "Ground Impacts", "Heavy blows blow the grass flat, throw up dust and leave marks in the ground for a while. Low leaves out the flying stones, leaves and most marks.", 2, "Off", "Low", "High");
    public static readonly GameOption GrassDensity = GameOption.Slider("GrassDensity", "Grass Density", "How much grass and small plants are drawn.", 1f, 0.25f, 1f, 0.05f);
    public static readonly GameOption ViewDistance = GameOption.Choice("ViewDistance", "View Distance", "How far objects keep their full detail.", 2, "Low", "Medium", "High");
    public static readonly GameOption TextureQuality = GameOption.Choice("TextureQuality", "Texture Quality", "Sharpness of textures. Lower uses less video memory.", 2, "Low", "Medium", "High");

    /// <summary>What a preset sets. Render Scale and Grass are shares, the rest indexes of their choices.</summary>
    public static readonly IReadOnlyList<GameOption> PresetMembers = new[]
        { RenderScale, AntiAliasing, Shadows, AmbientOcclusion, VolumetricFog, TerrainDetail, GrassDensity, ViewDistance, TextureQuality };

    private static readonly float[][] PresetValues =
    {
        new[] { 0.75f, 0f, 0f, 0f, 0f, 0f, 0.4f, 0f, 1f },
        new[] { 0.85f, 1f, 1f, 1f, 1f, 1f, 0.7f, 1f, 2f },
        // High: high shadows (the user, 2026-11-16: it had Medium's, and only Ultra was higher).
        new[] { 1f, 2f, 2f, 1f, 1f, 2f, 0.85f, 2f, 2f },
        new[] { 1f, 2f, 3f, 1f, 2f, 3f, 1f, 2f, 2f },
    };

    /// <summary>Sets every member to what the preset says.</summary>
    public static void ApplyPreset(int preset)
    {
        if (preset < 0 || preset >= PresetValues.Length)
        {
            return;
        }

        for (int i = 0; i < PresetMembers.Count; i++)
        {
            PresetMembers[i].Value = PresetValues[preset][i];
        }
    }

    /// <summary>The preset the members add up to, or Custom.</summary>
    public static int MatchingPreset()
    {
        for (int preset = 0; preset < PresetValues.Length; preset++)
        {
            bool same = true;
            for (int i = 0; i < PresetMembers.Count && same; i++)
            {
                same = UnityEngine.Mathf.Approximately(PresetMembers[i].Value, PresetValues[preset][i]);
            }

            if (same)
            {
                return preset;
            }
        }

        return PresetCustom;
    }

    public static readonly IReadOnlyList<GameOption> Graphics = new[]
        { Preset, RenderScale, Upscaling, AntiAliasing, Shadows, AmbientOcclusion, ContactShadows, VolumetricFog, TerrainDetail, GroundImpacts, GrassDensity, ViewDistance, TextureQuality };

    // ----- Graphics: look -----

    public static readonly GameOption Brightness = GameOption.Slider("Brightness", "Brightness", "Lifts or lowers the dark. The raids are meant to be dark, but you should be able to see.", 0f, -0.5f, 0.5f, 0.05f, false);
    public static readonly GameOption Bloom = GameOption.Toggle("Bloom", "Bloom", "The soft glow around fire and light.", true);
    public static readonly GameOption MotionBlur = GameOption.Toggle("MotionBlur", "Motion Blur", "Blur on fast camera movement.", true);
    public static readonly GameOption FilmGrain = GameOption.Toggle("FilmGrain", "Film Grain", "A fine grain over the picture.", true);

    public static readonly IReadOnlyList<GameOption> Look = new[] { Brightness, Bloom, MotionBlur, FilmGrain };

    // ----- Display -----

    public static readonly GameOption VSync = GameOption.Toggle("VSync", "VSync", "Keeps the picture from tearing. Adds a little input delay.", false);
    public static readonly GameOption FrameLimit = GameOption.Choice("FrameLimit", "Frame Rate Limit", "The most frames per second. Lower runs cooler and quieter. With VSync on, it is kept by showing every second, third or fourth refresh of the screen.", 2,
        "Unlimited", "30", "60", "90", "120", "144", "165", "240");

    /// <summary>Frames per second for each FrameLimit choice; -1 is no limit.</summary>
    public static int FrameRate => new[] { -1, 30, 60, 90, 120, 144, 165, 240 }[FrameLimit.Index];

    // ----- Controls -----

    public static readonly GameOption LookSensitivity = GameOption.Slider("LookSensitivity", "Camera Sensitivity", "How fast the camera turns with the mouse or the right stick.", 1f, 0.2f, 3f, 0.05f);
    public static readonly GameOption InvertY = GameOption.Toggle("InvertY", "Invert Camera Y", "Mouse or right stick up looks down, in the action camera.", false);

    /// <summary>0 follows the controller in use; otherwise the PadFamily + 1 whose buttons are drawn.</summary>
    public static readonly GameOption ControllerButtons = GameOption.Choice("ControllerButtons", "Controller Buttons",
        "Which buttons the prompts show while you play with a controller. Automatic matches the controller in your hands; pick one if it shows the wrong kind (Steam Input can make every controller look like an Xbox pad).",
        0, "Automatic", "Xbox", "PlayStation", "Switch");

    public static readonly GameOption Vibration = GameOption.Slider("Vibration", "Controller Vibration",
        "How strongly the controller rumbles when you hit, are hit, parry, or a fish takes the bait. Off at 0%.", 1f, 0f, 1f, 0.05f);

    public static readonly IReadOnlyList<GameOption> Controls = new[] { LookSensitivity, InvertY, ControllerButtons, Vibration };

    // ----- Accessibility -----

    public static readonly GameOption UiScale = GameOption.Slider("UiScale", "UI and Text Size", "The size of all menus, text and the HUD.", 1f, 0.75f, 1.5f, 0.05f);
    /// <summary>The skill bar's size on the HUD (keys 1-9). 100% was its size until 2026-11-15, when the user asked for 15% more.</summary>
    public static readonly GameOption SkillBarSize = GameOption.Slider("SkillBarSize", "Skill Bar Size", "The size of the skill bar (keys 1-9) at the bottom of the screen.", 1.15f, 0.75f, 1.6f, 0.05f);
    public static readonly GameOption ScreenShake = GameOption.Slider("ScreenShake", "Screen Shake", "How much hits and heavy footsteps shake the camera.", 1f, 0f, 1f, 0.05f);
    public static readonly GameOption HitStop = GameOption.Toggle("HitStop", "Hit Stop", "The game freezes for a moment on strong hits.", true);
    public static readonly GameOption ScreenFlash = GameOption.Slider("ScreenFlash", "Screen Flashes", "How strongly the screen edges flash when you are hurt or low on health.", 1f, 0f, 1f, 0.05f);
    public static readonly GameOption TextSpeed = GameOption.Choice("TextSpeed", "Dialogue Speed", "How fast lines appear, and how long subtitles in a fight stay up.", 1, "Slow", "Normal", "Fast", "Instant");
    public static readonly GameOption LargeTimer = GameOption.Toggle("LargeTimer", "Large Raid Timer", "A bigger clock during raids.", false);
    public static readonly GameOption AlwaysShowVitals = GameOption.Toggle("AlwaysShowVitals", "Always Show Vitals",
        "Keeps health, stamina, hunger and thirst fully lit. Off, they dim while everything is full.", false);
    public static readonly GameOption VitalsOverPlayer = GameOption.Toggle("VitalsOverPlayer", "Vitals Over Player",
        "Shows small health and stamina bars above your character, and health above your companions, while you fight.", false);

    public static readonly GameOption SoundCaptions = GameOption.Toggle("SoundCaptions", "Sound Captions",
        "Writes important sounds on the screen, and the side they came from: a creature that has seen you, a horn, a tree coming down.", false);
    public static readonly GameOption RarityLetters = GameOption.Toggle("RarityLetters", "Rarity Letters",
        "Adds a letter to item slots for how rare the item is, so it doesn't rest on the colour of the frame.", false);
    public static readonly GameOption DamageTaken = GameOption.Choice("DamageTaken", "Damage Taken",
        "How much of a blow reaches you. Raids still end, and what you lose in them is still lost.", 0, "Normal", "75%", "50%");
    public static readonly GameOption TimingHelp = GameOption.Choice("TimingHelp", "Parry and Dodge Timing",
        "How long the moment to parry lasts, and how long a dodge carries you through a blow.", 0, "Normal", "Longer", "Much Longer");

    // Docs/LookAndFeelResearch_2026-11-18.md U5: warnings that read on any ground, and fog thinned for the eye only.
    public static readonly GameOption HighContrastWarnings = GameOption.Toggle("HighContrastWarnings", "High-Contrast Warnings",
        "Attack warnings and hazards on the ground get wider, brighter rims with a dark line inside, so they stand out on any ground.", false);
    public static readonly GameOption FogAmount = GameOption.Slider("FogAmount", "Fog Amount",
        "How thick the fog and mist look. Only what you see changes: creatures see through the fog as they always do.", 1f, 0.4f, 1f, 0.05f);
    public static readonly IReadOnlyList<GameOption> Accessibility = new[] { UiScale, SkillBarSize, ScreenShake, HitStop, ScreenFlash, TextSpeed, LargeTimer, AlwaysShowVitals, VitalsOverPlayer, SoundCaptions, RarityLetters, DamageTaken, TimingHelp, HighContrastWarnings, FogAmount };

    /// <summary>The share of the fog and mist that is drawn (Fog Amount): 0.4 to 1.</summary>
    public static float FogShown => UnityEngine.Mathf.Clamp(FogAmount.Value, 0.4f, 1f);

    /// <summary>The share of a blow that reaches the player.</summary>
    public static float DamageTakenShare => DamageTaken.Index == 2 ? 0.5f : DamageTaken.Index == 1 ? 0.75f : 1f;

    /// <summary>How many times longer the parry window and a dodge's protection last.</summary>
    public static float TimingShare => TimingHelp.Index == 2 ? 2f : TimingHelp.Index == 1 ? 1.5f : 1f;

    /// <summary>The assists in use, in words for the playtest log. Empty with none.</summary>
    public static string AssistsInUse => DamageTaken.Index == 0 && TimingHelp.Index == 0 ? "" : $"damage taken {DamageTakenShare:0.##}, timing {TimingShare:0.##}";

    /// <summary>Letters per second for dialogue, from a base speed. Infinity shows lines at once.</summary>
    public static float DialogueLettersPerSecond(float normal)
    {
        switch (TextSpeed.Index)
        {
            case 0:
                return normal * 0.5f;
            case 2:
                return normal * 2f;
            case 3:
                return float.PositiveInfinity;
            default:
                return normal;
        }
    }

    /// <summary>How much longer than normal a subtitle stays up.</summary>
    public static float SubtitleHold => TextSpeed.Index == 0 ? 1.5f : 1f;

    // ----- Audio -----

    public static readonly GameOption MuteUnfocused = GameOption.Toggle("MuteUnfocused", "Mute in Background", "Silence the game while its window isn't active.", false);
}
