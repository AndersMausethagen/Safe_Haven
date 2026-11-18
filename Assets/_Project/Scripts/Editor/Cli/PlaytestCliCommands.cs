using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Play-mode helpers for the Unity CLI (`unity command sh_give ...`): inspect and set up the player, quests, raids and
/// saves while testing. They replace the long eval scripts that used to set up each playtest by hand.
/// </summary>
public static class PlaytestCliCommands
{
    private const string Tag = "safehaven";
    private static readonly LeaseKey CliInvulnerability = new LeaseKey("an agent probe from the command line");

    [CliCommand("sh_player", "Summarise the player: scene, raid, health, needs, stats, bag, pocket, gear, essences and quests.", Tags = new[] { Tag })]
    public static string Player()
    {
        GameObject player = RequirePlayer();
        var sb = new StringBuilder();
        sb.Append($"Scene {SceneManager.GetActiveScene().name}");
        RaidDirector director = RaidDirector.Current;
        if (director != null && director.Session != null)
        {
            RaidSession raid = director.Session;
            sb.Append($" | Raid {raid.Definition.name} stage {director.StageIndex} {raid.TimeRemaining:F0}s left, {raid.Outcome}");
        }

        sb.AppendLine();
        if (player.TryGetComponent(out Damageable health))
        {
            sb.Append($"HP {health.CurrentHealth:F0}/{health.MaxHealth:F0}{(health.IsInvulnerable ? " (invulnerable)" : "")}");
        }

        if (player.TryGetComponent(out PlayerNeeds needs))
        {
            sb.Append($" | Hunger {needs.Hunger.Current:F0}/{needs.Hunger.Max:F0} Thirst {needs.Thirst.Current:F0}/{needs.Thirst.Max:F0}");
        }

        sb.AppendLine();
        if (player.TryGetComponent(out Combatant combatant))
        {
            IEnumerable<string> stats = Enum.GetValues(typeof(StatType)).Cast<StatType>()
                .Select(stat => (stat, value: combatant.Stats.Get(stat)))
                .Where(pair => Mathf.Abs(pair.value) > 0.0001f)
                .Select(pair => $"{pair.stat} {pair.value.ToString("0.##", CultureInfo.InvariantCulture)}");
            sb.AppendLine("Stats: " + string.Join(", ", stats));
        }

        if (player.TryGetComponent(out ItemInventory inventory))
        {
            ItemContainer bag = inventory.Container;
            sb.AppendLine($"Bag {Used(bag)}/{bag.UnlockedSlots} slots, {bag.CurrentWeight:0.#}/{(bag.HasWeightLimit ? bag.WeightCapacity.ToString("0.#") : "-")} wt: {Contents(bag)}");
            if (inventory.SafePocket != null)
            {
                sb.AppendLine($"Pocket: {Contents(inventory.SafePocket)}");
            }
        }

        if (player.TryGetComponent(out PlayerEquipment equipment))
        {
            var gear = new List<string>();
            foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
            {
                ItemStack stack = equipment.Gear.Get(slot);
                if (!stack.IsEmpty)
                {
                    gear.Add($"{slot}={stack.Item.name}");
                }
            }

            sb.AppendLine("Gear: " + (gear.Count > 0 ? string.Join(", ", gear) : "none"));
        }

        ResourceStorage essences = Essences(player);
        if (essences != null)
        {
            IEnumerable<string> held = essences.StoredAmounts.Where(a => a.Amount > 0).Select(a => $"{a.Resource.name} {a.Amount}");
            sb.AppendLine("Essences: " + JoinOrNone(held));
        }

        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        IEnumerable<string> active = log.Entries.Where(e => e.Status == QuestStatus.Active || e.Status == QuestStatus.ReadyToTurnIn)
            .Select(e => $"{e.Quest.name}{(e.Status == QuestStatus.ReadyToTurnIn ? " (hand in)" : "")} [{string.Join(",", e.Counts.Select((c, i) => $"{c}/{e.Quest.Objectives[i].Amount}"))}]");
        sb.Append("Quests: " + JoinOrNone(active));
        return sb.ToString();
    }

    [CliCommand("sh_give", "Give (or with a negative amount, take) items or essences.", Tags = new[] { Tag })]
    public static string Give(
        [CliArg("id", "Item or essence id, display name or asset name")] string id,
        [CliArg("amount", "How many; negative removes")] int amount = 1,
        [CliArg("to", "bag, pocket or stash")] string to = "bag")
    {
        ItemDefinition item = CliLookup.Find<ItemDefinition>(id);
        ResourceDefinition essence = item == null ? CliLookup.Find<ResourceDefinition>(id) : null;
        if (item == null && essence == null)
        {
            throw new ArgumentException($"No item or essence matches '{id}'.");
        }

        if (essence != null)
        {
            ResourceStorage storage = to == "stash" ? GameSession.Stash.Resources : Essences(RequirePlayer());
            if (storage == null)
            {
                throw new InvalidOperationException("The player has no essence storage.");
            }

            int changed = amount >= 0 ? storage.Add(essence, amount) : -storage.Remove(essence, -amount);
            return $"{essence.name} {Signed(changed)} -> {storage.GetAmount(essence)}";
        }

        ItemContainer container = ResolveContainer(to);
        if (amount < 0)
        {
            int removed = container.Remove(item, -amount);
            return $"{item.name} -{removed} -> {container.Count(item)} in {to}";
        }

        int accepted = to == "bag" && RequirePlayer().TryGetComponent(out ItemInventory inventory)
            ? inventory.Give(item, amount, ArrivalCause.Found)
            : container.Add(item, amount);
        string refused = accepted < amount ? $" ({amount - accepted} didn't fit)" : "";
        return $"{item.name} +{accepted}{refused} -> {container.Count(item)} in {to}, weight {container.CurrentWeight:0.#}";
    }

    [CliCommand("sh_clear", "Empty the bag, safe pocket, essences or all of them.", Tags = new[] { Tag })]
    public static string Clear([CliArg("what", "bag, pocket, essences, stash or all")] string what = "bag")
    {
        GameObject player = RequirePlayer();
        var cleared = new List<string>();
        if (what == "bag" || what == "all")
        {
            ResolveContainer("bag").Clear();
            cleared.Add("bag");
        }

        if (what == "pocket" || what == "all")
        {
            ResolveContainer("pocket").Clear();
            cleared.Add("pocket");
        }

        if (what == "essences" || what == "all")
        {
            ResourceStorage essences = Essences(player);
            foreach (var held in essences.StoredAmounts.ToList())
            {
                essences.Remove(held.Resource, held.Amount);
            }

            cleared.Add("essences");
        }

        if (what == "stash")
        {
            GameSession.Stash.Items.Clear();
            cleared.Add("stash");
        }

        if (cleared.Count == 0)
        {
            throw new ArgumentException("--what must be bag, pocket, essences, stash or all.");
        }

        return "Cleared " + string.Join(", ", cleared);
    }

    [CliCommand("sh_player_set", "Set player health, hunger, thirst or invulnerability. Negative values leave a field alone.", Tags = new[] { Tag })]
    public static string SetPlayer(
        [CliArg("health", "Health to set (revives if dead)")] float health = -1f,
        [CliArg("hunger", "Hunger meter value")] float hunger = -1f,
        [CliArg("thirst", "Thirst meter value")] float thirst = -1f,
        [CliArg("invulnerable", "on, off, or empty to leave as is")] string invulnerable = "")
    {
        GameObject player = RequirePlayer();
        Damageable damageable = player.GetComponent<Damageable>();
        if (health >= 0f && damageable != null)
        {
            if (!damageable.IsAlive)
            {
                damageable.Revive(Mathf.Clamp01(health / damageable.MaxHealth));
            }
            else if (health > damageable.CurrentHealth)
            {
                damageable.Heal(health - damageable.CurrentHealth);
            }
            else if (health < damageable.CurrentHealth)
            {
                var hit = new DamageInfo(null, null, damageable.CurrentHealth - health, DamageType.Physical, false, player.transform.position, Vector3.down);
                damageable.ApplyDamage(hit);
            }
        }

        if (player.TryGetComponent(out PlayerNeeds needs))
        {
            if (hunger >= 0f)
            {
                needs.Hunger.Set(hunger);
            }

            if (thirst >= 0f)
            {
                needs.Thirst.Set(thirst);
            }
        }

        if (damageable != null && invulnerable == "on")
        {
            damageable.AddInvulnerability(CliInvulnerability);
        }
        else if (damageable != null && invulnerable == "off")
        {
            damageable.RemoveInvulnerability(CliInvulnerability);
        }

        return Player().Split('\n')[1].Trim();
    }

    [CliCommand("sh_quest", "List, start, complete or advance quests.", Tags = new[] { Tag })]
    public static string Quest(
        [CliArg("action", "list, start, complete or progress")] string action = "list",
        [CliArg("id", "Quest id, title or asset name")] string id = "",
        [CliArg("objective", "Objective index for progress")] int objective = 0,
        [CliArg("amount", "Progress amount")] int amount = 1)
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        if (action == "list")
        {
            IEnumerable<QuestDefinition> quests = QuestManager.Current != null
                ? QuestManager.Current.Quests
                : CliLookup.LoadAll(typeof(QuestDefinition)).Cast<QuestDefinition>();
            return string.Join("\n", quests.Where(q => CliLookup.Matches(q, id)).Select(q => DescribeQuest(log, q)));
        }

        QuestDefinition quest = CliLookup.Require<QuestDefinition>(id);
        switch (action)
        {
            case "start":
                if (QuestManager.Current != null)
                {
                    if (!QuestManager.Current.TryStartQuest(quest, out string reason))
                    {
                        throw new InvalidOperationException($"Couldn't start {quest.name}: {reason}");
                    }
                }
                else if (!log.TryStart(quest))
                {
                    throw new InvalidOperationException($"Couldn't start {quest.name}.");
                }

                break;
            case "complete":
                log.ForceComplete(quest);
                break;
            case "turnin":
                if (!log.TryTurnIn(quest))
                {
                    throw new InvalidOperationException($"{quest.name} is {log.StatusOf(quest)}, not ready to hand in.");
                }

                break;
            case "progress":
                if (log.StatusOf(quest) != QuestStatus.Active)
                {
                    throw new InvalidOperationException($"{quest.name} is {log.StatusOf(quest)}; start it first (--action start).");
                }

                log.AddProgress(quest, objective, amount);
                break;
            default:
                throw new ArgumentException("--action must be list, start, complete, turnin or progress.");
        }

        return DescribeQuest(log, quest);
    }

    [CliCommand("sh_raid", "Raid status, launch, extract, skip time or die.", Tags = new[] { Tag })]
    public static string Raid(
        [CliArg("action", "status, launch, extract, skip or die")] string action = "status",
        [CliArg("id", "Raid to launch")] string id = "",
        [CliArg("sparks", "Sparks to bring when launching")] int sparks = 0,
        [CliArg("seconds", "Seconds to skip; 0 skips to 1s before the end")] float seconds = 0f)
    {
        GameObject player = null;
        RaidDirector director = RaidDirector.Current;
        switch (action)
        {
            case "status":
                RequirePlayMode();
                return director == null || director.Session == null
                    ? $"No raid running (scene {SceneManager.GetActiveScene().name})."
                    : $"{director.Session.Definition.name} stage {director.StageIndex} ({director.CurrentStage?.DisplayName ?? "-"}), " +
                      $"{director.Session.TimeRemaining:F0}s left, elapsed {director.Session.Elapsed:F0}s, {director.Session.Outcome}";
            case "launch":
                player = RequirePlayer();
                RaidDefinition raid = CliLookup.Require<RaidDefinition>(id);
                if (!RaidLauncher.TryLaunch(raid, player, Array.Empty<ItemDefinition>(), out string problem, sparks))
                {
                    throw new InvalidOperationException($"Couldn't launch {raid.name}: {problem}");
                }

                return $"Launching {raid.name}. The scene loads over the next frames.";
        }

        if (director == null || director.Session == null)
        {
            throw new InvalidOperationException("No raid is running.");
        }

        switch (action)
        {
            case "extract":
                return director.Extract(director.Player) ? "Extracted." : "Extraction refused.";
            case "skip":
                // Stop just short of the end; the director's own Update then finishes the raid properly.
                float skip = seconds > 0f ? seconds : director.Session.TimeRemaining - 1f;
                director.Session.Tick(Mathf.Min(skip, director.Session.TimeRemaining - 0.05f));
                return $"{director.Session.TimeRemaining:F1}s left.";
            case "die":
                Damageable damageable = director.Player.GetComponent<Damageable>();
                damageable.RemoveInvulnerability(CliInvulnerability);
                var hit = new DamageInfo(null, null, damageable.MaxHealth * 100f, DamageType.Physical, false, director.Player.transform.position, Vector3.down);
                damageable.ApplyDamage(hit);
                return damageable.IsAlive ? "Player survived (invulnerable?)." : "Player died.";
            default:
                throw new ArgumentException("--action must be status, launch, extract, skip or die.");
        }
    }

    [CliCommand("sh_spawn", "Spawn monsters near the player, snapped to the NavMesh.", Tags = new[] { Tag })]
    public static string Spawn(
        [CliArg("id", "Monster id, name or asset name")] string id,
        [CliArg("count", "How many")] int count = 1,
        [CliArg("distance", "Metres in front of the player")] float distance = 6f)
    {
        GameObject player = RequirePlayer();
        MonsterDefinition monster = CliLookup.Require<MonsterDefinition>(id);
        if (monster.Prefab == null)
        {
            throw new InvalidOperationException($"{monster.name} has no prefab.");
        }

        var spawned = new List<string>();
        foreach (GameObject instance in Cheats.Spawn(monster.Prefab, count, distance))
        {
            Vector3 position = instance.transform.position;
            spawned.Add($"{instance.name} at {position.x:F0},{position.y:F0},{position.z:F0}");
        }

        return "Spawned " + string.Join("; ", spawned);
    }

    [CliCommand("sh_save", "Save, load, or describe the save file.", Tags = new[] { Tag })]
    public static string Save([CliArg("action", "save, load or info")] string action = "info")
    {
        switch (action)
        {
            case "info":
                string file = !SaveGameService.HasSave ? "No save file."
                    : SaveGameService.TryReadSave(out _, out string readProblem) ? "Save file reads cleanly." : "Save file broken: " + readProblem;
                string canSave = EditorApplication.isPlaying ? SaveGameService.CheckSave() ?? "Saving allowed here." : "Not playing.";
                return $"{file} {canSave}";
            case "save":
                RequirePlayer();
                return SaveGameService.TrySave(out string saveProblem) ? "Saved." : "Save failed: " + saveProblem;
            case "load":
                RequirePlayer();
                return SaveGameService.TryLoad(CliLookup.Database, "SafeHaven", out string loadProblem)
                    ? "Loading save. The scene loads over the next frames."
                    : "Load failed: " + loadProblem;
            default:
                throw new ArgumentException("--action must be save, load or info.");
        }
    }

    private static void RequirePlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Enter play mode first (unity command editor_play).");
        }
    }

    private static GameObject RequirePlayer()
    {
        RequirePlayMode();

        if (RaidDirector.Current != null && RaidDirector.Current.Player != null)
        {
            return RaidDirector.Current.Player;
        }

        PlayerEquipment equipment = Object.FindAnyObjectByType<PlayerEquipment>();
        if (equipment == null)
        {
            throw new InvalidOperationException($"No player in scene {SceneManager.GetActiveScene().name} (menu or loading screen?).");
        }

        return equipment.gameObject;
    }

    private static ItemContainer ResolveContainer(string to)
    {
        switch (to)
        {
            case "stash":
                return GameSession.Stash.Items;
            case "bag":
            case "pocket":
                ItemInventory inventory = RequirePlayer().GetComponent<ItemInventory>();
                if (inventory == null)
                {
                    throw new InvalidOperationException("The player has no ItemInventory.");
                }

                return to == "bag" ? inventory.Container : inventory.SafePocket;
            default:
                throw new ArgumentException("--to must be bag, pocket or stash.");
        }
    }

    private static ResourceStorage Essences(GameObject player)
    {
        return player.TryGetComponent(out ResourceInventory resources) ? resources.Storage : null;
    }

    private static string DescribeQuest(QuestLog log, QuestDefinition quest)
    {
        QuestLog.Entry entry = log.Get(quest);
        string progress = entry != null
            ? string.Join(", ", quest.Objectives.Select((o, i) => $"{o.Describe()} {entry.Counts[i]}/{o.Amount}"))
            : string.Join(", ", quest.Objectives.Select(o => o.Describe()));
        return $"{quest.name}: {log.StatusOf(quest)} - {progress}";
    }

    private static int Used(ItemContainer container)
    {
        int used = 0;
        for (int i = 0; i < container.SlotCount; i++)
        {
            if (!container.GetSlot(i).IsEmpty)
            {
                used++;
            }
        }

        return used;
    }

    private static string Contents(ItemContainer container)
    {
        var totals = new Dictionary<string, int>();
        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty)
            {
                totals.TryGetValue(stack.Item.name, out int count);
                totals[stack.Item.name] = count + stack.Count;
            }
        }

        return JoinOrNone(totals.Select(pair => $"{pair.Key} {pair.Value}"));
    }

    private static string JoinOrNone(IEnumerable<string> parts)
    {
        string joined = string.Join(", ", parts);
        return joined.Length > 0 ? joined : "none";
    }

    private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString(CultureInfo.InvariantCulture);
}
