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

    public IReadOnlyList<JournalPage> Backstory => backstory;
    public IReadOnlyList<JournalPage> Mechanics => mechanics;
    public IReadOnlyList<MonsterDefinition> Creatures => creatures;
    public IReadOnlyList<ResidentNote> ResidentNotes => residentNotes;
    public float FindChance => findChance;
}
