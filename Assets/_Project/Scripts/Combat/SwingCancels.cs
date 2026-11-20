using System;

/// <summary>
/// What may break a player's swing off, before its blow (it then deals no damage; what it cost stays spent) or after it (the
/// follow-through cut short). Each attack can say for itself (AttackDefinition › Cancels), the rest by kind from Combat Technique
/// Settings › Cancels; <see cref="SwingMotion.WindupCancels"/> and <see cref="SwingMotion.RecoveryCancels"/> resolve them. After God
/// of War's cancel rules (Eric Williams): quick blows give way to a block or a roll before they land, big ones only to the roll.
/// </summary>
[Flags]
public enum SwingCancels
{
    None = 0,
    Dodge = 1,
    Guard = 2,
}
