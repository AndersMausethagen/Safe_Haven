using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode checks for the second base progression pass (`unity command script run -- --file AgentScripts/ProbeHavenProgression.cs --entry ProbeHavenProgression.Home`).
/// Plain Instantiate: nothing here is saved. Never saves the game.
/// </summary>
public static class ProbeHavenProgression
{
    private static BuildingDefinition Def(string file) => AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/" + file + ".asset");

    private static ItemDefinition Item(string name) =>
        AssetDatabase.FindAssets("t:ItemDefinition " + name).Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(one => one.name == name);

    private static Construction Raise(string file, Vector3 at, int level)
    {
        BuildingDefinition definition = Def(file);
        GameObject made = Object.Instantiate(definition.Prefab, at, Quaternion.identity);
        made.name = "Probe " + file;
        var construction = made.GetComponent<Construction>();
        construction.RestoreState($"{level},{level},0,1");
        return construction;
    }

    public static string Home()
    {
        var log = new StringBuilder();
        Player player = Player.Current;
        SettlementState settlement = GameSession.Settlement;
        log.AppendLine($"start: ward {settlement.WardLevel}, portal {settlement.PortalLevel}, rank {settlement.HavenRank}, residents {settlement.Residents.Count}, beds {settlement.Beds}, scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        Vector3 origin = player.transform.position + player.transform.forward * 8f;

        RaidDefinition raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        log.AppendLine($"tier II before ward: {RaidLauncher.CheckWard(raid.Difficulties[1]) ?? "open"}");
        log.AppendLine($"difficulty for score 500 at this ward: tier {raid.DifficultyFor(500).tier}");

        // A worker with and without a bed.
        Construction hunter = Raise("TanningRack", origin + Vector3.right * 10f, 2);
        CraftingStation lodge = hunter.GetComponent<CraftingStation>();
        JobDefinition job = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Hunter.asset");
        ResidentDefinition gunnar = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Settlement/Gunnar.asset");
        bool had = settlement.HasResident(gunnar);
        settlement.AddResident(gunnar, job);
        // The bed goes to whoever is first in line, so count how many are ahead of Gunnar.
        int ahead = settlement.Residents.ToList().FindIndex(one => one.Resident == gunnar);
        HavenProgress.Current.Count(false);
        log.AppendLine($"hunter's lodge speed, no spare bed: {lodge.Speed:0.00} (beds {settlement.Beds}, ahead {ahead})");
        var huts = new List<Construction>();
        for (int i = 0; i < ahead / 3 + 1; i++)
        {
            huts.Add(Raise("Hut", origin + Vector3.back * (6f + i * 5f), 2));
        }

        HavenProgress.Current.Count(false);
        log.AppendLine($"after {huts.Count} huts: beds {settlement.Beds}, housed {settlement.Housed}, gunnar has bed {settlement.HasBed(settlement.Find(gunnar))}, speed {lodge.Speed:0.00}, comfort {HavenReadiness.Comfort}");

        // Stations list what their tier opens.
        foreach (string file in new[] { "TanningRack", "Loom", "Kitchen", "Cauldron", "ConstructionTable", "Sawbench" })
        {
            BuildingDefinition definition = Def(file);
            var crafting = player.GetComponent<PlayerCrafting>();
            for (int tier = 2; tier <= definition.MaxLevel; tier++)
            {
                log.AppendLine($"{definition.DisplayName} {tier}: {HavenBuildings.WhatTierBrings(definition, tier, definition.UpgradeTo(tier), crafting).Replace("\n", " | ")}");
            }
        }

        // The Trading Post's page.
        Construction post = Raise("TradingPost", origin + Vector3.left * 10f, 1);
        player.GetComponent<ItemInventory>().Give(Item("GildedTrollIdol"), 1);
        MethodInfo build = typeof(TradingPost).GetMethod("BuildPage", BindingFlags.NonPublic | BindingFlags.Static);
        var page = (MenuPage)build.Invoke(null, new object[] { player.gameObject });
        log.AppendLine($"trading post: {page.Body}");
        foreach (MenuAction action in page.Actions)
        {
            log.AppendLine($"  {action.Label} [{action.Detail}] enabled={action.Enabled}");
        }

        MenuAction sell = page.Actions.FirstOrDefault(action => action.Label.StartsWith("Sell Gilded"));
        int before = GameSession.Stash.Items.Count(TradeSettings.Current.Coin);
        sell?.Chosen();
        log.AppendLine($"sold idol: pennies in stash {before} -> {GameSession.Stash.Items.Count(TradeSettings.Current.Coin)}");

        // The portal's menu.
        RaidPortal portal = Object.FindAnyObjectByType<RaidPortal>();
        if (portal != null)
        {
            var portalPage = new MenuPage { Title = "probe" };
            portal.AddActions(portalPage, player.gameObject);
            foreach (MenuAction action in portalPage.Actions)
            {
                log.AppendLine($"portal: {action.Label} [{action.Detail}] enabled={action.Enabled}");
            }
        }

        ItemInventory inventory = player.GetComponent<ItemInventory>();
        log.AppendLine($"pocket: {inventory.SafePocket.UnlockedSlots} of {inventory.SafePocket.SlotCount}");
        settlement.ImprovePortal();
        inventory.RefreshPocket();
        log.AppendLine($"pocket after improving: {inventory.SafePocket.UnlockedSlots}; extraction cut {RaidLauncher.ExtractionCut:0.00}");

        // A level that asks for another building refuses until it stands.
        Construction smithy = Raise("Forge", origin + Vector3.forward * 10f, 2);
        log.AppendLine($"Smithy III open: {BuildingUnlocks.IsOpen(Def("Forge").UpgradeTo(3))} / {BuildingUnlocks.Missing(Def("Forge").UpgradeTo(3))}");
        Construction tailor = Raise("Loom", origin + Vector3.forward * 16f, 1);
        log.AppendLine($"Smithy III open with both: {BuildingUnlocks.IsOpen(Def("Forge").UpgradeTo(3))}");
        log.AppendLine($"tailor's shop station tier {tailor.GetComponent<CraftingStation>().Tier}; trading post rank gate open: {BuildingUnlocks.IsOpen(Def("TradingPost"))} / {BuildingUnlocks.Missing(Def("TradingPost"))}");

        // Rank brings settlers without any bed.
        if (!had)
        {
            log.AppendLine("gunnar added for the probe");
        }

        return log.ToString();
    }

    /// <summary>A live raid: the field kits. Call after launching and setting the player invulnerable.</summary>
    public static string Raid()
    {
        var log = new StringBuilder();
        Player player = Player.Current;
        ItemInventory inventory = player.GetComponent<ItemInventory>();
        log.AppendLine($"raid? {RaidDirector.Current != null}, exits {ExtractionPoint.Active.Count}, tier {RaidDirector.Current?.Session.Tier}");

        foreach (string name in new[] { "ForwardCampKit", "WaystoneShard", "FieldRepairKit" })
        {
            inventory.Container.AddStack(new ItemStack(Item(name), 1));
        }

        int Slot(string name)
        {
            for (int i = 0; i < inventory.Container.SlotCount; i++)
            {
                if (!inventory.Container.GetSlot(i).IsEmpty && inventory.Container.GetSlot(i).Item.name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        player.Health.ApplyDamage(new DamageInfo(null, null, 40f, DamageType.Physical, false, player.transform.position, Vector3.up, 40f, HitTechnique.None));
        log.AppendLine($"hurt: {player.Health.CurrentHealth:0}/{player.Health.MaxHealth:0}");
        log.AppendLine($"camp used: {inventory.Use(Slot("ForwardCampKit"))}; camps {Object.FindObjectsByType<FieldCamp>(FindObjectsSortMode.None).Length}");
        int exits = ExtractionPoint.Active.Count;
        log.AppendLine($"shard used: {inventory.Use(Slot("WaystoneShard"))}; exits {exits} -> {ExtractionPoint.Active.Count}");
        ExtractionPoint newest = ExtractionPoint.Active[ExtractionPoint.Active.Count - 1];
        log.AppendLine($"new exit at {newest.transform.position}, player at {player.transform.position}, duration {newest.Duration:0.0}, near {Vector3.Distance(newest.transform.position, player.transform.position):0.0}");

        var equipment = player.GetComponent<PlayerEquipment>();
        ItemContainer worn = equipment.Gear.Slots;
        int wornSlot = -1;
        for (int i = 0; i < worn.SlotCount; i++)
        {
            if (!worn.GetSlot(i).IsEmpty && worn.GetSlot(i).Item.HasDurability)
            {
                wornSlot = i;
                break;
            }
        }

        if (wornSlot >= 0)
        {
            worn.SetDurability(wornSlot, 0.2f);
            log.AppendLine($"worn at {worn.GetSlot(wornSlot).Durability:0.00}; kit used {inventory.Use(Slot("FieldRepairKit"))}; now {worn.GetSlot(wornSlot).Durability:0.00}");
        }
        else
        {
            log.AppendLine("nothing worn to mend; kit refused: " + !inventory.Use(Slot("FieldRepairKit")));
        }

        return log.ToString();
    }

    public static string HealthNow() => $"health {Player.Current.Health.CurrentHealth:0}/{Player.Current.Health.MaxHealth:0}";

    /// <summary>A breach with a Tier III Watch Post and a totem beside the ward. Reports how much they do on their own.</summary>
    public static string BreachStart()
    {
        Player player = Player.Current;
        Vector3 at = HomePortal.Current.transform.position + new Vector3(10f, 0f, 0f);
        Raise("WatchPost", at, 3);
        var totem = Raise("WardTotem", at + new Vector3(4f, 0f, 4f), 1);
        GameSession.Settlement.WarnOfBreach();
        WardBreachDirector.Current.Begin();
        return $"breach active: {WardBreachDirector.Current.IsActive}; totem effect set: {new SerializedObject(totem.GetComponent<WardTotem>()).FindProperty("effect").objectReferenceValue != null}; monsters {MonsterAI.Active.Count}";
    }

    public static string BreachStatus()
    {
        var log = new StringBuilder();
        log.AppendLine($"active {WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive}, monsters {MonsterAI.Active.Count}");
        foreach (MonsterAI monster in MonsterAI.Active.Take(10))
        {
            var health = monster.GetComponent<Damageable>();
            var effects = monster.GetComponent<StatusEffects>();
            log.AppendLine($"{monster.name} {health.CurrentHealth:0}/{health.MaxHealth:0} state {monster.CurrentState} status {(effects != null ? string.Join(",", effects.Current.Select(one => one.Definition.name)) : "-")}");
        }

        return log.ToString();
    }
}
