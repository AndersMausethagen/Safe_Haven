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
    /// <summary>The raid this save was made on the way into, by name. Loaded with this still set, that raid never came home. Empty in other saves.</summary>
    public string raidUnderWay;
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
public sealed class TradeSaveData
{
    public string offer;
    public int count;
}

[Serializable]
public sealed class SettlementSaveData
{
    public bool portalRepaired;
    public int wardLevel;
    /// <summary>Highest Haven rank reached. 0 in older saves.</summary>
    public int havenRank;
    public int successfulExtractions;
    /// <summary>Days that have passed at home (the seasons follow them). 0 in older saves.</summary>
    public int daysPassed;
    /// <summary>How far the blight has crept into the woods round home (0-1). 0 in older saves.</summary>
    public float blight;
    /// <summary>The Haven's hold on the regions round it (Territory). Missing in older saves.</summary>
    public TerritorySaveData territory = new TerritorySaveData();
    /// <summary>Lasting gifts of the spirits (SpiritBonds). Empty in older saves.</summary>
    public List<string> spiritGifts = new List<string>();
    public int huldraRespect;
    public int huldraAwayUntil;
    /// <summary>What the Haven is asking for (HavenRequestDefinition ids), and how many asks were answered. Empty in older saves.</summary>
    public List<string> requests = new List<string>();
    public int requestsAnswered;
    /// <summary>Renown kept for crafting orders filled. 0 in older saves.</summary>
    public int orderRenown;
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
    /// <summary>What has been put in each house (HomeLifeSettings › Comforts): "house id|comfort id". Missing in older saves.</summary>
    public List<string> houseComforts = new List<string>();
    /// <summary>The worn footpaths round home (Footpaths), packed. Missing in older saves.</summary>
    public string footpaths = "";
    /// <summary>Raid seconds the Carpenter and Mason have yet to spend on scaffolds at home (ResidentBuilders).</summary>
    public float builderSeconds;
    /// <summary>Ids of the bosses beaten. Missing in older saves.</summary>
    public List<string> defeatedBosses = new List<string>();
    public string lastBossBeaten;
    public int lastBossBeatenAt;
    /// <summary>The homecoming whose share of held quests has begun, and how many of them (QuestManager). Missing in older saves: a full share.</summary>
    public int heldQuestsHomecoming;
    public int heldQuestsStarted;
    /// <summary>Those lost on raids. Missing in older saves.</summary>
    public List<FallenSaveData> fallen = new List<FallenSaveData>();
    /// <summary>Ids of the jobs they left. Missing in older saves.</summary>
    public List<string> vacantJobs = new List<string>();
    public int purificationQueued;
    public float purificationProgress;
    /// <summary>Share faster the altar works for its upgrades. Missing in older saves; the altar sets it again at home.</summary>
    public float purificationBonus;
    /// <summary>Share more Purified from each batch. Missing in older saves; the altar sets it again at home.</summary>
    public float purificationYield;
    /// <summary>How far the portal has been improved. Missing in older saves.</summary>
    public int portalLevel;
    /// <summary>What was bought at the Trading Post since the last raid survived. Missing in older saves.</summary>
    public List<TradeSaveData> tradeBought = new List<TradeSaveData>();
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
public sealed class FallenSaveData
{
    public string id;
    public string where;
    public int atExtraction;
    public int raidsAlong;
    /// <summary>What they will have to get back, as CompanionMemory. Missing in older saves: nothing to come back.</summary>
    public List<int> forgotten = new List<int>();
}

[Serializable]
public sealed class ResidentSaveData
{
    public string id;
    /// <summary>The job's id, or empty while unassigned.</summary>
    public string job;
    /// <summary>The player has paused their work orders.</summary>
    public bool workPaused;
    /// <summary>Going along on raids. Missing in older saves, which start them at none.</summary>
    public int raidsAlong;
    public int killsAlong;
    public int timesHelpedUp;
    public int memory;
    /// <summary>Having been lost and found: what they remember and what they have yet to, as CompanionMemory.</summary>
    public List<int> remembers = new List<int>();
    public List<int> forgotten = new List<int>();
    public bool stranger;
    public int comingBack;
    public bool allCameBack;
    /// <summary>Crafts made on their own, for their rank in the job. Missing in older saves: a novice.</summary>
    public int workDone;
    /// <summary>A lesson given since the last raid survived.</summary>
    public bool taughtSinceRaid;
    /// <summary>The house they sleep in (a building's save id), or empty for the home spots by the portal. Missing in older saves.</summary>
    public string home;
    /// <summary>The house the player chose for them, or empty to let them choose (HavenHomes).</summary>
    public string homeChosen;
    /// <summary>Which of the buildings of their job's kind they work at (a building's save id), and its station's queue.</summary>
    public string workplace;
    public string workQueue;
}

[Serializable]
public sealed class ProgressSaveData
{
    /// <summary>The character level from before skills, read once to turn its experience into Insight; 1 in newer saves.</summary>
    public int level = 1;
    /// <summary>Experience towards that level; 0 in newer saves.</summary>
    public int experience;
    /// <summary>Talent ranks from before skills: no longer read (talents became perks).</summary>
    public List<AmountSaveData> talents = new List<AmountSaveData>();
    /// <summary>Total experience in each skill (ProficiencyDefinition), by id. Missing in older saves.</summary>
    public List<AmountSaveData> proficiencies = new List<AmountSaveData>();
    /// <summary>The perk chosen at each reached skill milestone, by perk id. Missing in older saves.</summary>
    public List<string> perks = new List<string>();
    /// <summary>Experience not yet put into a skill. Missing in older saves.</summary>
    public int insight;
    /// <summary>Plain pieces made in a row, by kind, for the Sure Hand technique. Missing in older saves.</summary>
    public List<AmountSaveData> plainRuns = new List<AmountSaveData>();
    /// <summary>Techniques already announced. Missing in older saves.</summary>
    public List<string> techniquesSeen = new List<string>();
    /// <summary>Raids entered at least once, by raid id. Missing in older saves: their next raid plays its first-visit cinematic.</summary>
    public List<string> raidsVisited = new List<string>();
    /// <summary>Art runes attuned, "Kind=itemId" (CharacterProgress.AttunedRunes).</summary>
    public List<string> attunedRunes = new List<string>();
    /// <summary>Enchantments learned from inscriptions, by id (CharacterProgress.LearnedEnchantments).</summary>
    public List<string> enchantments = new List<string>();
    /// <summary>False in saves from before inscriptions: they know every enchantment marked Known In Old Saves.</summary>
    public bool enchantmentsLearned;
    /// <summary>Ranks deepened at the altar, by skill id. Missing in older saves.</summary>
    public List<AmountSaveData> skillRanks = new List<AmountSaveData>();
    public List<string> crafted = new List<string>();
    public List<string> stones = new List<string>();
    /// <summary>Creatures met, by id, with how many were slain: the journal's record. Missing in older saves.</summary>
    public List<AmountSaveData> creatures = new List<AmountSaveData>();
    /// <summary>The rest of the journal's record. Missing in older saves.</summary>
    public JournalSaveData journal = new JournalSaveData();
}

[Serializable]
public sealed class JournalSaveData
{
    public List<string> read = new List<string>();
    public List<string> announced = new List<string>();
    public List<string> found = new List<string>();
    public List<string> killedBy = new List<string>();
    public string pinned = "";
    public List<RaidRecordSaveData> raids = new List<RaidRecordSaveData>();
    /// <summary>Skills tried at least once, which open their mechanics pages. Missing in older saves.</summary>
    public List<string> tried = new List<string>();
    /// <summary>Named places found on the map. Missing in older saves.</summary>
    public List<string> discovered = new List<string>();
    /// <summary>The world's curious places already thought about (CuriousPlace). Missing in older saves.</summary>
    public List<string> noticed = new List<string>();
    /// <summary>The biggest of each kind of fish caught, in cm (id, amount as cm x 10). Missing in older saves.</summary>
    public List<AmountSaveData> biggestFish = new List<AmountSaveData>();
}

[Serializable]
public sealed class RaidRecordSaveData
{
    public string raid;
    public string outcome;
    public int seconds;
    public int value;
    public string broughtDownBy;
    public List<string> lines = new List<string>();
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
    /// <summary>The quickbar's items (hold F), by item id, in order. Missing in older saves.</summary>
    public List<string> quickItems;
    /// <summary>The items on the skill bar, by slot: an item id, or "" where a skill or nothing is. Missing in older saves.</summary>
    public List<string> skillBarItems;
    /// <summary>The skills the player knows (a boss's reward among them), by id. Missing in older saves, which keep the starting ones.</summary>
    public List<string> knownSkills;
    /// <summary>The skill bar, by slot: a skill id, or "" for an empty slot. Missing in older saves.</summary>
    public List<string> skillBar;
    /// <summary>Crafts by hand under way when saved, already paid (HandCraftQueue). Null when there were none, and in older saves.</summary>
    public HandCraftSaveData handCrafts;
}

/// <summary>The hand-craft queue as saved: each craft in order (the first under way), and how far the first has come.</summary>
[Serializable]
public sealed class HandCraftSaveData
{
    public List<HandCraftEntrySaveData> crafts = new List<HandCraftEntrySaveData>();
    /// <summary>Seconds the first craft has been worked on.</summary>
    public float elapsed;
}

/// <summary>One paid craft, with what was paid from where, so it can be given back there (MaterialPool.Payment).</summary>
[Serializable]
public sealed class HandCraftEntrySaveData
{
    public string recipe;
    public List<AmountSaveData> carried = new List<AmountSaveData>();
    public List<AmountSaveData> stored = new List<AmountSaveData>();
    public List<AmountSaveData> fromBag = new List<AmountSaveData>();
    public List<AmountSaveData> fromStore = new List<AmountSaveData>();
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
    /// <summary>How well it was made (ItemQuality). Missing in older saves, where everything was plain.</summary>
    public int quality;
    /// <summary>Who made a Fine or Masterwork piece. Missing in older saves.</summary>
    public string maker;
    /// <summary>A caught fish's length in cm (the biggest of the stack). Missing in older saves, and 0 for everything else.</summary>
    public float size;
}

[Serializable]
public sealed class AmountSaveData
{
    public string id;
    public int amount;
}

[Serializable]
public sealed class TerritorySaveData
{
    public int turn;
    public int restUntil;
    public int firstClaimTurn = -1;
    public bool freeClaimUsed;
    public List<RegionHoldSaveData> holds = new List<RegionHoldSaveData>();
    public bool hasPush;
    public string pushRegion = "";
    public int pushDue;
    public float pushStrength;
    public float pushWeakened;
}

[Serializable]
public sealed class RegionHoldSaveData
{
    public string region;
    public bool claimed;
    public bool everClaimed;
    public float hold;
    public int taskProgress;
    public int taskFrom;
    public int tasksDone;
}
