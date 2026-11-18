using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The settings: their ranges, presets and defaults, and the controls list against the Input Actions asset.</summary>
public sealed class OptionsTests
{
    private readonly Dictionary<GameOption, float> before = new Dictionary<GameOption, float>();

    private static IEnumerable<GameOption> Everything()
    {
        return GameOptions.Gameplay.Concat(GameOptions.Graphics).Concat(GameOptions.Look).Concat(GameOptions.Controls).Concat(GameOptions.Accessibility)
            .Concat(new[] { GameOptions.Upscaling, GameOptions.VSync, GameOptions.FrameLimit, GameOptions.MuteUnfocused });
    }

    [SetUp]
    public void Remember()
    {
        foreach (GameOption option in Everything())
        {
            before[option] = option.Value;
        }
    }

    // The player's own preferences are in the same place, so they go back as they were.
    [TearDown]
    public void Restore()
    {
        foreach (KeyValuePair<GameOption, float> saved in before)
        {
            saved.Key.Value = saved.Value;
        }

        before.Clear();
        InputSystem.actions.RemoveAllBindingOverrides();
        InputRebinds.Load();
    }

    [Test]
    public void AValueStaysInItsRangeAndSnapsToItsStep()
    {
        GameOption option = GameOptions.RenderScale;
        option.Value = 5f;
        Assert.AreEqual(1f, option.Value, 0.001f);
        option.Value = 0f;
        Assert.AreEqual(0.5f, option.Value, 0.001f);
        option.Value = 0.77f;
        Assert.AreEqual(0.75f, option.Value, 0.001f);
    }

    [Test]
    public void AChangeIsReportedOnce()
    {
        GameOption option = GameOptions.DamageNumbers;
        int heard = 0;
        void Count(GameOption changed)
        {
            if (changed == option)
            {
                heard++;
            }
        }

        GameOption.AnyChanged += Count;
        option.Value = option.On ? 0f : 1f;
        float same = option.Value;
        option.Value = same;
        GameOption.AnyChanged -= Count;
        Assert.AreEqual(1, heard);
    }

    [Test]
    public void ValuesReadTheWayThePlayerExpects()
    {
        GameOptions.DamageNumbers.Value = 1f;
        Assert.AreEqual("On", GameOptions.DamageNumbers.ValueText);
        GameOptions.AntiAliasing.Value = 4f;
        Assert.AreEqual("TAA", GameOptions.AntiAliasing.ValueText, "temporal, as a choice");
        GameOptions.AntiAliasing.Value = 2f;
        Assert.AreEqual("4x MSAA", GameOptions.AntiAliasing.ValueText);
        GameOptions.ScreenShake.Value = 0.5f;
        Assert.AreEqual("50%", GameOptions.ScreenShake.ValueText);
    }

    [Test]
    public void TheDefaultsAreTheGameAsMade()
    {
        foreach (GameOption member in GameOptions.PresetMembers)
        {
            member.ResetToDefault();
        }

        Assert.AreEqual(GameOptions.PresetUltra, GameOptions.MatchingPreset(), "defaults must be the Ultra preset");
        Assert.AreEqual(1f, GameOptions.RenderScale.Value, 0.001f);
        Assert.AreEqual(2, GameOptions.AntiAliasing.Index, "4x MSAA, as in the render asset");
        Assert.IsTrue(GameOptions.AmbientOcclusion.On);
        Assert.AreEqual(TerrainSurface.DetailQuality.Ultra, (TerrainSurface.DetailQuality)GameOptions.TerrainDetail.Index);
    }

    [Test]
    public void EveryPresetIsRecognisedAndATweakIsCustom(
        [Values(GameOptions.PresetLow, GameOptions.PresetMedium, GameOptions.PresetHigh, GameOptions.PresetUltra)] int preset)
    {
        GameOptions.ApplyPreset(preset);
        Assert.AreEqual(preset, GameOptions.MatchingPreset());
        GameOptions.GrassDensity.Value = 0.55f;
        Assert.AreEqual(GameOptions.PresetCustom, GameOptions.MatchingPreset());
    }

    [Test]
    public void DialogueSpeedAndSubtitlesFollowTheSetting()
    {
        GameOptions.TextSpeed.Value = 3f;
        Assert.IsTrue(float.IsInfinity(GameOptions.DialogueLettersPerSecond(55f)));
        GameOptions.TextSpeed.Value = 1f;
        Assert.AreEqual(55f, GameOptions.DialogueLettersPerSecond(55f), 0.001f);
        Assert.AreEqual(1f, GameOptions.SubtitleHold, 0.001f);
        GameOptions.TextSpeed.Value = 0f;
        Assert.Greater(GameOptions.SubtitleHold, 1f);
    }

    [Test]
    public void TheFrameLimitListMatchesItsNames()
    {
        GameOptions.FrameLimit.Value = 0f;
        Assert.AreEqual(-1, GameOptions.FrameRate);
        for (int i = 1; i < GameOptions.FrameLimit.Choices.Length; i++)
        {
            GameOptions.FrameLimit.Value = i;
            Assert.AreEqual(GameOptions.FrameLimit.Choices[i], GameOptions.FrameRate.ToString());
        }
    }

    [Test]
    public void ScreenShakeOffAddsNone()
    {
        // With shake on, a blow is felt: without this half, a CameraShake that never shook would pass as well.
        GameOptions.ScreenShake.Value = 1f;
        CameraShake.Add(0.5f);
        Assert.GreaterOrEqual(CameraShake.Trauma, 0.45f, "shake on: a blow shakes the view");

        GameOptions.ScreenShake.Value = 0f;
        float start = CameraShake.Trauma;
        CameraShake.Add(0.5f);
        Assert.LessOrEqual(CameraShake.Trauma, start);
    }

    [Test]
    public void EveryRowHasAnActionAndAKeySomewhere()
    {
        foreach (RebindRow row in InputRebinds.Rows)
        {
            InputAction action = InputSystem.actions.FindAction(row.ActionPath);
            Assert.IsNotNull(action, row.ActionPath);
            Assert.IsTrue(InputRebinds.HasBinding(row, GameInput.KeyboardMouse) || InputRebinds.HasBinding(row, GameInput.Gamepad), row.Label + " has no key at all");
        }
    }

    [Test]
    public void TheControlsListCoversEveryPlayerAction()
    {
        var listed = new HashSet<string>(InputRebinds.Rows.Select(row => row.ActionPath));
        foreach (InputAction action in InputSystem.actions.FindActionMap("Player").actions)
        {
            Assert.IsTrue(listed.Contains("Player/" + action.name), action.name + " can't be rebound");
        }
    }

    [Test]
    public void TheRowsDontShareAKeyOutOfTheBox()
    {
        var seen = new Dictionary<string, string>();
        foreach (RebindRow row in InputRebinds.Rows)
        {
            if (!InputRebinds.HasBinding(row, GameInput.KeyboardMouse))
            {
                continue;
            }

            InputAction action = InputSystem.actions.FindAction(row.ActionPath);
            string path = action.bindings[InputRebinds.BindingIndex(action, row.Part, GameInput.KeyboardMouse)].effectivePath.ToLowerInvariant();
            if (row.Section == "Camera and Building" || seen.TryGetValue(path, out string other) && other == "Camera and Building")
            {
                // Build mode has its own map, so its keys may repeat those of the others.
                continue;
            }

            Assert.IsFalse(seen.ContainsKey(path), $"{row.Label} and {(seen.ContainsKey(path) ? seen[path] : "")} share {path}");
            seen[path] = row.Label;
        }
    }

    [Test]
    public void AKeyInUseIsFound()
    {
        RebindRow dodge = InputRebinds.Rows.First(row => row.ActionPath == "Player/Dodge");
        RebindRow interact = InputRebinds.Rows.First(row => row.ActionPath == "Player/Interact");
        InputAction action = InputSystem.actions.FindAction(interact.ActionPath);
        string key = action.bindings[InputRebinds.BindingIndex(action, null, GameInput.KeyboardMouse)].effectivePath;
        Assert.IsTrue(InputRebinds.TryFindConflict(dodge, GameInput.KeyboardMouse, key, out RebindRow other));
        Assert.AreEqual("Interact", other.Label);
        Assert.IsFalse(InputRebinds.TryFindConflict(interact, GameInput.KeyboardMouse, key, out _), "a row doesn't conflict with itself");
    }

    [Test]
    public void BuildKeysDontCountAsInUseElsewhere()
    {
        RebindRow rotate = InputRebinds.Rows.First(row => row.ActionPath == "Build/Rotate");
        RebindRow interact = InputRebinds.Rows.First(row => row.ActionPath == "Player/Interact");
        InputAction action = InputSystem.actions.FindAction(interact.ActionPath);
        string key = action.bindings[InputRebinds.BindingIndex(action, null, GameInput.KeyboardMouse)].effectivePath;
        Assert.IsFalse(InputRebinds.TryFindConflict(rotate, GameInput.KeyboardMouse, key, out _));
    }

    [Test]
    public void ARebindShowsInTheLabelsAndResetBringsItBack()
    {
        InputAction action = GameInput.SwapWeapon;
        string original = GameInput.KeyLabel(action);
        action.ApplyBindingOverride(InputRebinds.BindingIndex(action, null, GameInput.KeyboardMouse), "<Keyboard>/k");
        Assert.AreEqual("K", GameInput.KeyLabel(action));
        InputSystem.actions.RemoveAllBindingOverrides();
        Assert.AreEqual(original, GameInput.KeyLabel(action));
    }

    [Test]
    public void UltraShadows_AreTheDefault()
    {
        // The ground's own relief shadows only resolve in an 8K sun shadow map at the usual top-down zoom (2026-11-09).
        // The top choice is the default, however many choices there are and whatever they are called.
        Assert.AreEqual(GameOptions.Shadows.Choices.Length - 1, GameOptions.Shadows.Default, 1e-4f);
    }
}
