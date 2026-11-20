using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Options screen's unsaved changes (the user, 2026-11-20: "Give us the option to Save changes and not automatically change settings
/// when we press the button. Prompt the player that he has unsaved settings"). Every row edits this draft instead of the setting; Save
/// applies the whole draft, Discard forgets it, and leaving with changes asks first (OptionsScreen). A change back to the saved value is
/// no change. Keys are the GameOption itself or a name for settings kept elsewhere (volumes, camera, the window). Plain C#: no Unity
/// objects, so it is tested without the screen.
/// </summary>
public sealed class OptionsDraft
{
    private sealed class Change
    {
        public string Label;
        public Func<float> Saved;
        public Action<float> Apply;
        public float Value;
        public int Order;
        public bool First;
    }

    private readonly Dictionary<object, Change> changes = new Dictionary<object, Change>();
    private int order;

    /// <summary>Raised whenever the draft gains, changes or loses a change.</summary>
    public event Action Changed;

    public int Count => changes.Count;
    public bool HasChanges => changes.Count > 0;

    /// <summary>True when <paramref name="key"/> has a change waiting to be saved.</summary>
    public bool IsChanged(object key) => key != null && changes.ContainsKey(key);

    /// <summary>The value the screen shows: the unsaved one, or the saved one when there is none.</summary>
    public float Get(object key, Func<float> saved) => key != null && changes.TryGetValue(key, out Change change) ? change.Value : saved();

    /// <summary>
    /// Records <paramref name="value"/> for <paramref name="key"/>, to be applied with <paramref name="apply"/> on Save. Setting it back
    /// to what is saved removes the change. <paramref name="first"/> changes are applied before the rest (a quality preset before the
    /// settings it sets).
    /// </summary>
    public void Set(object key, string label, Func<float> saved, Action<float> apply, float value, bool first = false)
    {
        if (key == null || saved == null || apply == null)
        {
            return;
        }

        if (Mathf.Approximately(saved(), value))
        {
            if (changes.Remove(key))
            {
                Changed?.Invoke();
            }

            return;
        }

        if (changes.TryGetValue(key, out Change change))
        {
            if (Mathf.Approximately(change.Value, value))
            {
                return;
            }

            change.Value = value;
        }
        else
        {
            changes[key] = new Change { Label = label, Saved = saved, Apply = apply, Value = value, Order = order++, First = first };
        }

        Changed?.Invoke();
    }

    // ----- A GameOption -----

    public float Get(GameOption option) => Get(option, () => option.Value);

    public bool On(GameOption option) => Get(option) > 0.5f;

    public int Index(GameOption option) => Mathf.RoundToInt(Get(option));

    /// <summary>Records a new value for a setting, cleaned as the setting would clean it.</summary>
    public void Set(GameOption option, float value, bool first = false)
    {
        if (option != null)
        {
            Set(option, option.Label, () => option.Value, applied => option.Value = applied, option.Cleaned(value), first);
        }
    }

    /// <summary>The names of what is changed, in the order they were changed.</summary>
    public List<string> Labels()
    {
        var ordered = new List<Change>(changes.Values);
        ordered.Sort((a, b) => a.Order.CompareTo(b.Order));
        return ordered.ConvertAll(change => change.Label);
    }

    /// <summary>Applies every change (those marked first before the rest, otherwise in the order made) and empties the draft.</summary>
    public void Apply()
    {
        var ordered = new List<Change>(changes.Values);
        ordered.Sort((a, b) => a.First != b.First ? (a.First ? -1 : 1) : a.Order.CompareTo(b.Order));
        changes.Clear();
        foreach (Change change in ordered)
        {
            change.Apply(change.Value);
        }

        Changed?.Invoke();
    }

    /// <summary>Forgets every change: the screen shows what is saved again.</summary>
    public void Discard()
    {
        if (changes.Count == 0)
        {
            return;
        }

        changes.Clear();
        Changed?.Invoke();
    }
}
