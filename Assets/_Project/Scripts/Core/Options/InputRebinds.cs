using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One line of the controls list: what the player calls it, the action, and the part of a composite (Move's "up").</summary>
public readonly struct RebindRow
{
    public readonly string Label;
    public readonly string Section;
    public readonly string ActionPath;
    public readonly string Part;

    public RebindRow(string section, string label, string actionPath, string part = null)
    {
        Section = section;
        Label = label;
        ActionPath = actionPath;
        Part = part;
    }
}

/// <summary>
/// Changing the controls: which actions can be rebound, finding the binding behind a row, waiting for the new key, and
/// keeping the result. Overrides sit on top of the Input Actions asset and are kept in PlayerPrefs, so the asset stays as
/// authored and <see cref="GameInput.KeyLabel"/> follows them. A key already used by another action swaps with it, so
/// nothing is ever left without a key. Escape is the game's own and can't be taken.
/// </summary>
public static class InputRebinds
{
    private const string Key = "SafeHaven.Bindings";

    public static readonly IReadOnlyList<RebindRow> Rows = new[]
    {
        new RebindRow("Movement", "Move Forward", "Player/Move", "up"),
        new RebindRow("Movement", "Move Back", "Player/Move", "down"),
        new RebindRow("Movement", "Move Left", "Player/Move", "left"),
        new RebindRow("Movement", "Move Right", "Player/Move", "right"),
        new RebindRow("Movement", "Dodge", "Player/Dodge"),
        new RebindRow("Movement", "Sneak", "Player/Sneak"),
        new RebindRow("Movement", "Interact", "Player/Interact"),
        new RebindRow("Movement", "Stop Working", "Player/CancelInteraction"),

        new RebindRow("Combat", "Attack", "Player/Attack"),
        new RebindRow("Combat", "Guard / Aim", "Player/Guard"),
        new RebindRow("Combat", "Draw Weapon", "Player/ToggleCombat"),
        new RebindRow("Combat", "Swap Weapons", "Player/SwapWeapon"),
        new RebindRow("Combat", "Torch", "Player/ToggleTorch"),
        new RebindRow("Combat", "Weapon Art / Next Arrow", "Player/CycleArrows"),
        new RebindRow("Combat", "Skill 1", "Player/Skill1"),
        new RebindRow("Combat", "Skill 2", "Player/Skill2"),
        new RebindRow("Combat", "Skill 3", "Player/Skill3"),
        new RebindRow("Combat", "Skill 4", "Player/Skill4"),
        new RebindRow("Combat", "Skill 5", "Player/Skill5"),

        new RebindRow("Windows", "Bag (second key)", "Player/Inventory"),
        new RebindRow("Windows", "Bag", "Player/Items"),
        new RebindRow("Windows", "Skills", "Player/Skills"),
        new RebindRow("Windows", "Character", "Player/Character"),
        new RebindRow("Windows", "Build", "Player/Build"),
        new RebindRow("Windows", "Talents", "Player/Talents"),
        new RebindRow("Windows", "Journal", "Player/Journal"),
        new RebindRow("Windows", "Haven", "Player/Haven"),
        new RebindRow("Windows", "World Map", "Player/WorldMap"),
        new RebindRow("Windows", "Command Wheel", "Player/CommandWheel"),

        new RebindRow("Camera and Building", "Camera Style", "Camera/ToggleStyle"),
        new RebindRow("Camera and Building", "Turn Camera", "Camera/Orbit"),
        new RebindRow("Camera and Building", "Rotate Building", "Build/Rotate"),
        new RebindRow("Camera and Building", "Fine Rotate", "Build/FineRotate"),
        new RebindRow("Camera and Building", "Free Placement", "Build/FreePlace"),

        new RebindRow("Game", "Quick Save", "System/QuickSave"),
        new RebindRow("Game", "Quick Load", "System/QuickLoad"),
    };

    private static InputActionRebindingExtensions.RebindingOperation operation;

    private static int finishedFrame = -1;

    /// <summary>True while waiting for the player to press the new key, and just after: the Escape that cancels it means nothing else.</summary>
    public static bool IsListening => operation != null || Time.frameCount - finishedFrame <= 1;

    /// <summary>Puts back what the player chose earlier. Called once when the game starts.</summary>
    public static void Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json) && InputSystem.actions != null)
        {
            InputSystem.actions.LoadBindingOverridesFromJson(json);
        }
    }

    /// <summary>Back to the keys the game came with.</summary>
    public static void ResetAll()
    {
        if (InputSystem.actions != null)
        {
            InputSystem.actions.RemoveAllBindingOverrides();
        }

        PlayerPrefs.DeleteKey(Key);
    }

    private static void Save()
    {
        PlayerPrefs.SetString(Key, InputSystem.actions.SaveBindingOverridesAsJson());
    }

    /// <summary>The binding of a row for a control scheme, or -1 when it has none there.</summary>
    public static int BindingIndex(InputAction action, string part, string scheme)
    {
        if (action == null)
        {
            return -1;
        }

        InputBinding group = InputBinding.MaskByGroup(scheme);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            bool right = part == null
                ? !binding.isComposite && !binding.isPartOfComposite
                : binding.isPartOfComposite && string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase);
            if (right && group.Matches(binding))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>True when the row has a key in this scheme. Stopping work is Escape on the keyboard, so it has none there.</summary>
    public static bool HasBinding(RebindRow row, string scheme)
    {
        return InputSystem.actions != null && BindingIndex(InputSystem.actions.FindAction(row.ActionPath), row.Part, scheme) >= 0;
    }

    /// <summary>How the row's key reads now, or a dash.</summary>
    public static string Describe(RebindRow row, string scheme)
    {
        InputAction action = InputSystem.actions != null ? InputSystem.actions.FindAction(row.ActionPath) : null;
        int index = BindingIndex(action, row.Part, scheme);
        return index >= 0 ? action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions) : "–";
    }

    /// <summary>The other row in this scheme that uses the same key, if any.</summary>
    public static bool TryFindConflict(RebindRow row, string scheme, string effectivePath, out RebindRow other)
    {
        foreach (RebindRow candidate in Rows)
        {
            // Build mode's keys only count while building, so they may repeat the others.
            if ((candidate.ActionPath == row.ActionPath && candidate.Part == row.Part) || IsBuildKey(candidate) != IsBuildKey(row))
            {
                continue;
            }

            InputAction action = InputSystem.actions.FindAction(candidate.ActionPath);
            int index = BindingIndex(action, candidate.Part, scheme);
            if (index >= 0 && SamePath(action.bindings[index].effectivePath, effectivePath))
            {
                other = candidate;
                return true;
            }
        }

        other = default;
        return false;
    }

    private static bool IsBuildKey(RebindRow row) => row.ActionPath.StartsWith("Build/", StringComparison.Ordinal);

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Waits for the next key or button for this row. Escape cancels. <paramref name="done"/> gets a line for the player: what
    /// it swapped with, or null. The game's other input is off meanwhile, so the key does nothing else.
    /// </summary>
    public static void Listen(RebindRow row, string scheme, Action<string> done)
    {
        InputAction action = InputSystem.actions.FindAction(row.ActionPath);
        int index = BindingIndex(action, row.Part, scheme);
        if (index < 0 || IsListening)
        {
            done?.Invoke(null);
            return;
        }

        string before = action.bindings[index].effectivePath;
        GameInput.SuspendGameplay();
        var rebind = action.PerformInteractiveRebinding(index)
            .WithExpectedControlType("Button")
            .WithBindingGroup(scheme)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .OnMatchWaitForAnother(0.1f);
        rebind = scheme == GameInput.Gamepad ? rebind.WithControlsHavingToMatchPath("<Gamepad>") : rebind.WithControlsExcluding("<Gamepad>");
        operation = rebind
            .OnComplete(_ =>
            {
                string note = null;
                string chosen = action.bindings[index].effectivePath;
                if (TryFindConflict(row, scheme, chosen, out RebindRow other))
                {
                    InputAction otherAction = InputSystem.actions.FindAction(other.ActionPath);
                    otherAction.ApplyBindingOverride(BindingIndex(otherAction, other.Part, scheme), before);
                    note = $"Swapped with {other.Label}.";
                }

                Finish();
                Save();
                done?.Invoke(note);
            })
            .OnCancel(_ =>
            {
                Finish();
                done?.Invoke(null);
            })
            .Start();
    }

    private static void Finish()
    {
        operation?.Dispose();
        operation = null;
        finishedFrame = Time.frameCount;
        GameInput.ResumeGameplay();
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        operation = null;
        finishedFrame = -1;
    }
}
