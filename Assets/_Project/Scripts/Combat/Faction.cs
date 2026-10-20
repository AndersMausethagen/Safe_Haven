/// <summary>
/// Which side something fights for. A hit only lands on a different faction, so the player's attacks pass through their
/// own buildings, and Neutral objects (crates, props) can be broken by anyone.
/// </summary>
public enum Faction
{
    Player = 0,
    Enemy = 1,
    Neutral = 2,
}

/// <summary>
/// Who AI starts fights with. This is separate from what a landed hit can damage: Neutral objects can still be broken,
/// but Neutral AI never picks a target or retaliates.
/// </summary>
public static class FactionRelations
{
    public static bool WillAttack(this Faction attacker, Faction target)
    {
        switch (attacker)
        {
            case Faction.Enemy:
                return target == Faction.Player;
            case Faction.Player:
                return target == Faction.Enemy;
            default:
                return false;
        }
    }
}
