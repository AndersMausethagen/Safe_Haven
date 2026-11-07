using System;
using UnityEngine;

/// <summary>Something a quest can ask the player to talk to, reach, use or rescue: a person (ResidentDefinition) or a
/// place or thing (QuestTargetDefinition). Its id is what saves remember the player did to it.</summary>
public interface IQuestTarget
{
    string QuestId { get; }
    string DisplayName { get; }
}

/// <summary>A component that is someone: a resident, an expedition member, a companion. Talking to its object counts as
/// talking to them (Talkable), with no field to fill in.</summary>
public interface IQuestPerson
{
    ResidentDefinition Resident { get; }
}

/// <summary>Marks a field that says who or what its object is for quests (a Talkable's or Quest Zone's target), so the
/// quest tools can find where in the scenes a quest's person, place or thing is.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class QuestIdentityAttribute : PropertyAttribute
{
}
