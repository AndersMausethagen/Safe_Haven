using UnityEngine;

/// <summary>
/// Which side something is on. The player and their allies form one side and never hurt each other; enemies hunt that
/// side; Neutral objects (crates, props) can be broken by anyone. Quests count kills by the side that fell
/// (QuestObjective: "kill 5 enemies"). Saved and serialized: add new factions at the end and never renumber.
/// </summary>
public enum Faction
{
    /// <summary>The player, and what belongs to them (their buildings).</summary>
    Player = 0,
    /// <summary>Monsters and anyone else hostile to the player's side.</summary>
    Enemy = 1,
    /// <summary>Takes no side: props, crates, wildlife that never starts a fight.</summary>
    Neutral = 2,
    /// <summary>Friendly people on the player's side: companions, rescued travellers, residents.</summary>
    Ally = 3,
}

/// <summary>
/// Who AI starts fights with, and whose hits land on whom. Starting fights is separate from what a landed hit can
/// damage: Neutral objects can still be broken, but Neutral AI never picks a target or retaliates.
/// </summary>
public static class FactionRelations
{
    /// <summary>True when AI of <paramref name="attacker"/>'s side starts a fight with <paramref name="target"/>.</summary>
    public static bool WillAttack(this Faction attacker, Faction target)
    {
        switch (attacker)
        {
            case Faction.Enemy:
                return target.IsPlayerSide();
            case Faction.Player:
            case Faction.Ally:
                return target == Faction.Enemy;
            default:
                return false;
        }
    }

    /// <summary>The side <paramref name="actor"/> fights on (its Combatant); null when it takes no part in fights.</summary>
    public static Faction? SideOf(GameObject actor)
    {
        Combatant combatant = actor != null ? actor.GetComponentInParent<Combatant>() : null;
        return combatant != null ? combatant.Faction : (Faction?)null;
    }

    /// <summary>The player and their allies.</summary>
    public static bool IsPlayerSide(this Faction faction) => faction == Faction.Player || faction == Faction.Ally;

    /// <summary>On the same side: hits between them don't land (no friendly fire).</summary>
    public static bool AreAllies(this Faction faction, Faction other) =>
        faction == other || (faction.IsPlayerSide() && other.IsPlayerSide());

    /// <summary>How quests and text name one of the side: "enemy", "ally".</summary>
    public static string Noun(this Faction faction, bool plural)
    {
        switch (faction)
        {
            case Faction.Enemy:
                return plural ? "enemies" : "enemy";
            case Faction.Ally:
                return plural ? "allies" : "ally";
            case Faction.Neutral:
                return plural ? "creatures" : "creature";
            default:
                return plural ? "players" : "player";
        }
    }
}
