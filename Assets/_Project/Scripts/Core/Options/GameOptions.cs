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

    public static readonly IReadOnlyList<GameOption> Gameplay = new[] { DamageNumbers, AutoGather, ToggleSneak };

    // ----- Graphics: picture -----

    public const int PresetLow = 0, PresetMedium = 1, PresetHigh = 2, PresetUltra = 3, PresetCustom = 4;

    public static readonly GameOption Preset = GameOption.Choice("Preset", "Quality Preset",
        "Sets everything below at once. Ultra is the game as it was made.", PresetUltra, "Low", "Medium", "High", "Ultra", "Custom");
    public static readonly GameOption RenderScale = GameOption.Slider("RenderScale", "Render Scale",
        "How many pixels are drawn, as a share of the screen. Lowering it is the biggest gain on a slow graphics card.", 1f, 0.5f, 1f, 0.05f);
    public static readonly GameOption Upscaling = GameOption.Choice("Upscaling", "Upscaling", "How a Render Scale below 100% is brought up to the screen.", 1, "Bilinear", "FSR", "STP");
    public static readonly GameOption AntiAliasing = GameOption.Choice("AntiAliasing", "Anti-Aliasing", "Smooths jagged edges.", 2, "Off", "2x MSAA", "4x MSAA", "8x MSAA");
    public static readonly GameOption Shadows = GameOption.Choice("Shadows", "Shadows", "Sharpness and reach of shadows. High is the game as it was made.", 2, "Low", "Medium", "High");
    public static readonly GameOption AmbientOcclusion = GameOption.Toggle("AmbientOcclusion", "Ambient Occlusion", "Soft shade where things meet the ground.", true);
    public static readonly GameOption VolumetricFog = GameOption.Choice("VolumetricFog", "Volumetric Fog", "Detail of the fog and light shafts. Costly in the raid forests.", 2, "Low", "Medium", "High");
    public static readonly GameOption TerrainDetail = GameOption.Choice("TerrainDetail", "Terrain Detail", "How much the ground's surface is shaped by its stones and roots.", 3, "Off", "Low", "Balanced", "Ultra");
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
        new[] { 1f, 2f, 1f, 1f, 1f, 2f, 0.85f, 2f, 2f },
        new[] { 1f, 2f, 2f, 1f, 2f, 3f, 1f, 2f, 2f },
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
        { Preset, RenderScale, Upscaling, AntiAliasing, Shadows, AmbientOcclusion, VolumetricFog, TerrainDetail, GrassDensity, ViewDistance, TextureQuality };

    // ----- Graphics: look -----

    public static readonly GameOption Brightness = GameOption.Slider("Brightness", "Brightness", "Lifts or lowers the dark. The raids are meant to be dark, but you should be able to see.", 0f, -0.5f, 0.5f, 0.05f, false);
    public static readonly GameOption Bloom = GameOption.Toggle("Bloom", "Bloom", "The soft glow around fire and light.", true);
    public static readonly GameOption MotionBlur = GameOption.Toggle("MotionBlur", "Motion Blur", "Blur on fast camera movement.", true);
    public static readonly GameOption FilmGrain = GameOption.Toggle("FilmGrain", "Film Grain", "A fine grain over the picture.", true);

    public static readonly IReadOnlyList<GameOption> Look = new[] { Brightness, Bloom, MotionBlur, FilmGrain };

    // ----- Display -----

    public static readonly GameOption VSync = GameOption.Toggle("VSync", "VSync", "Keeps the picture from tearing. Adds a little input delay.", false);
    public static readonly GameOption FrameLimit = GameOption.Choice("FrameLimit", "Frame Rate Limit", "The most frames per second. Lower runs cooler and quieter.", 0,
        "Unlimited", "30", "60", "90", "120", "144", "165", "240");

    /// <summary>Frames per second for each FrameLimit choice; -1 is no limit.</summary>
    public static int FrameRate => new[] { -1, 30, 60, 90, 120, 144, 165, 240 }[FrameLimit.Index];

    // ----- Controls -----

    public static readonly GameOption LookSensitivity = GameOption.Slider("LookSensitivity", "Camera Sensitivity", "How fast the camera turns with the mouse or the right stick.", 1f, 0.2f, 3f, 0.05f);
    public static readonly GameOption InvertY = GameOption.Toggle("InvertY", "Invert Camera Y", "Mouse up looks down, in the action camera.", false);

    public static readonly IReadOnlyList<GameOption> Controls = new[] { LookSensitivity, InvertY };

    // ----- Accessibility -----

    public static readonly GameOption UiScale = GameOption.Slider("UiScale", "UI and Text Size", "The size of all menus, text and the HUD.", 1f, 0.75f, 1.5f, 0.05f);
    public static readonly GameOption ScreenShake = GameOption.Slider("ScreenShake", "Screen Shake", "How much hits and heavy footsteps shake the camera.", 1f, 0f, 1f, 0.05f);
    public static readonly GameOption HitStop = GameOption.Toggle("HitStop", "Hit Stop", "The game freezes for a moment on strong hits.", true);
    public static readonly GameOption ScreenFlash = GameOption.Slider("ScreenFlash", "Screen Flashes", "How strongly the screen edges flash when you are hurt or low on health.", 1f, 0f, 1f, 0.05f);
    public static readonly GameOption TextSpeed = GameOption.Choice("TextSpeed", "Dialogue Speed", "How fast lines appear, and how long subtitles in a fight stay up.", 1, "Slow", "Normal", "Fast", "Instant");
    public static readonly GameOption LargeTimer = GameOption.Toggle("LargeTimer", "Large Raid Timer", "A bigger clock during raids.", false);

    public static readonly IReadOnlyList<GameOption> Accessibility = new[] { UiScale, ScreenShake, HitStop, ScreenFlash, TextSpeed, LargeTimer };

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
