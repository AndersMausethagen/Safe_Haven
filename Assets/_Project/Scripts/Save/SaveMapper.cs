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
        };
    }

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
        into.breach = (int)settlement.Breach;
        into.breachesWon = settlement.BreachesWon;
        into.nextBreachAt = settlement.NextBreachAt;
        into.breachPendingSince = settlement.BreachPendingSince;
        into.wardStrain = settlement.WardStrain;
        into.breachWeakening = settlement.BreachWeakening;
        into.purificationQueued = settlement.PurificationQueued;
        into.purificationProgress = settlement.PurificationProgress;
        into.purificationBonus = settlement.PurificationBonus;
        foreach (ResidentRecord record in settlement.Residents)
        {
            into.residents.Add(new ResidentSaveData { id = record.Resident.Id, workPaused = record.WorkPaused, job = record.Job != null ? record.Job.Id : "" });
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
                settlement.Find(resident).WorkPaused = entry.workPaused;
            }
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
            into.Add(new QuestSaveData { id = entry.Quest.Id, status = (int)entry.Status, counts = new List<int>(entry.Counts) });
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
                quests.Restore(quest, (QuestStatus)entry.status, entry.counts);
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
                var entityData = new EntitySaveData
                {
                    id = entity.Id, prefab = entity.PrefabId, spawned = entity.Spawned, destroyed = entity.Destroyed, active = entity.Active,
                    hasTransform = entity.HasTransform, position = entity.Position, rotation = entity.Rotation,
                };
                foreach (KeyValuePair<string, string> component in entity.Components)
                {
                    entityData.components.Add(new ComponentSaveData { key = component.Key, state = component.Value });
                }

                sceneData.entities.Add(entityData);
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

                EntityState entity = scene.GetOrAdd(entityData.id);
                entity.PrefabId = entityData.prefab;
                entity.Spawned = entityData.spawned;
                entity.Destroyed = entityData.destroyed;
                entity.Active = entityData.active;
                entity.HasTransform = entityData.hasTransform;
                entity.Position = entityData.position;
                entity.Rotation = entityData.rotation;
                foreach (ComponentSaveData component in entityData.components)
                {
                    if (!string.IsNullOrEmpty(component?.key))
                    {
                        entity.Components[component.key] = component.state;
                    }
                }
            }
        }

        return world;
    }

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

    private static void WriteProgress(CharacterProgress progress, ProgressSaveData into)
    {
        if (progress == null)
        {
            return;
        }

        into.level = progress.Level;
        into.experience = progress.Experience;
        foreach (KeyValuePair<string, int> rank in progress.Ranks)
        {
            into.talents.Add(new AmountSaveData { id = rank.Key, amount = rank.Value });
        }

        foreach (KeyValuePair<string, int> skill in progress.SkillRanks)
        {
            into.skillRanks.Add(new AmountSaveData { id = skill.Key, amount = skill.Value });
        }

        into.crafted.AddRange(progress.CraftedItems);
        into.stones.AddRange(progress.UsedStones);
    }

    // Talents that no longer exist give their points back.
    private static CharacterProgress ReadProgress(ProgressSaveData saved)
    {
        var progress = new CharacterProgress();
        if (saved == null)
        {
            return progress;
        }

        LevelingSettings settings = LevelingSettings.Current;
        var ranks = new List<KeyValuePair<string, int>>();
        foreach (AmountSaveData talent in saved.talents ?? new List<AmountSaveData>())
        {
            TalentDefinition known = settings.FindTalent(talent.id);
            if (known != null)
            {
                ranks.Add(new KeyValuePair<string, int>(talent.id, Math.Min(talent.amount, known.MaxRank)));
            }
        }

        int deepest = EssenceSettings.Current.SkillRanks.Count;
        var skillRanks = new List<KeyValuePair<string, int>>();
        foreach (AmountSaveData skill in saved.skillRanks ?? new List<AmountSaveData>())
        {
            skillRanks.Add(new KeyValuePair<string, int>(skill.id, Math.Min(skill.amount, deepest)));
        }

        progress.Restore(saved.level, saved.experience, ranks, saved.crafted, saved.stones, skillRanks);
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
            var stack = new ItemStack(item, slot.count, 1f - slot.wear, slot.empower, enchantment);
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

    private static T Resolve<T>(T found, string kind, string id, List<string> missing) where T : UnityEngine.Object
    {
        if (found == null)
        {
            missing.Add($"{kind} '{id}'");
        }

        return found;
    }
}
