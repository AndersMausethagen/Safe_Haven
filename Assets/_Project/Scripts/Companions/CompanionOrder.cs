/// <summary>
/// What the player has asked a companion to do (GDD §15.5: follow, hold, and more). Each order is a rule in
/// <see cref="CompanionTactics"/>; the companion's definition says which orders it takes and what it answers.
/// Saved and serialized: add new orders at the end and never renumber.
/// </summary>
public enum CompanionOrder
{
    /// <summary>Walks with the leader and joins any fight near them.</summary>
    Follow = 0,
    /// <summary>Holds a spot and fights only what comes close to it.</summary>
    Stay = 1,
    /// <summary>Keeps close to the leader and fights only what attacks the leader or itself.</summary>
    Defend = 2,
    /// <summary>Keeps out of the fight, behind the leader and away from enemies. Never attacks.</summary>
    Retreat = 3,
    /// <summary>Goes after whatever the leader last hit, wherever it is; then follows again.</summary>
    AttackTarget = 4,
}
