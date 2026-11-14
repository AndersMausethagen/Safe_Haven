/// <summary>
/// How a physical blow lands: crushing, cutting or piercing (the user, 2026-11-14). It is a property of the attack,
/// alongside its Damage Type, not a new damage type: armour, physical power and crits treat every physical blow alike.
/// What differs is the target. Each kind has a resistance stat (Blunt/Slash/Stab Resistance); below zero is a weakness,
/// so a wall or a stone giant can take more from a hammer and less from a blade. Values are serialized as numbers: add
/// new kinds at the end and never renumber.
/// </summary>
public enum StrikeKind
{
    /// <summary>Not said: no weakness or resistance applies (spells, falls, anything not a blow).</summary>
    None = 0,
    /// <summary>Crushing: hammers, clubs, fists, a troll's fist or stomp.</summary>
    Blunt = 1,
    /// <summary>Cutting: swords, axes, claws.</summary>
    Slash = 2,
    /// <summary>Piercing: spears, daggers' thrusts, arrows, tusks and fangs.</summary>
    Stab = 3,
}

public static class StrikeKindExtensions
{
    /// <summary>The resistance stat for this kind of blow, or false for <see cref="StrikeKind.None"/>.</summary>
    public static bool TryResistanceStat(this StrikeKind kind, out StatType stat)
    {
        switch (kind)
        {
            case StrikeKind.Blunt:
                stat = StatType.BluntResistance;
                return true;
            case StrikeKind.Slash:
                stat = StatType.SlashResistance;
                return true;
            case StrikeKind.Stab:
                stat = StatType.StabResistance;
                return true;
            default:
                stat = default;
                return false;
        }
    }

    public static string Label(this StrikeKind kind) => kind == StrikeKind.None ? "" : kind.ToString();
}
