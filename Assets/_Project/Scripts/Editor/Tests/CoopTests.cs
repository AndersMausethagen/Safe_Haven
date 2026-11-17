using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>Co-op: where friends are drawn between poses, who may join, and that their bodies are set up like yours.</summary>
public sealed class CoopTests
{
    private static PoseSnapshot At(double time, float x, float yaw = 0f) =>
        new PoseSnapshot { Time = time, Position = new Vector3(x, 0f, 0f), Yaw = yaw };

    // Review 2026-11-17 (S8): a friend's lit beacon called none of the host's creatures; its call reached only this game's, which on
    // the host's run are the host's puppets. A world sound for the creatures goes where they are made too.
    [Test]
    public void AWorldSound_OnTheHostsRun_IsHeardWhereTheCreaturesAreMade()
    {
        var host = new HearsSounds();
        CreatureSource.Remote = host;
        try
        {
            WorldNoise.EmitShared(new Vector3(1f, 2f, 3f), 35f);
            WorldNoise.EmitShared(Vector3.zero, 0f);
        }
        finally
        {
            CreatureSource.Remote = null;
        }

        Assert.AreEqual(1, host.Heard.Count, "the beacon's call, and nothing for a silent one");
        Assert.AreEqual(new Vector4(1f, 2f, 3f, 35f), host.Heard[0]);
        WorldNoise.EmitShared(Vector3.zero, 35f);
        Assert.AreEqual(1, host.Heard.Count, "in one's own game it is heard here only");
    }

    private sealed class HearsSounds : IRaidCreatureSource
    {
        public readonly List<Vector4> Heard = new List<Vector4>();

        public bool Wake(SpawnGroup group) => true;

        public void Spawn(GameObject prefab, Vector3 position, float yaw, bool hunt)
        {
        }

        public void Hear(Vector3 position, float radius) => Heard.Add(new Vector4(position.x, position.y, position.z, radius));
    }

    [Test]
    public void Poses_AreDrawnBetweenTheTwoAroundTheMoment()
    {
        var poses = new PoseBuffer();
        poses.Add(At(1.0, 0f, 0f));
        poses.Add(At(1.1, 1f, 90f));
        Assert.IsTrue(poses.Sample(1.05, 6f, out Vector3 position, out float yaw, out _));
        Assert.AreEqual(0.5f, position.x, 0.001f);
        Assert.AreEqual(45f, yaw, 0.001f);
    }

    [Test]
    public void Poses_ArrivingOutOfOrder_AreSlottedIn_AndRepeatsDropped()
    {
        var poses = new PoseBuffer();
        poses.Add(At(1.0, 0f));
        poses.Add(At(1.2, 2f));
        poses.Add(At(1.1, 1f)); // late
        poses.Add(At(1.1, 9f)); // a repeat
        Assert.AreEqual(3, poses.Count);
        poses.Sample(1.15, 6f, out Vector3 position, out _, out _);
        Assert.AreEqual(1.5f, position.x, 0.001f);
    }

    [Test]
    public void Poses_HoldTheNewest_AndJumpAcrossAPortal()
    {
        var poses = new PoseBuffer();
        poses.Add(At(1.0, 0f));
        poses.Add(At(1.1, 1f));
        poses.Sample(5.0, 6f, out Vector3 late, out _, out _);
        Assert.AreEqual(1f, late.x, 0.001f, "Nothing newer: stays where last seen, no guessing.");

        poses.Add(At(1.2, 40f));
        poses.Sample(1.15, 6f, out Vector3 jumped, out _, out _);
        Assert.AreEqual(40f, jumped.x, 0.001f, "Forty metres in a tenth of a second is a portal, not a slide.");
    }

    [Test]
    public void Poses_KeepOnlyTheNewest_WhenFull()
    {
        var poses = new PoseBuffer(4);
        for (int i = 0; i < 10; i++)
        {
            poses.Add(At(i, i));
        }

        Assert.AreEqual(4, poses.Count);
        Assert.AreEqual(9.0, poses.NewestTime);
        poses.Add(At(0.5, 0f));
        Assert.AreEqual(4, poses.Count, "Older than everything kept: dropped.");
    }

    [Test]
    public void Joining_NeedsTheSameVersion_AndRoom()
    {
        Assert.IsTrue(CoopSession.MayJoin("1", "1", 3, 4, out _));
        Assert.IsFalse(CoopSession.MayJoin("1", "1", 4, 4, out string full));
        StringAssert.Contains("full", full);
        Assert.IsFalse(CoopSession.MayJoin("0", "1", 1, 4, out string version));
        StringAssert.Contains("version", version);
    }

    [Test]
    public void Addresses_AreAnIp_WithAnOptionalPort()
    {
        Assert.IsTrue(CoopSession.TryParseAddress("192.168.1.20", 7777, out string ip, out ushort port));
        Assert.AreEqual(("192.168.1.20", (ushort)7777), (ip, port));
        Assert.IsTrue(CoopSession.TryParseAddress(" 10.0.0.5:9000 ", 7777, out ip, out port));
        Assert.AreEqual(("10.0.0.5", (ushort)9000), (ip, port));
        Assert.IsFalse(CoopSession.TryParseAddress("", 7777, out _, out _));
        Assert.IsFalse(CoopSession.TryParseAddress("my friend's house", 7777, out _, out _));
        Assert.IsFalse(CoopSession.TryParseAddress("10.0.0.5:0", 7777, out _, out _));
    }

    private static SaveData Game(string who, string scene, Vector3 at)
    {
        var data = new SaveData { scene = scene, hasPlayerPose = true, playerPosition = at, displayName = who + "'s Journey" };
        data.player.knownRecipes.Add(who + " recipe");
        data.progress.level = who == "Host" ? 9 : 3;
        data.appearance.name = who;
        data.settlement.wardLevel = who == "Host" ? 4 : 1;
        data.stash.items.Add(new SlotSaveData { item = who + " heirloom", count = 1 });
        data.world.Add(new SceneSaveData { scene = scene });
        data.world[0].entities.Add(new EntitySaveData { id = who + " house", spawned = true });
        data.quests.Add(new QuestSaveData());
        return data;
    }

    [Test]
    public void AVisit_IsTheHostsWorld_WithYourCharacter_BesideTheHost()
    {
        SaveData host = Game("Host", "SafeHaven", new Vector3(10f, 0f, 20f));
        SaveData world = HavenVisit.WorldOf(host, hostIsHome: true);
        Assert.IsEmpty(world.player.knownRecipes, "Nothing of the host's character is sent.");
        Assert.AreEqual(1, world.progress.level);

        SaveData guest = Game("Guest", "SafeHaven", new Vector3(-5f, 0f, -5f));
        SaveData visit = HavenVisit.Combine(world, guest);
        Assert.AreEqual("Host house", visit.world[0].entities[0].id, "The host's buildings.");
        Assert.AreEqual(4, visit.settlement.wardLevel, "The host's settlement.");
        Assert.AreEqual("Host heirloom", visit.stash.items[0].item, "The host's stash (closed to guests for now).");
        Assert.AreEqual("Guest recipe", visit.player.knownRecipes[0], "Your bag and recipes.");
        Assert.AreEqual(3, visit.progress.level, "Your level.");
        Assert.AreEqual("Guest", visit.appearance.name);
        Assert.AreEqual(new Vector3(11.6f, 0f, 21.6f), visit.playerPosition, "Beside the host.");
    }

    [Test]
    public void AHostOnARaid_IsVisitedInTheirHaven()
    {
        SaveData world = HavenVisit.WorldOf(Game("Host", "Raid_Deepwood", new Vector3(1f, 2f, 3f)), hostIsHome: false);
        Assert.AreEqual(HavenVisit.HavenScene, world.scene);
        Assert.IsFalse(world.hasPlayerPose, "Not beside them in the raid: at the Haven's arrival point.");
    }

    [Test]
    public void SavingWhileVisiting_KeepsYourOwnWorld_WithWhatYourCharacterGained()
    {
        SaveData home = Game("Guest", "SafeHaven", new Vector3(-5f, 0f, -5f));
        SaveData visit = HavenVisit.Combine(HavenVisit.WorldOf(Game("Host", "SafeHaven", Vector3.zero), true), home);
        visit.player.knownRecipes.Add("learned on the visit");
        visit.progress.level = 4;

        SaveData kept = HavenVisit.KeepHome(home, visit);
        Assert.AreEqual("Guest house", kept.world[0].entities[0].id, "Your own buildings, untouched.");
        Assert.AreEqual(1, kept.settlement.wardLevel);
        Assert.AreEqual("Guest heirloom", kept.stash.items[0].item);
        Assert.AreEqual(new Vector3(-5f, 0f, -5f), kept.playerPosition, "You load at home, where you left.");
        CollectionAssert.Contains(kept.player.knownRecipes, "learned on the visit");
        Assert.AreEqual(4, kept.progress.level);
        Assert.AreEqual(1, home.player.knownRecipes.Count, "The kept home game isn't changed by the visit.");
    }

    [Test]
    public void AWorld_TravelsInPieces_AndArrivesWhole_InAnyOrder()
    {
        SaveData host = Game("Host", "SafeHaven", Vector3.one);
        for (int i = 0; i < 400; i++)
        {
            host.world[0].entities.Add(new EntitySaveData { id = "tree " + i, position = new Vector3(i, 0f, i * 0.5f) });
        }

        byte[] packed = WorldTransfer.Pack(HavenVisit.WorldOf(host, true));
        var pieces = WorldTransfer.Split(packed, 1000);
        Assert.Greater(pieces.Count, 1);
        var receiver = new WorldTransfer.Receiver();
        byte[] whole = null;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            byte[] result = receiver.Add(7, i, pieces.Count, pieces[i]);
            receiver.Add(7, i, pieces.Count, pieces[i]); // a repeat changes nothing
            if (result != null)
            {
                whole = result;
            }
        }

        Assert.IsNotNull(whole);
        SaveData arrived = WorldTransfer.Unpack(whole);
        Assert.AreEqual(401, arrived.world[0].entities.Count);
        Assert.AreEqual("tree 399", arrived.world[0].entities[400].id);
    }

    [Test]
    public void SteamJoinLinks_NameTheLobby()
    {
        Assert.IsTrue(SteamLobby.TryParseConnect("+connect_lobby 109775241234567890", out ulong lobby));
        Assert.AreEqual(109775241234567890UL, lobby);
        Assert.IsTrue(SteamLobby.TryParseConnect("\"C:/Games/Safe Haven.exe\" -screen-fullscreen 1 +connect_lobby 42", out lobby),
            "Steam starting the game puts it among the other arguments.");
        Assert.AreEqual(42UL, lobby);
        Assert.IsFalse(SteamLobby.TryParseConnect("+connect_lobby", out _));
        Assert.IsFalse(SteamLobby.TryParseConnect("+connect_lobby friend", out _));
        Assert.IsFalse(SteamLobby.TryParseConnect("+connect_lobby 0", out _));
        Assert.IsFalse(SteamLobby.TryParseConnect("", out _));
    }

    [Test]
    public void AFriendsGear_DressesThemAsItDressesYou()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition armour = null;
        foreach (ItemDefinition item in content.Items)
        {
            if (item.WornLook != null)
            {
                armour = item;
                break;
            }
        }

        Assert.IsNotNull(armour, "Some item has a worn look.");
        var look = new PlayerLookState { Gear = new Unity.Collections.FixedString512Bytes($"|{armour.Id}||") };
        Assert.AreEqual(armour, look.Worn((GearSlot)1, content));
        Assert.IsNull(look.Worn((GearSlot)0, content));
        Assert.IsNull(look.Worn((GearSlot)40, content), "A slot past the list is empty, not an error.");

        var outfit = new List<GearLookDefinition>();
        PlayerLook.Outfit(slot => look.Worn(slot, content), 0, outfit);
        Assert.Contains(armour.WornLook, outfit);
        Assert.Contains(CharacterCatalog.Current.PlainShoes, outfit, "Bare feet wear plain shoes, as at home.");
    }

    [Test]
    public void TheNetworkPlayer_IsSetUp()
    {
        MultiplayerSettings settings = MultiplayerSettings.Current;
        Assert.AreEqual(4, settings.MaxPlayers);
        Assert.IsNotNull(settings.PlayerPrefab, "Resources/MultiplayerSettings points at the Network Player prefab.");
        Assert.IsNotNull(settings.PlayerPrefab.GetComponent<NetworkPlayerAvatar>());
        Assert.IsFalse(settings.PlayerPrefab.SynchronizeTransform, "Poses go in the avatar's own messages.");

        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        Assert.AreEqual(player.GetComponent<Animator>().runtimeAnimatorController, settings.BodyAnimations,
            "A friend's body runs the player's own Animator Controller, so the states sent are states it has.");
    }

    [Test]
    public void AFriendsBody_IsNotAPlayer()
    {
        var holder = new GameObject("Coop Test");
        try
        {
            RemotePlayerBody body = RemotePlayerBody.Create(holder.transform, MultiplayerSettings.Current);
            Assert.IsNull(body.GetComponentInParent<Player>(), "Nothing in the game may take a friend for you.");
            Animator animator = body.GetComponent<Animator>();
            Assert.IsFalse(animator.fireEvents, "Clip events call into player components a friend's body doesn't have.");
            Assert.IsFalse(animator.applyRootMotion);
            Assert.IsNotNull(body.GetComponent<ModularCharacter>());
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void TheStash_IsTheHosts_AndAGuestSendsOnlyWhatTheyPutInOrTook()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition material = null;
        foreach (ItemDefinition item in content.Items)
        {
            if (item != null && item.Category == ItemCategory.Material && item.MaximumStack >= 10)
            {
                material = item;
                break;
            }
        }

        Assert.IsNotNull(material);
        var host = new StashStorage();
        host.Items.Add(material, 10);
        var guestStash = new StashStorage();
        var guest = new StashShare();

        Assert.IsTrue(guest.TakeSnapshot(StashShare.SnapshotOf(host, 0), guestStash, content));
        Assert.AreEqual(10, guestStash.Items.Count(material));
        Assert.IsNull(guest.CollectChange(guestStash), "Nothing done, nothing sent.");

        guestStash.Items.Remove(material, 4);
        StashShare.Change change = guest.CollectChange(guestStash);
        Assert.AreEqual(1, change.number);
        Assert.AreEqual(1, change.items.Count);
        Assert.AreEqual(-4, change.items[0].count);

        // Meanwhile the host's workshop made 3 more: the guest's change doesn't overwrite them.
        host.Items.Add(material, 3);
        StashShare.ApplyChange(change, host, content);
        Assert.AreEqual(9, host.Items.Count(material));

        Assert.IsFalse(guest.TakeSnapshot(StashShare.SnapshotOf(host, 0), guestStash, content),
            "A stash from before the guest's change would show what they took for a moment, and let them take it twice.");
        Assert.AreEqual(6, guestStash.Items.Count(material));
        Assert.IsTrue(guest.TakeSnapshot(StashShare.SnapshotOf(host, 1), guestStash, content));
        Assert.AreEqual(9, guestStash.Items.Count(material));
    }

    [Test]
    public void TheHostsStash_IsSent_OnlyWhenItChanged()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition material = null;
        foreach (ItemDefinition item in content.Items)
        {
            if (item != null && item.Category == ItemCategory.Material)
            {
                material = item;
                break;
            }
        }

        var host = new StashStorage();
        var share = new StashShare();
        Assert.IsTrue(share.HostStashChanged(host), "The first look sends it.");
        Assert.IsFalse(share.HostStashChanged(host));
        host.Items.Add(material, 1);
        Assert.IsTrue(share.HostStashChanged(host));
        Assert.IsFalse(share.HostStashChanged(host));
    }

    [Test]
    public void AChangedObject_IsSent_WithoutWhatEachPlayerKeeps_AndDoesntWipeWhatItDoesntMention()
    {
        var entity = new EntityState("node-1") { PrefabId = "p", Active = true };
        entity.Components["node"] = "{\"depleted\":true}";
        entity.Components["exploration"] = "mine";

        EntitySaveData sent = SaveMapper.ToData(entity, key => key != "exploration");
        Assert.AreEqual(1, sent.components.Count);
        Assert.AreEqual("node", sent.components[0].key);

        var theirs = new EntityState("node-1");
        theirs.Components["exploration"] = "theirs";
        SaveMapper.ReadInto(sent, theirs);
        Assert.AreEqual("{\"depleted\":true}", theirs.Components["node"]);
        Assert.AreEqual("theirs", theirs.Components["exploration"], "Each player's own state stays theirs.");

        sent.destroyed = true;
        SaveMapper.ReadInto(sent, theirs);
        Assert.IsTrue(theirs.Destroyed);
        Assert.AreEqual(0, theirs.Components.Count);
    }

    [Test]
    public void SavedComponents_SayHowTheyAreShared()
    {
        Assert.IsTrue(typeof(ISharedState).IsAssignableFrom(typeof(ExplorationMap)));
        Assert.IsTrue(typeof(ISharedState).IsAssignableFrom(typeof(DayNightCycle)));
        Assert.IsTrue(typeof(ISharedState).IsAssignableFrom(typeof(StoryScript)));
        Assert.IsTrue(typeof(ISharedState).IsAssignableFrom(typeof(ResourceNode)), "Nodes leave their regrowth countdown out of the comparison.");
        Assert.IsTrue(typeof(ISharedState).IsAssignableFrom(typeof(TerrainTreeMemory)), "Felled trees leave their countdowns out too.");
    }

    [Test]
    public void AFriendsSettlement_IsTakenOnInPlace_SoListenersStay()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var mine = new SettlementState();
        int heard = 0;
        mine.Changed += () => heard++;

        var theirs = new SettlementState();
        theirs.RaiseWardLevel();
        theirs.RaiseWardLevel();
        theirs.RepairPortal();
        SettlementSaveData sent = SaveMapper.ToData(theirs);

        mine.TakeOn(SaveMapper.SettlementFrom(sent, content));
        Assert.AreEqual(2, mine.WardLevel);
        Assert.IsTrue(mine.PortalRepaired);
        Assert.AreEqual(1, heard, "Taking it on is announced once, to the same listeners.");

        mine.RaiseWardLevel();
        Assert.AreEqual(3, mine.WardLevel);
        Assert.AreEqual(2, heard, "Still listened to afterwards.");
    }

    [Test]
    public void AtHome_NothingIsLeftToAHost()
    {
        HavenVisit.Forget();
        Assert.IsFalse(HavenVisit.LeftToHost());
        Assert.IsFalse(HavenVisit.MayBuild);
        Assert.IsFalse(HavenVisit.MayUseStash);
    }

    [Test]
    public void AFollowersRaid_IsTheHostsRun_SameMapSameRules()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        RaidDefinition raid = null;
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var candidate = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate != null && candidate.Difficulties.Count > 1)
            {
                raid = candidate;
                break;
            }
        }

        Assert.IsNotNull(raid, "A raid with tiers to test with.");
        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in content.Items)
        {
            if (item != null && item.IsRaidModifier && item.RaidModifier != null)
            {
                modifiers.Add(item.RaidModifier);
                break;
            }
        }

        RaidDifficulty tier = raid.Difficulties[1];
        RaidSession host = RaidLauncher.Build(raid, modifiers, null, "SafeHaven", 12345, tier, 2, false, 180f, 0.1f, 1, null, 0f);
        RaidInvite invite = WorldTransfer.Unpack<RaidInvite>(WorldTransfer.Pack(RaidInvite.Of(host, 180f, 0.1f, 1)));
        Assert.AreSame(raid, invite.FindRaid(new[] { raid }));

        RaidSession follower = RaidLauncher.FromInvite(invite, raid, content, null, "SafeHaven", 90f);
        Assert.AreEqual(host.Seed, follower.Seed);
        Assert.AreSame(host.Difficulty, follower.Difficulty);
        Assert.AreEqual(host.TotalSeconds, follower.TotalSeconds, 0.001f);
        Assert.AreEqual(host.Parameters.ExtractionTime, follower.Parameters.ExtractionTime, 0.001f);
        Assert.AreEqual(host.Parameters.EnemyHealthMultiplier, follower.Parameters.EnemyHealthMultiplier, 0.001f);
        Assert.AreEqual(host.Parameters.LootMultiplier, follower.Parameters.LootMultiplier, 0.001f);
        CollectionAssert.AreEqual(host.Modifiers, follower.Modifiers);
        CollectionAssert.AreEqual(host.Foretold, follower.Foretold, "The same events are foretold, so the same ones happen.");
        Assert.AreEqual(host.TotalSeconds - 90f, follower.TimeRemaining, 0.001f, "The follower's clock starts where the host's stands.");
    }

    [Test]
    public void HealthFloors_FromSeveralHolders_TheHighestCounts_AndNoneUndoesAnother()
    {
        var holder = new GameObject("Floor Test");
        try
        {
            Damageable health = holder.AddComponent<Damageable>();
            var downedRule = new object();
            Assert.AreEqual(0f, health.HealthFloor);

            health.HealthFloor = 1f; // a Deathless relic
            health.HoldFloor(downedRule, 1f);
            health.ReleaseFloor(downedRule);
            Assert.AreEqual(1f, health.HealthFloor, "Letting go of the downed rule's floor leaves the relic's.");

            health.HoldFloor(downedRule, 1f);
            health.HealthFloor = 0f; // the relic spent
            Assert.AreEqual(1f, health.HealthFloor, "The relic letting go leaves the downed rule's floor.");
            health.ReleaseFloor(downedRule);
            Assert.AreEqual(0f, health.HealthFloor);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void BeingDown_TravelsWithAFriendsLook()
    {
        var look = new PlayerLookState { Downed = true, Health = 90, Visibility = 64 };
        Assert.IsFalse(look.Equals(new PlayerLookState()), "Going down is a change friends hear of.");
        using var writer = new FastBufferWriter(1024, Unity.Collections.Allocator.Temp);
        writer.WriteNetworkSerializable(look);
        using var reader = new FastBufferReader(writer, Unity.Collections.Allocator.Temp);
        reader.ReadNetworkSerializable(out PlayerLookState read);
        Assert.IsTrue(read.Downed);
        Assert.AreEqual(90, read.Health);
        Assert.AreEqual(64, read.Visibility, "A friend sneaking is as hard to see in the host's game as in their own.");
    }

    [Test]
    public void AFriendDownOrFallen_SaysSoOverTheirHead()
    {
        var holder = new GameObject("Coop Test");
        try
        {
            RemotePlayerBody body = RemotePlayerBody.Create(holder.transform, MultiplayerSettings.Current);
            var tag = body.GetComponentInChildren<TMPro.TextMeshPro>();
            body.ShowState("Astrid", true, false);
            Assert.AreEqual("Astrid · Down!", tag.text);
            Assert.AreEqual(UIStyle.Current.Danger, tag.color);
            body.ShowState("Astrid", false, true);
            Assert.AreEqual("Astrid · Fallen", tag.text);
            body.ShowState("Astrid", false, false);
            Assert.AreEqual("Astrid", tag.text);
            Assert.AreEqual(UIStyle.Current.Text, tag.color);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void WhatAVisitorWinsOnAFriendsRaid_ReachesTheirOwnHaven()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ResidentDefinition traveller = null;
        foreach (string guid in AssetDatabase.FindAssets("t:ResidentDefinition"))
        {
            traveller = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (traveller != null)
            {
                break;
            }
        }

        Assert.IsNotNull(traveller);
        var homeField = typeof(HavenVisit).GetField("home", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var ownGame = new SaveData { scene = "SafeHaven" };
        homeField.SetValue(null, ownGame);
        try
        {
            // GameSession.Home is the guest's own Haven while they visit (guard 9).
            Assert.AreSame(HavenVisit.OwnHaven, GameSession.Home);
            Assert.IsTrue(GameSession.Home.AddResident(traveller));
            Assert.IsFalse(GameSession.Home.AddResident(traveller), "Once.");
            GameSession.Home.MarkBossDefeated("heartroot");
            GameSession.Home.MarkBossDefeated("heartroot");

            SaveData written = HavenVisit.ForOwnSave(new SaveData { scene = "SafeHaven" });
            Assert.AreEqual(1, written.settlement.residents.Count);
            Assert.AreEqual(traveller.Id, written.settlement.residents[0].id);
            CollectionAssert.AreEqual(new[] { "heartroot" }, written.settlement.defeatedBosses);
            SettlementState read = SaveMapper.SettlementFrom(written.settlement, content);
            Assert.IsNotNull(read.Find(traveller), "Home, they live in the Haven.");
            Assert.IsTrue(read.IsBossDefeated("heartroot"));
        }
        finally
        {
            HavenVisit.Forget();
        }
    }

    [Test]
    public void AVisitorBuildsAndOpensStoresOnlyWithTheHostsYes_AndKeepsWhatTheNokkTaughtThem()
    {
        var homeField = typeof(HavenVisit).GetField("home", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        homeField.SetValue(null, new SaveData { scene = "SafeHaven" });
        typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting)).SetValue(null, true);
        try
        {
            // Every way to build (the build menu, the Haven window, a building's own upgrade) goes through this.
            Assert.IsTrue(HavenVisit.BuildingNeedsAsking(), "no yes yet: the host is asked, nothing is built");
            HavenVisit.Allow(VisitorRight.Build, true);
            Assert.IsFalse(HavenVisit.BuildingNeedsAsking(), "with the yes");

            // The host's stores (the stash, their carts' cargo) take the other yes, separately.
            Assert.IsTrue(HavenVisit.StoresNeedAsking(), "building's yes isn't the stores'");
            HavenVisit.Allow(VisitorRight.Stash, true);
            Assert.IsFalse(HavenVisit.StoresNeedAsking());
            HavenVisit.Allow(VisitorRight.Stash, false);
            Assert.IsTrue(HavenVisit.StoresNeedAsking(), "taken back");

            Assert.IsFalse(SpiritBonds.HasGift(SpiritBonds.NokkGift));
            GameSession.Home.GiveSpiritGift(SpiritBonds.NokkGift);
            GameSession.Home.GiveSpiritGift(SpiritBonds.NokkGift);
            Assert.IsTrue(SpiritBonds.HasGift(SpiritBonds.NokkGift), "learnt at a friend's pond, it is theirs");
            SaveData written = HavenVisit.ForOwnSave(new SaveData { scene = "SafeHaven" });
            CollectionAssert.AreEqual(new[] { SpiritBonds.NokkGift }, written.settlement.spiritGifts, "and goes home with them, once");
        }
        finally
        {
            HavenVisit.Forget();
        }

        Assert.IsFalse(HavenVisit.BuildingNeedsAsking(), "at home, building needs no one's yes");
        Assert.IsFalse(HavenVisit.StoresNeedAsking(), "nor do one's own stores");
    }

    [Test]
    public void AVisitor_HasNoRaidToPlan_AndIsToldToFollowTheHost()
    {
        GameSession.StartNewGame();
        var host = new GameObject("Portal");
        var visitor = new GameObject("Visitor");
        var portal = host.AddComponent<RaidPortal>();
        var so = new SerializedObject(portal);
        so.FindProperty("raid").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        so.FindProperty("needsRepair").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        var homeField = typeof(HavenVisit).GetField("home", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        homeField.SetValue(null, new SaveData { scene = "SafeHaven" });
        var told = new List<string>();
        System.Action<string, MessageKind> listen = (text, kind) => told.Add(text);
        GameMessages.Posted += listen;
        try
        {
            var atHome = new MenuPage();
            portal.AddActions(atHome, visitor);
            Assert.IsTrue(atHome.Actions.Exists(action => action.Label == "Plan a raid" && action.Enabled), "At home the planner opens");

            typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting)).SetValue(null, true);
            var visiting = new MenuPage();
            portal.AddActions(visiting, visitor);
            MenuAction plan = visiting.Actions.Find(action => action.Label == "Plan a raid");
            Assert.IsNotNull(plan, "Still shown, so the visitor learns why");
            Assert.IsFalse(plan.Enabled, "A visitor follows the host instead of planning (nothing laid out for a run they can't enter)");
            Assert.AreEqual(HavenVisit.RaidRefusal, plan.Detail);

            // A portal without a station opens the planner when used: a visitor is told instead.
            typeof(RaidPortal).GetMethod("OpenPlanner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(portal, new object[] { visitor });
            CollectionAssert.Contains(told, HavenVisit.RaidRefusal);
        }
        finally
        {
            GameMessages.Posted -= listen;
            HavenVisit.Forget();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(visitor);
            GameSession.StartNewGame();
        }
    }
}
