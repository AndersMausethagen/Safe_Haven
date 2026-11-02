/// <summary>
/// Identifies a UI window so input can ask for it without referencing the window itself. Values are serialized as
/// numbers, so add new windows at the end and never renumber.
/// </summary>
public enum UIWindowId
{
    /// <summary>The old raw resource bars. Nothing opens them now: essences are shown in the bag.</summary>
    Resources = 0,
    /// <summary>The item bag and crafting (B).</summary>
    Items = 1,
    /// <summary>Choosing modifiers and entering a raid, opened at a raid portal.</summary>
    RaidPreparation = 2,
    /// <summary>What happened in the last raid, shown on returning to Safe Haven.</summary>
    RaidSummary = 3,
    /// <summary>The stash in Safe Haven, opened at the stash chest.</summary>
    Stash = 4,
    /// <summary>The skill book and skill bar loadout (K).</summary>
    Skills = 5,
    /// <summary>The shared menu for the Heart Tree, portal, residents, altar, repairs and building upgrades.</summary>
    ActionMenu = 6,
    /// <summary>The character screen: worn gear and stats (C).</summary>
    Character = 7,
    /// <summary>The Escape menu: continue, save, load, options and quit. Pauses the game while open.</summary>
    PauseMenu = 8,
    /// <summary>A crafting station's recipes, queue and output, opened at the station.</summary>
    Station = 9,
    /// <summary>A conversation: the speaker's portrait, their line and the player's answers (DialogueBox).</summary>
    Dialogue = 10,
    /// <summary>Level, experience and the four talent trees (N).</summary>
    Talents = 11,
    /// <summary>The overview of Safe Haven: its rank, renown, people and what to do next (H).</summary>
    Haven = 12,
    /// <summary>The Purification Altar: purifying, empowering and enchanting gear, deepening skills.</summary>
    Altar = 13,
    /// <summary>A cart's or boat's cargo beside the bag.</summary>
    Cargo = 14,
    /// <summary>The build menu (G): every known building as a picture card, by category.</summary>
    Build = 15,
    /// <summary>The cheat menu for playtesting (F1).</summary>
    Cheats = 16,
}
