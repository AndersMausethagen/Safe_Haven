/// <summary>What brought a topple on (MonsterAI.LastToppleCause): the boss's try row counts each (PlaytestLog.BossTry).</summary>
public enum ToppleCause
{
    /// <summary>Its poise worn through by blows.</summary>
    Blows = 0,
    /// <summary>A parry broke it.</summary>
    Parry = 1,
    /// <summary>A hammer's Crush on poise worn low.</summary>
    Crush = 2,
    /// <summary>A strike that ends in a topple (the Heartroot's Take Root), or one broken off on the way.</summary>
    Strike = 3,
    /// <summary>Every summon of one call killed in time (Monster Attack › Bond Topple Seconds).</summary>
    Bond = 4,
}
