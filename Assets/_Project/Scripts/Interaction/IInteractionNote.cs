/// <summary>
/// Optional, next to an IInteractionCondition: when IsNote is true, the condition's blocked reason is a note rather than a
/// problem (a quest giver saying "Come back when it's done"), so the interaction marker shows it in a soft color, not red.
/// </summary>
public interface IInteractionNote
{
    bool IsNote { get; }
}
