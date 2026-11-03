using System;

public enum MessageKind
{
    Info,
    /// <summary>Something was gained: "+3 Oak Wood".</summary>
    Gain,
    /// <summary>Something was refused or went wrong: "Needs a Tier II Pickaxe".</summary>
    Warning,
}

/// <summary>
/// A small channel for short on-screen messages. Gameplay posts ("+3 Oak Wood", "Needs a Tier II Pickaxe") and the HUD
/// shows them; neither knows the other, so systems can report to the player without referencing any UI.
/// <para>
/// Use this for text meant for the player to read. To report that something happened so other systems can react
/// (quests, audio, scene changes), raise a GameEvent instead; the two are kept apart so wording can change freely.
/// </para>
/// </summary>
public static class GameMessages
{
    public static event Action<string, MessageKind> Posted;

    public static void Post(string text, MessageKind kind = MessageKind.Info)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Posted?.Invoke(GameInput.WithRebinds(text), kind);
        }
    }
}
