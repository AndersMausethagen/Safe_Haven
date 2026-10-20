/// <summary>How a modifier changes a stat.</summary>
public enum StatModifierOp
{
    /// <summary>Adds the value to the stat.</summary>
    Flat = 0,
    /// <summary>Adds a share of the stat: 0.25 is +25%. Percent modifiers on the same stat add together.</summary>
    Percent = 1,
}

/// <summary>
/// One change to a stat, from gear, a buff, a skill or an ability. The source identifies who added it so the whole
/// batch can be removed again (StatSheet.RemoveModifiersFrom), the way V Rising removes a buff's stat modifications.
/// </summary>
public readonly struct StatModifier
{
    public readonly StatType Stat;
    public readonly StatModifierOp Op;
    public readonly float Value;
    public readonly object Source;

    public StatModifier(StatType stat, StatModifierOp op, float value, object source)
    {
        Stat = stat;
        Op = op;
        Value = value;
        Source = source;
    }
}
