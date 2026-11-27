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
    /// <summary>A horse or cart on its way home from a raid when this was written (the raid's end, before it was set down at home):
    /// it arrives once the save is loaded. Empty in other saves.</summary>
    public ArrivingSaveData arriving = new ArrivingSaveData();
}

/// <summary>What came out of a raid with the player and has not yet been set down at home: the horse (its prefab id, its state and
/// health) and the cart (its prefab id and state, cargo and all). Written at the raid's end, a quit or a crash on the results screen
/// lost them: they had left the Haven's world at the launch and lived only in memory.</summary>
[Serializable]
public sealed class ArrivingSaveData
{
    public string horse;
    public string horseState;
    public float horseHealth = 1f;
    public string cart;
    public string cartState;

    public bool IsEmpty => string.IsNullOrEmpty(horse) && string.IsNullOrEmpty(cart);
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
    // Who each standing ask from a mark or a trait was posted for: "request id|resident id" (HavenRequestDefinition › Who they are).
    public List<string> requestAskers = new List<string>();
    public int requestsAnswered;
    /// <summary>The homecoming the asks were last posted at. Missing in older saves: -1, posted at the next look.</summary>
    public int requestsAskedAt = -1;
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
    /// <summary>What a lost breach's band carried off (SettlementState.Plunder): its leader's monster id and the items. Missing: none.</summary>
    public string plunderLeader;
    public List<AmountSaveData> plunder = new List<AmountSaveData>();
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
    /// <summary>Building modules in the Haven's store, one id per spare (BuildingModules). Missing in older saves: none.</summary>
    public List<string> spareModules = new List<string>();
    public string lastBossBeaten;
    public int lastBossBeatenAt;
    /// <summary>The homecoming whose share of held quests has begun, and how many of them (QuestManager). Missing in older saves: a full share.</summary>
    public int heldQuestsHomecoming;
    public int heldQuestsStarted;
    /// <summary>Those lost on raids. Missing in older saves.</summary>
    public List<FallenSaveData> fallen = new List<FallenSaveData>();
    /// <summary>How residents stand with each other ("idA|idB|value", PeopleStandings). Missing in older saves: the written friends and frictions.</summary>
    public List<string> standings = new List<string>();
    /// <summary>Ids of the jobs they left. Missing in older saves.</summary>
    public List<string> vacantJobs = new List<string>();
    /// <summary>Rings drawn for the woodcutters to clear (RingOrder). Missing in older saves: none.</summary>
    public List<RingSaveData> rings = new List<RingSaveData>();
    /// <summary>Guard posts set in the Keeper's view (GuardPosts). Missing in older saves: none.</summary>
    public List<PostSaveData> posts = new List<PostSaveData>();
    /// <summary>The wishes already met are marked (HomeWishes). Missing in older saves: not yet, so they are marked quietly.</summary>
    public bool wishesSeeded;
    /// <summary>The held lands the parties go out to (LandParties). Missing in older saves: none.</summary>
    public List<PartySaveData> parties = new List<PartySaveData>();
    /// <summary>The outposts at the Secure lands (Outposts). Missing in older saves: none.</summary>
    public List<OutpostSaveData> outposts = new List<OutpostSaveData>();
    /// <summary>Every land has been Secure at once (the ring held). Missing in older saves: not yet.</summary>
    public bool ringHeld;
    /// <summary>The foray under way (Forays), or none (an empty destination, also in older saves).</summary>
    public ForaySaveData foray;
    public int purificationQueued;
    public float purificationProgress;
    /// <summary>Share faster the altar works for its upgrades. Missing in older saves; the altar sets it again at home.</summary>
    public float purificationBonus;
    /// <summary>Share more Purified from each batch. Missing in older saves; the altar sets it again at home.</summary>
    public float purificationYield;
    /// <summary>The part of a Purified a yield bonus has made but not yet given (it comes with a later batch). Missing in older saves: 0.</summary>
    public float purifiedRemainder;
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
    /// <summary>How many of the queued crafts a worker started (StationQueue.WorkerCrafts).</summary>
    public int workerCrafts;
    /// <summary>Finer pieces the worker made, waiting to be collected ("quality|maker"). Missing in older saves: none.</summary>
    public List<string> lifts = new List<string>();
    /// <summary>What waits in the output slots after the first (a Storage Loft's). Missing in older saves: none.</summary>
    public List<StationOutputSaveData> moreOutput = new List<StationOutputSaveData>();
    /// <summary>What its worker has fetched from the chests for their orders (StationQueue.Supplies). Missing in older saves: none.</summary>
    public List<StationOutputSaveData> supplies = new List<StationOutputSaveData>();
    /// <summary>A field's water, seconds of growing left (StationQueue.Water, Fields). Missing in older saves (-1): a full watering.</summary>
    public float water = -1f;
}

[System.Serializable]
public sealed class StationOutputSaveData
{
    public string id;
    public bool isResource;
    public int amount;
}

[Serializable]
public sealed class StationJobSaveData
{
    /// <summary>The recipe's id.</summary>
    public string recipe;
    public int count;
}

/// <summary>A person's talents (TalentSheet): experience and passions by skill id ("id|amount"), and passion picks owed.</summary>
[Serializable]
public sealed class TalentSaveData
{
    public List<string> experience = new List<string>();
    public List<string> passions = new List<string>();
    public int picksOwed;
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
    /// <summary>The house kept for them (a building id). Missing in older saves: none, so their bed went to whoever needed it.</summary>
    public string home;
    /// <summary>Their talents, kept while lost. Missing in older saves: their written start.</summary>
    public TalentSaveData talents = new TalentSaveData();
    /// <summary>The kit, marks and deeds waiting with them. Missing in older saves: none.</summary>
    public SlotSaveData kitWeapon;
    public SlotSaveData kitBody;
    public List<string> marks = new List<string>();
    public List<string> deeds = new List<string>();
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
    /// <summary>Weight carried for their work (Strength). Missing in older saves: none yet.</summary>
    public float haulDone;
    /// <summary>The id of the worker gear given to them, or empty. Missing in older saves: none.</summary>
    public string workGear;
    /// <summary>Their wish for a home was met and thanked for (HomeWishes). Missing in older saves: not yet.</summary>
    public bool wishMet;
    /// <summary>ResidentMeal: what they ate this morning (0 fed, also an older save).</summary>
    public int meal;
    /// <summary>Home bruised from a foray gone wrong, until the next dawn.</summary>
    public bool bruised;
    /// <summary>Their talents (ResidentTalents). Missing in older saves: their written start, with their old rank's level in their job.</summary>
    public TalentSaveData talents = new TalentSaveData();
    /// <summary>Marks raids left (ids), deeds toward them ("key|count") and what they feel for a while ("id|last day"). Missing in older saves: none.</summary>
    public List<string> marks = new List<string>();
    public List<string> deeds = new List<string>();
    public List<string> feelings = new List<string>();
    /// <summary>Inspiration (0 none) and the day it came.</summary>
    public int inspiration;
    public int inspiredOn = -1;
    /// <summary>Their bond with the player (PeopleBond). Missing in older saves (-1): their raids along.</summary>
    public float bond = -1f;
    /// <summary>The kit the player gave them (CompanionKit). Missing in older saves: none.</summary>
    public SlotSaveData kitWeapon;
    public SlotSaveData kitBody;
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
    /// <summary>What the player is gathering for (WantedAtHome keys). Missing in older saves.</summary>
    public List<string> tracked = new List<string>();
    /// <summary>Abilities' altar choices: "skill id#rank" in id, the option's id in name.</summary>
    public List<SkillChoiceSaveData> skillChoices = new List<SkillChoiceSaveData>();
    /// <summary>How often each ability has been cast (mastery).</summary>
    public List<AmountSaveData> skillUses = new List<AmountSaveData>();
    /// <summary>What the last raid set out with (PackForRaid): id and how many. Missing in older saves.</summary>
    public List<AmountSaveData> setOut = new List<AmountSaveData>();
    /// <summary>Raids fallen in (Damage Taken › Grows With Falls). Missing in older saves.</summary>
    public int falls;
    /// <summary>The death rules (DeathRules as a number). Missing in older saves: Normal, the default.</summary>
    public int deathRules = (int)DeathRules.Normal;
    /// <summary>Seconds left of the grace after a fall, in which another costs no lessons. Missing in older saves: none.</summary>
    public float fallGrace;
    /// <summary>Creatures met, by id, with how many were slain: the journal's record. Missing in older saves.</summary>
    public List<AmountSaveData> creatures = new List<AmountSaveData>();
    /// <summary>The rest of the journal's record. Missing in older saves.</summary>
    public JournalSaveData journal = new JournalSaveData();
    /// <summary>Boss encounters whose gate was reached, by raid id (later tries start at the rest stone). Missing in older saves.</summary>
    public List<string> encounterGates = new List<string>();
    /// <summary>When each boss encounter's boss was last brought down, by raid id (rematches wait). Missing in older saves.</summary>
    public List<StampSaveData> encounterKills = new List<StampSaveData>();
    /// <summary>The character's Lost (CharacterProgress.LostOnes). Missing in older saves: none has risen yet.</summary>
    public LostRosterSaveData lostOnes = new LostRosterSaveData();
}

/// <summary>A character's Lost as saved (LostOneRoster).</summary>
[Serializable]
public sealed class LostRosterSaveData
{
    public int seed;
    public int homecomings;
    public int risen;
    public List<LostOneSaveData> records = new List<LostOneSaveData>();
}

/// <summary>One of the Lost as saved (LostOneRecord): traits by affix asset name, what it carries slot by slot.</summary>
[Serializable]
public sealed class LostOneSaveData
{
    public string id;
    public string givenName;
    public string epithet;
    public string pastLife;
    public int rank = 1;
    public List<string> traits = new List<string>();
    public List<string> learned = new List<string>();
    public List<SlotSaveData> carried = new List<SlotSaveData>();
    public int kills;
    public int escapes;
    public int raidsMet;
    /// <summary>LostOneStatus: 0 walking, 1 laid to rest.</summary>
    public int status;
    public bool met;
    public int restedAtHomecoming;
    public string lastSeenIn;
    public string epitaph;
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
    /// <summary>Every raid run, past the MostRaids remembered (JournalRecord.RaidsEver). 0 in older saves.</summary>
    public int raidsEver;
    /// <summary>Skills tried at least once, which open their mechanics pages. Missing in older saves.</summary>
    public List<string> tried = new List<string>();
    /// <summary>Named places found on the map. Missing in older saves.</summary>
    public List<string> discovered = new List<string>();
    /// <summary>The world's curious places already thought about (CuriousPlace). Missing in older saves.</summary>
    public List<string> noticed = new List<string>();
    /// <summary>Named places seen from a lookout (on the map in grey until walked to). Missing in older saves.</summary>
    public List<string> seen = new List<string>();
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
    /// <summary>Completed after a raid was decided, its rewards not yet given (they are given at home). Missing in older saves: none.</summary>
    public bool rewardsWaiting;
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
    /// <summary>What is marked to keep in the bag (the Store key leaves it), by item id. Missing in older saves.</summary>
    public List<string> keptItems;
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

    /// <summary>
    /// The same piece in another slot and count, every other field kept (a field added later too). Copied by hand, a co-op stash change
    /// left out the fish's size (SharedStateTests: a slot is copied with this or made by SaveMapper.SlotOf, never field by field).
    /// </summary>
    public SlotSaveData With(int slot, int count)
    {
        var copy = (SlotSaveData)MemberwiseClone();
        copy.slot = slot;
        copy.count = count;
        return copy;
    }
}

[Serializable]
public sealed class AmountSaveData
{
    public string id;
    public int amount;
}

/// <summary>A moment of real time for an id, as UTC ticks (DateTime.Ticks).</summary>
[Serializable]
public sealed class StampSaveData
{
    public string id;
    public long utcTicks;
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

/// <summary>A ring drawn for the woodcutters (RingOrder).</summary>
[Serializable]
public sealed class PostSaveData
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public sealed class ForaySaveData
{
    public string destination = "";
    /// <summary>Resident ids.</summary>
    public List<string> members = new List<string>();
    public float secondsLeft;
    public float seconds;
    public float chance;
    public float haulScale = 1f;
}

[Serializable]
public sealed class OutpostSaveData
{
    public string region;
    /// <summary>The keeper's resident id.</summary>
    public string keeper;
}

[Serializable]
public sealed class PartySaveData
{
    /// <summary>LandPartyKind: 0 hunting, 1 fishing, 2 scouting.</summary>
    public int kind;
    public string region;
}

[System.Serializable]
public sealed class RingSaveData
{
    public float x;
    public float y;
    public float z;
    public float radius;
    public int pendingFells;
    public int standingSeen = -1;
    /// <summary>What the ring is for (RingKind: 0 felling, 1 quarry); saves from before quarry rings read 0.</summary>
    public int kind;
}

/// <summary>One altar choice for an ability: where ("skill id#rank") and which option.</summary>
[System.Serializable]
public sealed class SkillChoiceSaveData
{
    public string at;
    public string option;
}
