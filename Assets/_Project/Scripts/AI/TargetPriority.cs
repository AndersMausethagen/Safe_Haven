/// <summary>
/// Who a hunting monster goes for when it can see more than one target: a priority target (Combatant › Priority Target:
/// the player; "player" below) comes first while it can reach them;
/// anyone else on the player's side (a companion, a traveller) only when the player is out of sight or out of reach, and
/// then the nearest (switching only to a clearly nearer one, so it doesn't flip between two). Plain C#, tested in
/// CompanionTests; MonsterTargeting asks it on every target scan.
/// </summary>
public static class TargetPriority
{
    public enum Choice
    {
        Keep,
        Player,
        Other,
    }

    /// <param name="huntingPlayer">Its current target is the player.</param>
    /// <param name="playerSeen">The player is in sight (not needed while already hunting them).</param>
    /// <param name="playerReachable">It can walk to within striking distance of the player.</param>
    /// <param name="otherSeen">Someone else it could hunt is in sight.</param>
    /// <param name="otherReachable">It can reach that someone.</param>
    /// <param name="otherClearlyNearer">That someone is clearly nearer than the current target (or there is none).</param>
    public static Choice Decide(bool huntingPlayer, bool playerSeen, bool playerReachable, bool otherSeen,
        bool otherReachable, bool otherClearlyNearer)
    {
        if (huntingPlayer)
        {
            return !playerReachable && otherSeen && otherReachable ? Choice.Other : Choice.Keep;
        }

        if (playerSeen && playerReachable)
        {
            return Choice.Player;
        }

        return otherSeen && otherClearlyNearer ? Choice.Other : Choice.Keep;
    }
}
