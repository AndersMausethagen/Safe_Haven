using System;
using System.Globalization;
using UnityEngine;

public enum OptionKind { Toggle, Slider, Choice }

/// <summary>
/// One setting the player can change: its name, what it does, its range or choices and its default. The value lives in
/// PlayerPrefs, not the save file, because it is a preference and applies to every save. The Options screen draws a row
/// for each without knowing what it is for; whoever uses a setting reads <see cref="Value"/>, <see cref="On"/> or
/// <see cref="Index"/>, and systems that must act at once listen to <see cref="AnyChanged"/>.
/// </summary>
public sealed class GameOption
{
    private const string Prefix = "SafeHaven.Option.";

    private readonly string key;
    private readonly string format;
    private float value;
    private bool loaded;

    private GameOption(string key, OptionKind kind, string label, string help, float defaultValue, float min, float max, float step, string format, string[] choices)
    {
        this.key = key;
        this.format = format;
        Kind = kind;
        Label = label;
        Help = help;
        Default = defaultValue;
        Min = min;
        Max = max;
        Step = step;
        Choices = choices;
    }

    /// <summary>Raised after a value changes.</summary>
    public static event Action<GameOption> AnyChanged;

    public OptionKind Kind { get; }
    public string Label { get; }
    /// <summary>One line on what it does, shown when the row is hovered.</summary>
    public string Help { get; }
    public float Default { get; }
    public float Min { get; }
    public float Max { get; }
    /// <summary>What the value snaps to. 0 is free.</summary>
    public float Step { get; }
    /// <summary>The names of a Choice, in order.</summary>
    public string[] Choices { get; }

    public static GameOption Toggle(string key, string label, string help, bool defaultValue) =>
        new GameOption(key, OptionKind.Toggle, label, help, defaultValue ? 1f : 0f, 0f, 1f, 1f, null, null);

    /// <param name="format">How the value reads: "{0:0}%" with the value times 100 when it is a share, set by <paramref name="percent"/>.</param>
    public static GameOption Slider(string key, string label, string help, float defaultValue, float min, float max, float step, bool percent = true) =>
        new GameOption(key, OptionKind.Slider, label, help, defaultValue, min, max, step, percent ? "%" : "", null);

    public static GameOption Choice(string key, string label, string help, int defaultIndex, params string[] choices) =>
        new GameOption(key, OptionKind.Choice, label, help, defaultIndex, 0f, choices.Length - 1, 1f, null, choices);

    /// <summary>
    /// Shows a value for a moment without saving it (the Options screen's Preview of a HUD setting, the systems review 2026-11-21):
    /// <see cref="Value"/> reads it until <see cref="EndPreview"/>, PlayerPrefs keep the saved one, and listeners hear both changes.
    /// </summary>
    public void Preview(float shown)
    {
        previewValue = Clean(shown);
        previewing = true;
        previewed.Add(this);
        AnyChanged?.Invoke(this);
    }

    // The options shown in a preview now: cleared when Play starts, as these options are static and outlive a session.
    private static readonly System.Collections.Generic.HashSet<GameOption> previewed = new System.Collections.Generic.HashSet<GameOption>();

    /// <summary>Back to the saved value after <see cref="Preview"/>; nothing when no preview is showing.</summary>
    public void EndPreview()
    {
        if (!previewing)
        {
            return;
        }

        previewing = false;
        previewed.Remove(this);
        AnyChanged?.Invoke(this);
    }

    public bool IsPreviewing => previewing;

    private bool previewing;
    private float previewValue;

    public float Value
    {
        get
        {
            if (previewing)
            {
                return previewValue;
            }

            if (!loaded)
            {
                value = Clean(PlayerPrefs.GetFloat(Prefix + key, Default));
                loaded = true;
            }

            return value;
        }
        set
        {
            if (previewing)
            {
                previewing = false;
                previewed.Remove(this);
                AnyChanged?.Invoke(this);
            }

            float cleaned = Clean(value);
            if (Mathf.Approximately(cleaned, Value))
            {
                return;
            }

            this.value = cleaned;
            PlayerPrefs.SetFloat(Prefix + key, cleaned);
            AnyChanged?.Invoke(this);
        }
    }

    public bool On => Value > 0.5f;
    public int Index => Mathf.RoundToInt(Value);
    public string ChoiceName => Choices != null ? Choices[Mathf.Clamp(Index, 0, Choices.Length - 1)] : "";

    /// <summary>The value as the player reads it: "On", "High", "85%".</summary>
    public string ValueText => TextFor(Value);

    /// <summary>Any value of this setting as the player reads it (the Options screen shows unsaved ones and the default).</summary>
    public string TextFor(float raw)
    {
        float value = Clean(raw);
        switch (Kind)
        {
            case OptionKind.Toggle:
                return value > 0.5f ? "On" : "Off";
            case OptionKind.Choice:
                return Choices != null ? Choices[Mathf.Clamp(Mathf.RoundToInt(value), 0, Choices.Length - 1)] : "";
            default:
                if (format == "%")
                {
                    return Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture) + "%";
                }

                // A slider either side of nothing (Brightness) reads as a signed amount: "+25", "-10", "0".
                return Mathf.Approximately(Min, -Max)
                    ? (value > 0f ? "+" : "") + Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture)
                    : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The value this setting would take if set to <paramref name="raw"/>: clamped to its range and snapped to its step.</summary>
    public float Cleaned(float raw) => Clean(raw);

    public void ResetToDefault() => Value = Default;

    private float Clean(float raw)
    {
        float clamped = Mathf.Clamp(raw, Min, Max);
        // Rounded so stepping leaves no float dust: a slider at its middle reads as exactly 0.
        float snapped = Step > 0f ? Min + Mathf.Round((clamped - Min) / Step) * Step : clamped;
        return Mathf.Clamp(Mathf.Round(snapped * 10000f) / 10000f, Min, Max);
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        AnyChanged = null;
        foreach (GameOption option in previewed)
        {
            option.previewing = false;
        }

        previewed.Clear();
    }
}
