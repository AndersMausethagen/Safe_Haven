using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>What a save file turns back into in memory: the runtime objects GameSession hands to the next scene.</summary>
public sealed class LoadedGame
{
    public PlayerSnapshot Player { get; set; }
    public StashStorage Stash { get; set; }
    public WorldState World { get; set; } = new WorldState();
    public QuestLog Quests { get; set; } = new QuestLog();
    public CharacterProgress Progress { get; set; } = new CharacterProgress();
    public SettlementState Settlement { get; set; } = new SettlementState();
    /// <summary>The playtest run the save belongs to; null for older saves, which then start a new run.</summary>
    public PlaytestRun Playtest { get; set; }
    /// <summary>How the player looks and their name.</summary>
    public CharacterAppearance Appearance { get; set; }
    public string Scene { get; set; }
    public PlayerPose? Pose { get; set; }
    /// <summary>Ids in the file that no longer match any content; their entries were skipped.</summary>
    public List<string> MissingContent { get; } = new List<string>();
}

/// <summary>
/// Translates between the game's in-memory state (PlayerSnapshot, StashStorage, references to definitions) and the save
/// file format (SaveData: ids and numbers only). It is the only place that knows both sides, so the runtime systems never
/// see the disk format and the disk format never holds runtime objects. Pure and free of file access, so it is testable.
/// </summary>
public static class SaveMapper
{
    /// <summary>The format version written by this build. Raise it when existing fields change meaning.</summary>
    public const int CurrentVersion = 1;

    public static SaveData ToSaveData(PlayerSnapshot player, StashStorage stash, string scene, PlayerPose? pose, DateTime savedAtUtc,
        WorldState world = null, QuestLog quests = null, SettlementState settlement = null, PlaytestRun playtest = null,
        CharacterProgress progress = null)
    {
        var data = new SaveData
        {
            version = CurrentVersion,
            savedAtUtc = savedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            scene = scene,
            hasPlayerPose = pose.HasValue,
        };

        if (pose.HasValue)
        {
            data.playerPosition = pose.Value.Position;
            data.playerYaw = pose.Value.Yaw;
        }

        if (player != null)
        {
            foreach (PlayerSnapshot.SlotRecord slot in player.Slots)
            {
                data.player.bag.Add(ToSlot(slot.Index, slot.Stack));
            }

            foreach (PlayerSnapshot.SlotRecord slot in player.Pocket)
            {
                data.player.safePocket.Add(ToSlot(slot.Index, slot.Stack));
            }

            data.player.hasGear = player.HasGear;
            foreach (PlayerSnapshot.SlotRecord slot in player.Gear)
            {
                data.player.gear.Add(ToSlot(slot.Index, slot.Stack));
            }

            WriteAmounts(player.Resources, data.player.resources);
            foreach (ResourceDefinition resource in player.Discovered)
            {
                data.player.discoveredResources.Add(resource.Id);
            }

            foreach (RecipeDefinition recipe in player.KnownRecipes)
            {
                data.player.knownRecipes.Add(recipe.Id);
            }

            foreach (BuildingDefinition building in player.KnownBuildings)
            {
                data.player.knownBuildings.Add(building.Id);
            }

            if (player.QuickBar != null)
            {
                data.player.quickItems = new List<string>();
                foreach (ItemDefinition item in player.QuickBar)
                {
                    if (item != null)
                    {
                        data.player.quickItems.Add(item.Id);
                    }
                }
            }

            if (player.SkillBarItems != null)
            {
                data.player.skillBarItems = new List<string>();
                foreach (ItemDefinition item in player.SkillBarItems)
                {
                    data.player.skillBarItems.Add(item != null ? item.Id : "");
                }
            }

            // Skills learned in play (a boss's reward) are lost with the session without this.
            if (player.SkillBar != null)
            {
                data.player.knownSkills = new List<string>();
                foreach (SkillDefinition skill in player.KnownSkills)
                {
                    if (skill != null)
                    {
                        data.player.knownSkills.Add(skill.Id);
                    }
                }

                data.player.skillBar = new List<string>();
                foreach (SkillDefinition skill in player.SkillBar)
                {
                    data.player.skillBar.Add(skill != null ? skill.Id : "");
                }
            }

            PlayerSnapshot.Condition state = player.State;
            data.player.hasNeeds = state.HasNeeds;
            data.player.hunger = state.Hunger;
            data.player.thirst = state.Thirst;
            data.player.equippedSlot = state.EquippedSlot;
        }

        if (stash != null)
        {
            for (int i = 0; i < stash.Items.SlotCount; i++)
            {
                ItemStack stack = stash.Items.GetSlot(i);
                if (!stack.IsEmpty)
                {
                    data.stash.items.Add(ToSlot(i, stack));
                }
            }

            WriteAmounts(stash.Resources.StoredAmounts, data.stash.resources);
        }

        WriteWorld(world, data.world);
        WriteQuests(quests, data.quests);
        if (quests != null)
        {
            data.happenedEvents.AddRange(quests.HappenedEventIds);
        }
        WriteSettlement(settlement, data.settlement);
        WriteProgress(progress, data.progress);
        if (playtest != null)
        {
            data.playtest = new PlaytestSaveData { runId = playtest.Id, playedSeconds = playtest.PlayedSeconds, sessions = playtest.Sessions };
        }

        return data;
    }

    /// <summary>Every occupied slot of a container, for what saves its own contents (a cart's cargo).</summary>
    public static List<SlotSaveData> ToSlots(ItemContainer container)
    {
        var slots = new List<SlotSaveData>();
        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty)
            {
                slots.Add(ToSlot(i, stack));
            }
        }

        return slots;
    }

    private static SlotSaveData ToSlot(int index, ItemStack stack)
    {
        return new SlotSaveData
        {
            slot = index,
            item = stack.Item.Id,
            count = stack.Count,
            wear = 1f - stack.Durability,
            empower = stack.Empower,
            enchantment = stack.Enchantment != null ? stack.Enchantment.Id : "",
            quality = (int)stack.Quality,
            maker = stack.Maker,
            size = stack.Size,
        };
    }

    /// <summary>The settlement as save data (co-op sends it to visiting friends).</summary>
    public static SettlementSaveData ToData(SettlementState settlement)
    {
        var data = new SettlementSaveData();
        WriteSettlement(settlement, data);
        return data;
    }

    /// <summary>A settlement read from save data, on its own (co-op: a friend's settlement to take on).</summary>
    public static SettlementState SettlementFrom(SettlementSaveData data, IContentLookup content) => ReadSettlement(data, content, new List<string>());

    private static void WriteSettlement(SettlementState settlement, SettlementSaveData into)
    {
        if (settlement == null)
        {
            return;
        }

        into.portalRepaired = settlement.PortalRepaired;
        into.wardLevel = settlement.WardLevel;
        into.havenRank = settlement.HavenRank;
        into.successfulExtractions = settlement.SuccessfulExtractions;
        into.daysPassed = settlement.DaysPassed;
        into.blight = settlement.Blight;
        into.territory = WriteTerritory(settlement.Territory);
        into.spiritGifts = new List<string>(settlement.SpiritGifts);
        into.huldraRespect = settlement.HuldraRespect;
        into.huldraAwayUntil = settlement.HuldraAwayUntil;
        into.requests = new List<string>(settlement.Requests);
        into.requestsAnswered = settlement.RequestsAnswered;
        into.requestsAskedAt = settlement.RequestsAskedAt;
        into.orderRenown = settlement.OrderRenown;
        into.breach = (int)settlement.Breach;
        into.breachesWon = settlement.BreachesWon;
        into.nextBreachAt = settlement.NextBreachAt;
        into.breachPendingSince = settlement.BreachPendingSince;
        into.wardStrain = settlement.WardStrain;
        into.breachWeakening = settlement.BreachWeakening;
        into.purificationQueued = settlement.PurificationQueued;
        into.purificationProgress = settlement.PurificationProgress;
        into.purificationBonus = settlement.PurificationBonus;
        into.purificationYield = settlement.PurificationYield;
        into.portalLevel = settlement.PortalLevel;
        foreach (KeyValuePair<string, int> bought in settlement.TradeBought)
        {
            into.tradeBought.Add(new TradeSaveData { offer = bought.Key, count = bought.Value });
        }

        foreach (ResidentRecord record in settlement.Residents)
        {
            into.residents.Add(new ResidentSaveData { id = record.Resident.Id, workPaused = record.WorkPaused, job = record.Job != null ? record.Job.Id : "", raidsAlong = record.RaidsAlong, killsAlong = record.KillsAlong, timesHelpedUp = record.TimesHelpedUp, memory = (int)record.Memory, remembers = ToSaved(record.Remembers), forgotten = ToSaved(record.Forgotten), stranger = record.IsStranger, comingBack = (int)record.ComingBack, allCameBack = record.AllCameBack, workDone = record.WorkDone, taughtSinceRaid = record.TaughtSinceRaid, home = record.Home ?? "", homeChosen = record.HomeChosen ?? "", workplace = record.Workplace ?? "", workQueue = record.WorkQueue ?? "" });
        }

        foreach (KeyValuePair<string, List<string>> house in settlement.HouseComforts)
        {
            foreach (string comfort in house.Value)
            {
                into.houseComforts.Add(house.Key + "|" + comfort);
            }
        }

        into.footpaths = settlement.Footpaths ?? "";
        into.builderSeconds = settlement.BuilderSeconds;
        into.defeatedBosses.AddRange(settlement.DefeatedBosses);
        into.lastBossBeaten = settlement.LastBossBeaten;
        into.lastBossBeatenAt = settlement.LastBossBeatenAt;
        into.heldQuestsHomecoming = settlement.HeldQuestsHomecoming;
        into.heldQuestsStarted = settlement.HeldQuestsStarted;
        foreach (FallenRecord record in settlement.Fallen)
        {
            into.fallen.Add(new FallenSaveData { id = record.Resident.Id, where = record.Where, atExtraction = record.AtExtraction, raidsAlong = record.RaidsAlong, forgotten = ToSaved(record.Forgotten) });
        }

        foreach (JobDefinition job in settlement.VacantJobs)
        {
            into.vacantJobs.Add(job.Id);
        }

        foreach (StationQueue queue in settlement.Workshops.Queues)
        {
            if (queue.IsIdle && queue.Output == null)
            {
                continue;
            }

            var station = new StationSaveData
            {
                id = queue.StationId,
                type = (int)queue.Type,
                capacity = queue.Capacity,
                maxQueued = queue.MaxQueued,
                progress = queue.Progress,
                output = queue.Output is IStackDefinition made ? made.Id : "",
                outputIsResource = queue.Output is ResourceDefinition,
                outputAmount = queue.OutputAmount,
            };
            foreach (StationQueue.Job job in queue.Jobs)
            {
                station.jobs.Add(new StationJobSaveData { recipe = job.Recipe.Id, count = job.Count });
            }

            into.stations.Add(station);
        }
    }

    private static SettlementState ReadSettlement(SettlementSaveData saved, IContentLookup content, List<string> missing)
    {
        var settlement = new SettlementState();
        if (saved == null)
        {
            return settlement;
        }

        settlement.Restore(saved.portalRepaired, saved.wardLevel, saved.successfulExtractions, (BreachStatus)saved.breach,
            saved.purificationQueued, saved.purificationProgress, saved.purificationBonus);
        settlement.SetPurificationYield(saved.purificationYield);
        settlement.RestorePortal(saved.portalLevel);
        settlement.RestoreDays(saved.daysPassed);
        settlement.RestoreBlight(saved.blight);
        ReadTerritory(saved.territory, settlement.Territory);
        settlement.RestoreSpirits(saved.spiritGifts, saved.huldraRespect, saved.huldraAwayUntil);
        settlement.RestoreRequests(saved.requests, saved.requestsAnswered, saved.orderRenown, saved.requestsAskedAt);
        if (saved.tradeBought != null)
        {
            foreach (TradeSaveData bought in saved.tradeBought)
            {
                settlement.RestoreBought(bought.offer, bought.count);
            }
        }

        // Older saves never planned a second breach: it comes the usual distance from where the game stands.
        int nextBreachAt = saved.nextBreachAt > 0 || (BreachStatus)saved.breach != BreachStatus.Won
            ? saved.nextBreachAt : saved.successfulExtractions + WardBreachSettings.Current.RaidsBetween;
        settlement.RestoreBreaches(saved.breachesWon, nextBreachAt, saved.breachPendingSince, saved.wardStrain, saved.breachWeakening);
        settlement.ReachHavenRank(saved.havenRank);
        foreach (ResidentSaveData entry in saved.residents)
        {
            ResidentDefinition resident = Resolve(content.FindResident(entry?.id), "resident", entry?.id, missing);
            if (resident != null)
            {
                JobDefinition job = string.IsNullOrEmpty(entry.job) ? null : Resolve(content.FindJob(entry.job), "job", entry.job, missing);
                settlement.AddResident(resident, job);
                ResidentRecord restored = settlement.Find(resident);
                restored.WorkPaused = entry.workPaused;
                restored.RaidsAlong = entry.raidsAlong;
                restored.KillsAlong = entry.killsAlong;
                restored.TimesHelpedUp = entry.timesHelpedUp;
                restored.Memory = (CompanionMemory)entry.memory;
                restored.Remembers.AddRange(FromSaved(entry.remembers));
                restored.Forgotten.AddRange(FromSaved(entry.forgotten));
                restored.IsStranger = entry.stranger;
                restored.ComingBack = (CompanionMemory)entry.comingBack;
                restored.AllCameBack = entry.allCameBack;
                restored.WorkDone = entry.workDone;
                restored.TaughtSinceRaid = entry.taughtSinceRaid;
                restored.Home = entry.home ?? "";
                restored.HomeChosen = entry.homeChosen ?? "";
                restored.Workplace = entry.workplace ?? "";
                restored.WorkQueue = entry.workQueue ?? "";
            }
        }

        foreach (string entry in saved.houseComforts ?? new List<string>())
        {
            int bar = entry.IndexOf('|');
            if (bar > 0)
            {
                settlement.RestoreComfort(entry.Substring(0, bar), entry.Substring(bar + 1));
            }
        }

        settlement.Footpaths = saved.footpaths ?? "";
        settlement.RestoreBuilderSeconds(saved.builderSeconds);
        foreach (string boss in saved.defeatedBosses ?? new List<string>())
        {
            settlement.MarkBossDefeated(boss);
        }

        settlement.RestoreLastBoss(saved.lastBossBeaten, saved.lastBossBeatenAt);
        settlement.RestoreHeldQuests(saved.heldQuestsHomecoming, saved.heldQuestsStarted);
        foreach (FallenSaveData entry in saved.fallen ?? new List<FallenSaveData>())
        {
            settlement.RestoreFallen(Resolve(content.FindResident(entry?.id), "resident", entry?.id, missing), entry?.where, entry != null ? entry.atExtraction : 0, entry != null ? entry.raidsAlong : 0, FromSaved(entry?.forgotten));
        }

        foreach (string job in saved.vacantJobs ?? new List<string>())
        {
            settlement.RestoreVacantJob(Resolve(content.FindJob(job), "job", job, missing));
        }

        ReadStations(saved.stations, settlement.Workshops, content, missing);
        return settlement;
    }

    private static void ReadStations(List<StationSaveData> saved, WorkshopState into, IContentLookup content, List<string> missing)
    {
        if (saved == null)
        {
            return;
        }

        foreach (StationSaveData entry in saved)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
            {
                continue;
            }

            var jobs = new List<StationQueue.Job>();
            foreach (StationJobSaveData job in entry.jobs ?? new List<StationJobSaveData>())
            {
                RecipeDefinition recipe = Resolve(content.FindRecipe(job?.recipe), "recipe", job?.recipe, missing);
                if (recipe != null)
                {
                    jobs.Add(new StationQueue.Job(recipe, job.count));
                }
            }

            UnityEngine.Object output = null;
            if (!string.IsNullOrEmpty(entry.output))
            {
                // Older saves stored material outputs as resources; their ids now name items.
                UnityEngine.Object resource = entry.outputIsResource ? content.FindResource(entry.output) : null;
                output = resource != null
                    ? resource
                    : Resolve(content.FindItem(entry.output), entry.outputIsResource ? "resource" : "item", entry.output, missing);
            }

            var queue = new StationQueue(entry.id, (CraftingStationType)entry.type, entry.capacity, entry.maxQueued);
            queue.Restore(jobs, entry.progress, output, entry.outputAmount);
            into.Add(queue);
        }
    }

    private static void WriteQuests(QuestLog quests, List<QuestSaveData> into)
    {
        if (quests == null)
        {
            return;
        }

        foreach (QuestLog.Entry entry in quests.Entries)
        {
            into.Add(new QuestSaveData { id = entry.Quest.Id, status = (int)entry.Status, counts = new List<int>(entry.Counts), rewardsWaiting = entry.RewardsWaiting });
        }
    }

    private static QuestLog ReadQuests(List<QuestSaveData> saved, IContentLookup content, List<string> missing)
    {
        var quests = new QuestLog();
        if (saved == null)
        {
            return quests;
        }

        foreach (QuestSaveData entry in saved)
        {
            QuestDefinition quest = Resolve(content.FindQuest(entry?.id), "quest", entry?.id, missing);
            if (quest != null)
            {
                quests.Restore(quest, (QuestStatus)entry.status, entry.counts, entry.rewardsWaiting);
            }
        }

        return quests;
    }

    private static void WriteWorld(WorldState world, List<SceneSaveData> into)
    {
        if (world == null)
        {
            return;
        }

        foreach (SceneState scene in world.Scenes.Values)
        {
            if (!scene.WriteToDisk)
            {
                continue;
            }

            var sceneData = new SceneSaveData { scene = scene.Scene };
            foreach (EntityState entity in scene.Entities.Values)
            {
                sceneData.entities.Add(ToData(entity));
            }

            into.Add(sceneData);
        }
    }

    private static WorldState ReadWorld(List<SceneSaveData> scenes)
    {
        var world = new WorldState();
        if (scenes == null)
        {
            return world;
        }

        foreach (SceneSaveData sceneData in scenes)
        {
            if (string.IsNullOrEmpty(sceneData?.scene))
            {
                continue;
            }

            SceneState scene = world.GetOrAdd(sceneData.scene, true);
            foreach (EntitySaveData entityData in sceneData.entities)
            {
                if (string.IsNullOrEmpty(entityData?.id))
                {
                    continue;
                }

                ReadInto(entityData, scene.GetOrAdd(entityData.id));
            }
        }

        return world;
    }

    /// <summary>One remembered object as save data; <paramref name="include"/> picks its components (all when null).</summary>
    public static EntitySaveData ToData(EntityState entity, Func<string, bool> include = null)
    {
        var data = new EntitySaveData
        {
            id = entity.Id, prefab = entity.PrefabId, spawned = entity.Spawned, destroyed = entity.Destroyed, active = entity.Active,
            hasTransform = entity.HasTransform, position = entity.Position, rotation = entity.Rotation,
        };
        foreach (KeyValuePair<string, string> component in entity.Components)
        {
            if (include == null || include(component.Key))
            {
                data.components.Add(new ComponentSaveData { key = component.Key, state = component.Value });
            }
        }

        return data;
    }

    /// <summary>Copies saved data into a remembered object. Components it doesn't mention keep their state.</summary>
    public static void ReadInto(EntitySaveData data, EntityState entity)
    {
        entity.PrefabId = data.prefab;
        entity.Spawned = data.spawned;
        entity.Destroyed = data.destroyed;
        entity.Active = data.active;
        entity.HasTransform = data.hasTransform;
        entity.Position = data.position;
        entity.Rotation = data.rotation;
        if (data.destroyed)
        {
            entity.Components.Clear();
            return;
        }

        foreach (ComponentSaveData component in data.components)
        {
            if (!string.IsNullOrEmpty(component?.key))
            {
                entity.Components[component.key] = component.state;
            }
        }
    }

    private static List<int> ToSaved(List<CompanionMemory> memories) => memories.ConvertAll(memory => (int)memory);

    private static List<CompanionMemory> FromSaved(List<int> memories) => memories == null ? new List<CompanionMemory>() : memories.ConvertAll(memory => (CompanionMemory)memory);

    /// <summary>Rebuilds the in-memory state from a save. Entries whose content no longer exists are skipped and listed.</summary>
    public static LoadedGame FromSaveData(SaveData data, IContentLookup content)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var loaded = new LoadedGame
        {
            Scene = data.scene,
            Pose = data.hasPlayerPose ? new PlayerPose(data.playerPosition, data.playerYaw) : (PlayerPose?)null,
        };

        PlayerSaveData player = data.player ?? new PlayerSaveData();
        var resources = new ResourceStorage();
        List<ItemAmount> carriedMaterials = ReadAmounts(player.resources, resources, content, loaded.MissingContent);
        ItemContainer bag = ReadSlots(player.bag, NewContainerFor(player.bag, carriedMaterials), content, loaded.MissingContent);
        AddMaterials(carriedMaterials, bag);
        foreach (string id in player.discoveredResources)
        {
            // Materials that became items (older saves) have nothing left to discover.
            if (content.FindResource(id) == null && content.FindItem(id) != null)
            {
                continue;
            }

            ResourceDefinition resource = Resolve(content.FindResource(id), "resource", id, loaded.MissingContent);
            if (resource != null)
            {
                resources.MarkDiscovered(resource);
            }
        }

        var known = new List<RecipeDefinition>();
        foreach (string id in player.knownRecipes)
        {
            RecipeDefinition recipe = Resolve(content.FindRecipe(id), "recipe", id, loaded.MissingContent);
            if (recipe != null)
            {
                known.Add(recipe);
            }
        }

        var buildings = new List<BuildingDefinition>();
        foreach (string id in player.knownBuildings ?? new List<string>())
        {
            BuildingDefinition building = Resolve(content.FindBuilding(id), "building", id, loaded.MissingContent);
            if (building != null)
            {
                buildings.Add(building);
            }
        }

        PlayerSnapshot.Condition state = player.hasNeeds
            ? new PlayerSnapshot.Condition(player.hunger, player.thirst, player.equippedSlot)
            : new PlayerSnapshot.Condition { EquippedSlot = player.equippedSlot };
        ItemContainer gear = player.hasGear ? ReadSlots(player.gear ?? new List<SlotSaveData>(), new ItemContainer(GearSlots.Count, 1), content, loaded.MissingContent) : null;
        loaded.Player = PlayerSnapshot.Capture(bag, resources, known, ReadPocket(player.safePocket, content, loaded.MissingContent), buildings, state, gear);
        if (player.quickItems != null)
        {
            var quick = new List<ItemDefinition>();
            foreach (string id in player.quickItems)
            {
                ItemDefinition item = Resolve(content.FindItem(id), "item", id, loaded.MissingContent);
                if (item != null)
                {
                    quick.Add(item);
                }
            }

            loaded.Player.WithQuickItems(quick);
        }

        if (player.skillBarItems != null)
        {
            var onBar = new List<ItemDefinition>();
            foreach (string id in player.skillBarItems)
            {
                onBar.Add(string.IsNullOrEmpty(id) ? null : Resolve(content.FindItem(id), "item", id, loaded.MissingContent));
            }

            loaded.Player.WithSkillBarItems(onBar);
        }

        loaded.Player.HandCrafts = player.handCrafts != null && player.handCrafts.crafts.Count > 0 ? player.handCrafts : null;
        ReadSkills(player, content as ContentDatabase, loaded);

        StashSaveData stash = data.stash ?? new StashSaveData();
        loaded.Stash = new StashStorage();
        ReadSlots(stash.items, loaded.Stash.Items, content, loaded.MissingContent);
        AddMaterials(ReadAmounts(stash.resources, loaded.Stash.Resources, content, loaded.MissingContent), loaded.Stash.Items);
        loaded.World = ReadWorld(data.world);
        loaded.Quests = ReadQuests(data.quests, content, loaded.MissingContent);
        if (data.happenedEvents != null)
        {
            foreach (string eventId in data.happenedEvents)
            {
                loaded.Quests.RestoreHappened(eventId);
            }
        }
        loaded.Settlement = ReadSettlement(data.settlement, content, loaded.MissingContent);
        loaded.Progress = ReadProgress(data.progress);
        if (!string.IsNullOrEmpty(data.playtest?.runId))
        {
            loaded.Playtest = new PlaytestRun(data.playtest.runId, data.playtest.playedSeconds, data.playtest.sessions);
        }

        loaded.Appearance = data.appearance;
        return loaded;
    }

    /// <summary>All the experience a character level from before skills stood for (that curve: 100, then 1.18 times more
    /// each level, rounded to 5), with the experience towards the next. 0 for a newer save.</summary>
    public static int LegacyExperience(int level, int experience)
    {
        int total = Math.Max(0, experience);
        for (int reached = 1; reached < level; reached++)
        {
            total += Math.Max(1, (int)Math.Round(100.0 * Math.Pow(1.18, reached - 1) / 5.0) * 5);
        }

        return total;
    }

    private static void WriteProgress(CharacterProgress progress, ProgressSaveData into)
    {
        if (progress == null)
        {
            return;
        }

        foreach (KeyValuePair<string, int> skill in progress.Experience)
        {
            into.proficiencies.Add(new AmountSaveData { id = skill.Key, amount = skill.Value });
        }

        into.perks.AddRange(progress.Perks);
        into.insight = progress.Insight;
        foreach (KeyValuePair<string, int> run in progress.PlainRuns)
        {
            into.plainRuns.Add(new AmountSaveData { id = run.Key, amount = run.Value });
        }

        into.techniquesSeen.AddRange(progress.TechniquesSeen);
        into.raidsVisited.AddRange(progress.RaidsVisited);
        into.attunedRunes.AddRange(progress.AttunedRunes);
        into.enchantments.AddRange(progress.LearnedEnchantments);
        into.enchantmentsLearned = true;

        foreach (KeyValuePair<string, int> skill in progress.SkillRanks)
        {
            into.skillRanks.Add(new AmountSaveData { id = skill.Key, amount = skill.Value });
        }

        into.crafted.AddRange(progress.CraftedItems);
        into.stones.AddRange(progress.UsedStones);
        foreach (KeyValuePair<string, int> creature in progress.Creatures)
        {
            into.creatures.Add(new AmountSaveData { id = creature.Key, amount = creature.Value });
        }

        JournalRecord journal = progress.Journal;
        into.journal = new JournalSaveData { pinned = journal.PinnedQuest ?? "" };
        into.journal.read.AddRange(journal.Read);
        into.journal.announced.AddRange(journal.Announced);
        into.journal.found.AddRange(journal.Found);
        into.journal.killedBy.AddRange(journal.KilledBy);
        into.journal.tried.AddRange(journal.Tried);
        into.journal.discovered.AddRange(journal.Discovered);
        into.journal.noticed.AddRange(journal.Noticed);
        foreach (KeyValuePair<string, float> fish in journal.BiggestFish)
        {
            into.journal.biggestFish.Add(new AmountSaveData { id = fish.Key, amount = (int)Math.Round(fish.Value * 10f) });
        }
        foreach (RaidRecord raid in journal.Raids)
        {
            into.journal.raids.Add(new RaidRecordSaveData { raid = raid.Raid, outcome = raid.Outcome, seconds = raid.Seconds, value = raid.Value, broughtDownBy = raid.BroughtDownBy ?? "", lines = new List<string>(raid.Lines) });
        }
    }

    // Perks that no longer exist are dropped (their milestone waits for a new choice). A save from before skills turns
    // its character experience into Insight, to put into skills; its talents are gone.
    private static CharacterProgress ReadProgress(ProgressSaveData saved)
    {
        var progress = new CharacterProgress();
        if (saved == null)
        {
            return progress;
        }

        LevelingSettings settings = LevelingSettings.Current;
        var experience = new List<KeyValuePair<string, int>>();
        foreach (AmountSaveData skill in saved.proficiencies ?? new List<AmountSaveData>())
        {
            experience.Add(new KeyValuePair<string, int>(skill.id, skill.amount));
        }

        var perks = new List<string>();
        foreach (string perk in saved.perks ?? new List<string>())
        {
            if (settings.FindPerk(perk, out _) != null)
            {
                perks.Add(perk);
            }
        }

        int insight = saved.insight + LegacyExperience(saved.level, saved.experience);

        int deepest = EssenceSettings.Current.SkillRanks.Count;
        var skillRanks = new List<KeyValuePair<string, int>>();
        foreach (AmountSaveData skill in saved.skillRanks ?? new List<AmountSaveData>())
        {
            skillRanks.Add(new KeyValuePair<string, int>(skill.id, Math.Min(skill.amount, deepest)));
        }

        progress.Restore(experience, perks, insight, saved.crafted, saved.stones, skillRanks);
        var runs = new List<KeyValuePair<string, int>>();
        foreach (AmountSaveData run in saved.plainRuns ?? new List<AmountSaveData>())
        {
            runs.Add(new KeyValuePair<string, int>(run.id, run.amount));
        }

        progress.RestorePlainRuns(runs);
        progress.RestoreTechniquesSeen(saved.techniquesSeen);
        progress.RestoreRaidsVisited(saved.raidsVisited);
        progress.RestoreAttunedRunes(saved.attunedRunes);
        progress.RestoreEnchantments(saved.enchantments);
        if (!saved.enchantmentsLearned)
        {
            // Before inscriptions every enchantment was for sale at the altar: an old save keeps all it could carve.
            foreach (EnchantmentDefinition enchantment in EssenceSettings.Current.Enchantments)
            {
                if (enchantment != null && enchantment.KnownInOldSaves)
                {
                    progress.Learn(enchantment);
                }
            }
        }
        var creatures = new List<KeyValuePair<string, int>>();
        foreach (AmountSaveData creature in saved.creatures ?? new List<AmountSaveData>())
        {
            creatures.Add(new KeyValuePair<string, int>(creature.id, creature.amount));
        }

        progress.RestoreCreatures(creatures);
        JournalSaveData journal = saved.journal ?? new JournalSaveData();
        var raids = new List<RaidRecord>();
        foreach (RaidRecordSaveData raid in journal.raids ?? new List<RaidRecordSaveData>())
        {
            raids.Add(new RaidRecord { Raid = raid.raid, Outcome = raid.outcome, Seconds = raid.seconds, Value = raid.value, BroughtDownBy = string.IsNullOrEmpty(raid.broughtDownBy) ? null : raid.broughtDownBy, Lines = raid.lines ?? new List<string>() });
        }

        progress.Journal.Restore(journal.read ?? new List<string>(), journal.announced ?? new List<string>(), journal.found ?? new List<string>(), journal.killedBy ?? new List<string>(), raids, string.IsNullOrEmpty(journal.pinned) ? null : journal.pinned,
            journal.tried ?? new List<string>(), journal.discovered ?? new List<string>());
        progress.Journal.RestoreNoticed(journal.noticed);
        foreach (AmountSaveData fish in journal.biggestFish ?? new List<AmountSaveData>())
        {
            progress.Journal.NoteCatch(fish.id, fish.amount / 10f, out _);
        }

        return progress;
    }

    private static void WriteAmounts(IEnumerable<ResourceAmount> amounts, List<AmountSaveData> into)
    {
        foreach (ResourceAmount amount in amounts)
        {
            into.Add(new AmountSaveData { id = amount.Resource.Id, amount = amount.Amount });
        }
    }

    private static ItemContainer ReadPocket(List<SlotSaveData> slots, IContentLookup content, List<string> missing)
    {
        return slots != null && slots.Count > 0 ? ReadSlots(slots, NewContainerFor(slots), content, missing) : null;
    }

    // Sized to hold every saved slot index, so the layout survives, plus room for materials from older saves; the
    // player's bag decides its real size on arrival.
    private static ItemContainer NewContainerFor(List<SlotSaveData> slots, List<ItemAmount> extra = null)
    {
        int highest = 0;
        foreach (SlotSaveData slot in slots)
        {
            highest = Math.Max(highest, slot.slot);
        }

        int extraSlots = 0;
        if (extra != null)
        {
            foreach (ItemAmount amount in extra)
            {
                extraSlots += (amount.Amount + amount.Item.MaximumStack - 1) / amount.Item.MaximumStack;
            }
        }

        return new ItemContainer(highest + 1 + extraSlots, 1);
    }

    private static void AddMaterials(List<ItemAmount> materials, ItemContainer into)
    {
        foreach (ItemAmount amount in materials)
        {
            into.Add(amount.Item, amount.Amount);
        }
    }

    public static ItemContainer ReadSlots(List<SlotSaveData> slots, ItemContainer into, IContentLookup content, List<string> missing)
    {
        foreach (SlotSaveData slot in slots)
        {
            ItemDefinition item = Resolve(content.FindItem(slot.item), "item", slot.item, missing);
            if (item == null || slot.count <= 0)
            {
                continue;
            }

            EnchantmentDefinition enchantment = string.IsNullOrEmpty(slot.enchantment) ? null
                : Resolve(EssenceSettings.Current.FindEnchantment(slot.enchantment), "enchantment", slot.enchantment, missing);
            var stack = new ItemStack(item, slot.count, 1f - slot.wear, slot.empower, enchantment, (ItemQuality)Math.Max(0, Math.Min(slot.quality, 2)), slot.maker, slot.size);
            if (into.IsValid(slot.slot) && into.GetSlot(slot.slot).IsEmpty)
            {
                into.SetSlot(slot.slot, stack);
            }
            else
            {
                into.AddStack(stack);
            }
        }

        return into;
    }

    /// <summary>
    /// Restores resource amounts. Saves from before materials became items list them here too; those come back as items
    /// (their ids match), returned for the caller to put in the bag or stash.
    /// </summary>
    private static List<ItemAmount> ReadAmounts(List<AmountSaveData> amounts, ResourceStorage into, IContentLookup content, List<string> missing)
    {
        var becameItems = new List<ItemAmount>();
        foreach (AmountSaveData amount in amounts ?? new List<AmountSaveData>())
        {
            ResourceDefinition resource = content.FindResource(amount.id);
            if (resource != null)
            {
                into.SetAmount(resource, amount.amount);
                continue;
            }

            ItemDefinition item = Resolve(content.FindItem(amount.id), "resource", amount.id, missing);
            if (item != null && amount.amount > 0)
            {
                becameItems.Add(new ItemAmount(item, amount.amount));
            }
        }

        return becameItems;
    }

    // The skill book and bar, when the save has them (a bar of up to SkillLoadout.SlotCount slots: JsonUtility writes a list
    // it wasn't given as an empty one; saves from before 2026-11-14 hold five, the rest of the bar starts empty). Skills are looked up in the ContentDatabase only, not through IContentLookup, so a
    // lookup without skills (tests) leaves the starting skills alone, and so does a save none of whose skills can be found.
    private static void ReadSkills(PlayerSaveData player, ContentDatabase database, LoadedGame loaded)
    {
        if (database == null || player.skillBar == null || player.skillBar.Count == 0 || player.skillBar.Count > SkillLoadout.SlotCount)
        {
            return;
        }

        var known = new List<SkillDefinition>();
        foreach (string id in player.knownSkills ?? new List<string>())
        {
            SkillDefinition skill = Resolve(database.FindSkill(id), "skill", id, loaded.MissingContent);
            if (skill != null)
            {
                known.Add(skill);
            }
        }

        if (known.Count == 0)
        {
            return;
        }

        var bar = new SkillDefinition[SkillLoadout.SlotCount];
        for (int i = 0; i < bar.Length; i++)
        {
            string id = i < player.skillBar.Count ? player.skillBar[i] : null;
            bar[i] = string.IsNullOrEmpty(id) ? null : Resolve(database.FindSkill(id), "skill", id, loaded.MissingContent);
        }

        loaded.Player.WithSkills(known, bar);
    }

    private static T Resolve<T>(T found, string kind, string id, List<string> missing) where T : UnityEngine.Object
    {
        if (found == null)
        {
            missing.Add($"{kind} '{id}'");
        }

        return found;
    }

    private static TerritorySaveData WriteTerritory(TerritoryState territory)
    {
        var saved = new TerritorySaveData
        {
            turn = territory.Turn,
            restUntil = territory.RestUntil,
            firstClaimTurn = territory.FirstClaimTurn,
            freeClaimUsed = territory.FreeClaimUsed,
            hasPush = territory.Incursion != null,
        };
        foreach (TerritoryState.Hold hold in territory.Holds)
        {
            saved.holds.Add(new RegionHoldSaveData
            {
                region = hold.Region, claimed = hold.Claimed, everClaimed = hold.EverClaimed, hold = hold.Value,
                taskProgress = hold.TaskProgress, taskFrom = hold.TaskFrom, tasksDone = hold.TasksDone,
            });
        }

        if (territory.Incursion != null)
        {
            saved.pushRegion = territory.Incursion.Region;
            saved.pushDue = territory.Incursion.DueTurn;
            saved.pushStrength = territory.Incursion.Strength;
            saved.pushWeakened = territory.Incursion.Weakened;
        }

        return saved;
    }

    private static void ReadTerritory(TerritorySaveData saved, TerritoryState into)
    {
        if (saved == null)
        {
            into.Restore(0, 0, -1, false, null, null);
            return;
        }

        var holds = new List<TerritoryState.Hold>();
        foreach (RegionHoldSaveData hold in saved.holds ?? new List<RegionHoldSaveData>())
        {
            holds.Add(new TerritoryState.Hold
            {
                Region = hold.region, Claimed = hold.claimed, EverClaimed = hold.everClaimed, Value = hold.hold,
                TaskProgress = hold.taskProgress, TaskFrom = hold.taskFrom, TasksDone = hold.tasksDone,
            });
        }

        TerritoryState.Push push = saved.hasPush && !string.IsNullOrEmpty(saved.pushRegion)
            ? new TerritoryState.Push { Region = saved.pushRegion, DueTurn = saved.pushDue, Strength = saved.pushStrength, Weakened = saved.pushWeakened }
            : null;
        into.Restore(saved.turn, saved.restUntil, saved.firstClaimTurn, saved.freeClaimUsed, holds, push);
    }
}
