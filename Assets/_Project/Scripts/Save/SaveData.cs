using System;
using System.Collections.Generic;
using UnityEngine;

// The shape of a save file on disk. These are plain data written with JsonUtility: content is referred to by stable id
// and nothing here points at assets or scene objects, so a save stays readable when assets move or the game changes.
// Add fields at the end and raise SaveMapper.CurrentVersion when the meaning of existing data changes.

[Serializable]
public sealed class SaveData
{
    public int version;
    /// <summary>When the game was saved, in UTC, as an ISO 8601 round-trip string.</summary>
    public string savedAtUtc;
    /// <summary>The scene the player was in.</summary>
    public string scene;
    public bool hasPlayerPose;
    public Vector3 playerPosition;
    public float playerYaw;
    public PlayerSaveData player = new PlayerSaveData();
    public StashSaveData stash = new StashSaveData();
    /// <summary>The remembered state of every persistent scene. Missing in older saves, which then load with fresh scenes.</summary>
    public List<SceneSaveData> world = new List<SceneSaveData>();
    /// <summary>Quests that have started, with their progress. Missing in older saves, which then start with no quests done.</summary>
    public List<QuestSaveData> quests = new List<QuestSaveData>();
    /// <summary>Safe Haven as a place: portal, ward, residents, breach, purification. Missing in older saves.</summary>
    public SettlementSaveData settlement = new SettlementSaveData();
    /// <summary>The playtest run this save belongs to. Missing in older saves, which then start a new run.</summary>
    public PlaytestSaveData playtest = new PlaytestSaveData();
    /// <summary>Ids of Quest Events raised so far, so one-time world moments stay done. Missing in older saves.</summary>
    public List<string> happenedEvents = new List<string>();
    /// <summary>Level, experience and talents. Missing in older saves (they start at level 1).</summary>
    public ProgressSaveData progress = new ProgressSaveData();
    /// <summary>What the player calls this save. Missing in older saves, which then go by their file.</summary>
    public string displayName;
    /// <summary>How the player looks and their name. Missing in older saves, which then get the starting look.</summary>
    public CharacterAppearance appearance = new CharacterAppearance();
}

/// <summary>A PlaytestRun.</summary>
[Serializable]
public sealed class PlaytestSaveData
{
    public string runId;
    public float playedSeconds;
    public int sessions;
}

[Serializable]
public sealed class SettlementSaveData
{
    public bool portalRepaired;
    public int wardLevel;
    /// <summary>Highest Haven rank reached. 0 in older saves.</summary>
    public int havenRank;
    public int successfulExtractions;
    /// <summary>A BreachStatus.</summary>
    public int breach;
    /// <summary>Ward breaches beaten back. Missing in older saves, where a Won breach counts as one.</summary>
    public int breachesWon;
    /// <summary>Successful extractions at which the next breach comes due. Missing in older saves.</summary>
    public int nextBreachAt;
    public int breachPendingSince;
    /// <summary>Successful raids until the ward has recovered from a lost breach.</summary>
    public int wardStrain;
    /// <summary>Share taken off the next breach. Missing in older saves.</summary>
    public float breachWeakening;
    public List<ResidentSaveData> residents = new List<ResidentSaveData>();
    public int purificationQueued;
    public float purificationProgress;
    /// <summary>Share faster the altar works for its upgrades. Missing in older saves; the altar sets it again at home.</summary>
    public float purificationBonus;
    /// <summary>Every crafting station's queue and output slot. Missing in older saves.</summary>
    public List<StationSaveData> stations = new List<StationSaveData>();
}

/// <summary>A crafting station's queue (StationQueue), keyed by the station's id.</summary>
[Serializable]
public sealed class StationSaveData
{
    public string id;
    /// <summary>A CraftingStationType.</summary>
    public int type;
    public int capacity;
    public int maxQueued;
    public List<StationJobSaveData> jobs = new List<StationJobSaveData>();
    public float progress;
    /// <summary>The id of what lies in the output slot, or empty.</summary>
    public string output;
    public bool outputIsResource;
    public int outputAmount;
}

[Serializable]
public sealed class StationJobSaveData
{
    /// <summary>The recipe's id.</summary>
    public string recipe;
    public int count;
}

[Serializable]
public sealed class ResidentSaveData
{
    public string id;
    /// <summary>The job's id, or empty while unassigned.</summary>
    public string job;
}

[Serializable]
public sealed class ProgressSaveData
{
    public int level = 1;
    public int experience;
    public List<AmountSaveData> talents = new List<AmountSaveData>();
    /// <summary>Ranks deepened at the altar, by skill id. Missing in older saves.</summary>
    public List<AmountSaveData> skillRanks = new List<AmountSaveData>();
    public List<string> crafted = new List<string>();
    public List<string> stones = new List<string>();
}

[Serializable]
public sealed class QuestSaveData
{
    public string id;
    /// <summary>1 active, 2 completed.</summary>
    public int status;
    /// <summary>Progress per objective, in the quest's objective order.</summary>
    public List<int> counts = new List<int>();
}

[Serializable]
public sealed class SceneSaveData
{
    public string scene;
    public List<EntitySaveData> entities = new List<EntitySaveData>();
}

[Serializable]
public sealed class EntitySaveData
{
    public string id;
    public string prefab;
    public bool spawned;
    public bool destroyed;
    public bool active = true;
    public bool hasTransform;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public List<ComponentSaveData> components = new List<ComponentSaveData>();
}

[Serializable]
public sealed class ComponentSaveData
{
    public string key;
    /// <summary>The component's own state, as it wrote it (usually JSON).</summary>
    public string state;
}

[Serializable]
public sealed class PlayerSaveData
{
    public List<SlotSaveData> bag = new List<SlotSaveData>();
    /// <summary>The safe pocket. Missing in saves made before it existed, so readers must allow null.</summary>
    public List<SlotSaveData> safePocket = new List<SlotSaveData>();
    public List<AmountSaveData> resources = new List<AmountSaveData>();
    public List<string> discoveredResources = new List<string>();
    public List<string> knownRecipes = new List<string>();
    /// <summary>Buildings learned from blueprints and residents. Missing in older saves.</summary>
    public List<string> knownBuildings = new List<string>();
    /// <summary>False in saves made before hunger and thirst existed; the player then starts with the default values.</summary>
    public bool hasNeeds;
    public float hunger;
    public float thirst;
    /// <summary>Bag slot of the equipped weapon in saves made before gear slots existed, or -1. No longer written.</summary>
    public int equippedSlot = -1;
    /// <summary>False in saves made before gear slots existed; loading those equips gear from the bag instead.</summary>
    public bool hasGear;
    /// <summary>Worn gear; slot is the GearSlot.</summary>
    public List<SlotSaveData> gear = new List<SlotSaveData>();
}

[Serializable]
public sealed class StashSaveData
{
    public List<SlotSaveData> items = new List<SlotSaveData>();
    public List<AmountSaveData> resources = new List<AmountSaveData>();
}

[Serializable]
public sealed class SlotSaveData
{
    public int slot;
    public string item;
    public int count;
    /// <summary>Durability lost, 0 (as new) to 1 (broken). Stored as wear so older saves load undamaged.</summary>
    public float wear;
    /// <summary>Levels of empowering. Missing in older saves, where nothing was.</summary>
    public int empower;
    /// <summary>The enchantment's id, or empty.</summary>
    public string enchantment;
}

[Serializable]
public sealed class AmountSaveData
{
    public string id;
    public int amount;
}
