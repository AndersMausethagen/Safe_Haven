using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A page of the journal's written tabs: a piece of the backstory, or how something in the game works.</summary>
[Serializable]
public sealed class JournalPage
{
    [Tooltip("The shelf it sits on in the list.")]
    public string group;
    public string title;
    [Tooltip("One short line under the title in the list.")]
    public string line;
    [TextArea(6, 30)] public string body;
    [Tooltip("Hidden until this quest has started. Empty: no quest is needed.")]
    public QuestDefinition unlockedByQuest;
    [Tooltip("Hidden until this creature has been met. Empty: no creature is needed.")]
    public MonsterDefinition unlockedByCreature;
    [Tooltip("Hidden until this person has come home. Empty: nobody is needed.")]
    public ResidentDefinition unlockedByResident;
    [Tooltip("Hidden until it is found in a raid: searching a cache can turn it up, in the order the pages are listed.")]
    public bool foundInRaids;
    [Tooltip("Hidden until the player first tries this skill (swings at a tree, casts a line, lands a blow). A mechanics page that opens is shown as a tutorial.")]
    public ProficiencyDefinition unlockedBySkill;
    [Tooltip("Hidden until the player first does this in a fight (JournalNotices.Techniques: guard, parry, dodge, stagger, stamina, " +
             "hurt), so the combat pages open one at a time as each comes up, not all at the first blow. Empty: none needed.")]
    public string unlockedByTechnique;
    [Tooltip("A painting of it, for the tutorial window and the journal (LocalSpawn: Tools/LocalSpawn/tutorial_art.json). Mechanics pages only.")]
    public Sprite art;
    [Tooltip("What it is, in a sentence or two, above how it works (the body). Mechanics pages only.")]
    [TextArea(2, 4)] public string summary;
}

/// <summary>A cinematic the journal (Backstory tab) and the main menu (Cinematics) offer to watch again.</summary>
[Serializable]
public sealed class JournalCinematic
{
    public CinematicDefinition cinematic;
    [Tooltip("What it tells, in a line, under its title.")]
    public string line;
    [Tooltip("Listed in a character's journal once this quest is done (the quest whose end plays it). Empty: no quest is needed.")]
    public QuestDefinition afterQuest;
    [Tooltip("Listed in a character's journal once they have entered this raid (its first visit plays it). Empty: no raid is needed.")]
    public RaidDefinition afterRaid;
}

/// <summary>What someone at home has to say about a creature, shown on its page once they are back.</summary>
[Serializable]
public sealed class ResidentNote
{
    public ResidentDefinition who;
    public MonsterDefinition about;
    [TextArea(2, 6)] public string text;
}

/// <summary>
/// What the journal (J) has to say that isn't already in the game's data: the backstory, how things work, which creatures it lists and what people say of
/// them. The Quests tab reads the quest log, and each quest's own Journal Notes; the Creatures tab reads each creature's Journal Lore, Advice and Habitat.
/// Built by AgentScripts/BuildJournal.cs; lives at Resources/JournalSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Journal Settings", fileName = "JournalSettings")]
public sealed class JournalSettings : GameSettings<JournalSettings>
{
    [SerializeField] private List<JournalPage> backstory = new List<JournalPage>();
    [SerializeField] private List<JournalPage> mechanics = new List<JournalPage>();
    [Tooltip("Every creature the Creatures tab lists, in the order it lists them. One not met yet shows as unknown.")]
    [SerializeField] private List<MonsterDefinition> creatures = new List<MonsterDefinition>();
    [SerializeField] private List<ResidentNote> residentNotes = new List<ResidentNote>();
    [Tooltip("The chance a searched cache in a raid holds the next page still to be found.")]
    [SerializeField, Range(0f, 1f)] private float findChance = 0.2f;
    [Tooltip("The cinematics that can be watched again, in story order: in the journal once the character has reached each, " +
             "and in the main menu once it has been seen on this computer.")]
    [SerializeField] private List<JournalCinematic> cinematics = new List<JournalCinematic>();

    public IReadOnlyList<JournalPage> Backstory => backstory;
    public IReadOnlyList<JournalCinematic> Cinematics => cinematics;
    public IReadOnlyList<JournalPage> Mechanics => mechanics;
    public IReadOnlyList<MonsterDefinition> Creatures => creatures;
    public IReadOnlyList<ResidentNote> ResidentNotes => residentNotes;
    public float FindChance => findChance;
}
