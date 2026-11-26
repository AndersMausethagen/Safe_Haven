using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The Haven's Reach (Docs/Territory.md): claiming regions, holding them, the forest's push and what is done about it.</summary>
public sealed class TerritoryTests
{
    private const string Region = "Ravenwood";
    private static TerritorySettings Settings => AssetDatabase.LoadAssetAtPath<TerritorySettings>("Assets/_Project/Resources/TerritorySettings.asset");

    // The forest always gathers when it may (roll 0), or never (roll 1).
    private static float Always() => 0f;
    private static float Never() => 1f;

    [Test]
    public void TheFirstClaimIsFree_TheNextWantARune()
    {
        var state = new TerritoryState();
        Assert.IsFalse(state.ClaimNeedsRune(Settings));
        state.Claim(Region, Settings);
        Assert.IsTrue(state.IsClaimed(Region));
        Assert.AreEqual(Settings.ClaimHold, state.HoldOf(Region), 0.01f);
        Assert.IsTrue(state.ClaimNeedsRune(Settings), "Every claim after the first costs a Pact Rune.");
        Assert.AreEqual(RegionStanding.Held, state.StandingOf(Region, Settings));
    }

    [Test]
    public void AHold_WearsAway_FasterUnderBlight_AndIsLostAtNothing()
    {
        var calm = new TerritoryState();
        var blighted = new TerritoryState();
        calm.Claim(Region, Settings);
        blighted.Claim(Region, Settings);
        calm.AdvanceTurn(Settings, 0f, Never, out _);
        blighted.AdvanceTurn(Settings, 1f, Never, out _);
        Assert.Less(blighted.HoldOf(Region), calm.HoldOf(Region));

        var news = new System.Collections.Generic.List<string>();
        float spread = 0f;
        for (int turn = 0; turn < 200 && blighted.IsClaimed(Region); turn++)
        {
            news.AddRange(blighted.AdvanceTurn(Settings, 1f, Never, out float more));
            spread += more;
        }

        Assert.IsFalse(blighted.IsClaimed(Region), "Left alone long enough, the forest takes it back.");
        Assert.Greater(spread, 0f, "Losing a region spreads the blight.");
        Assert.IsTrue(news.Any(n => n.Contains("taken")));
        Assert.IsTrue(blighted.Of(Region).EverClaimed);
    }

    [Test]
    public void ThePush_ComesForTheWeakest_AfterAWarning()
    {
        var state = new TerritoryState();
        state.Claim(Region, Settings);
        state.Claim("Eastwood", Settings);
        state.Raise("Eastwood", 20f);
        for (int i = 0; i < Settings.FirstPushAfter && state.Incursion == null; i++)
        {
            state.AdvanceTurn(Settings, 0f, Always, out _);
        }

        Assert.IsNotNull(state.Incursion, "Once the Haven has held land a while, the forest pushes.");
        Assert.AreEqual(Region, state.Incursion.Region, "At the weakest of the Haven's lands.");
        int due = state.Incursion.DueTurn;
        Assert.AreEqual(state.Turn + Settings.WarningTurns, due, "With warning.");
        float before = state.HoldOf(Region);
        while (state.Turn < due)
        {
            state.AdvanceTurn(Settings, 0f, Never, out _);
        }

        Assert.IsNull(state.Incursion);
        Assert.Less(state.HoldOf(Region), before - Settings.PushStrengthAt(0f) + 0.01f + Settings.ErosionAt(0f) * Settings.WarningTurns, "Unanswered, it takes its strength.");
    }

    [Test]
    public void BurningTwoHearts_StopsThePush_AndAWatchHeldBreaksIt()
    {
        var state = new TerritoryState();
        state.Claim(Region, Settings);
        while (state.Incursion == null)
        {
            state.AdvanceTurn(Settings, 0f, Always, out _);
        }

        Assert.IsTrue(state.Weaken(Settings));
        Assert.AreEqual(state.Incursion.Strength * (1f - Settings.HeartShare), state.Incursion.Remaining, 0.01f);
        state.Weaken(Settings);
        Assert.AreEqual(0f, state.Incursion.Remaining, 0.01f, "Two hearts burned leave it nothing.");

        float before = state.HoldOf(Region);
        Assert.IsTrue(state.Defended(Region, Settings));
        Assert.IsNull(state.Incursion, "A watch held breaks the push.");
        Assert.AreEqual(Mathf.Min(100f, before + Settings.DefenceHold), state.HoldOf(Region), 0.01f);
        Assert.IsFalse(state.Weaken(Settings), "Nothing left to weaken.");
    }

    [Test]
    public void ATask_Done_FirmsTheHold_AndRestsATurn()
    {
        var state = new TerritoryState();
        state.Claim(Region, Settings);
        float before = state.HoldOf(Region);
        for (int i = 0; i < 4; i++)
        {
            Assert.IsFalse(state.CountTask(Region, 5, Settings));
        }

        Assert.IsTrue(state.CountTask(Region, 5, Settings));
        Assert.AreEqual(before + Settings.TaskHold, state.HoldOf(Region), 0.01f);
        Assert.IsFalse(state.TaskOpen(Region), "It asks again after a turn.");
        state.AdvanceTurn(Settings, 0f, Never, out _);
        Assert.IsTrue(state.TaskOpen(Region));
    }

    [Test]
    public void HeldLands_BringRenown_MoreWhenSecure()
    {
        var state = new TerritoryState();
        state.Claim(Region, Settings);
        Assert.AreEqual(Settings.HeldRenown, state.Renown(Settings));
        state.Raise(Region, 100f);
        Assert.AreEqual(RegionStanding.Secure, state.StandingOf(Region, Settings));
        Assert.AreEqual(Settings.SecureRenown, state.Renown(Settings));
    }

    [Test]
    public void TheLands_SurviveASave()
    {
        var settlement = new SettlementState();
        settlement.Territory.Claim(Region, Settings);
        while (settlement.Territory.Incursion == null)
        {
            settlement.Territory.AdvanceTurn(Settings, 0f, Always, out _);
        }

        settlement.Territory.Weaken(Settings);
        settlement.Territory.CountTask(Region, 99, Settings);
        // Through the save the game writes and reads, not SaveMapper's private helpers (SaveCoverageTests leaves the lands to this).
        SaveData saved = SaveMapper.ToSaveData(null, null, "SafeHaven", null, System.DateTime.UtcNow, settlement: settlement);
        LoadedGame game = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)),
            Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
        TerritoryState loaded = game.Settlement.Territory;
        Assert.IsTrue(loaded.IsClaimed(Region));
        Assert.AreEqual(settlement.Territory.HoldOf(Region), loaded.HoldOf(Region), 0.01f);
        Assert.AreEqual(settlement.Territory.Turn, loaded.Turn);
        Assert.IsNotNull(loaded.Incursion);
        Assert.AreEqual(Settings.HeartShare, loaded.Incursion.Weakened, 0.01f);
        Assert.AreEqual(1, loaded.Of(Region).TaskProgress);
        Assert.IsTrue(loaded.FreeClaimUsed);
    }

    [Test]
    public void EveryRegion_IsDescribed_AndHasAStone()
    {
        WorldAtlas atlas = WorldAtlas.For("SafeHaven");
        Assert.IsNotNull(Settings.StonePrefab, "Run AgentScripts/BuildTerritory.cs.");
        Assert.IsNotNull(Settings.StonePrefab.GetComponent<BoundaryStone>());
        Assert.IsNotNull(Settings.PactRune);
        string home = atlas.RegionAt(Vector3.zero).name;
        foreach (WorldAtlas.Region region in atlas.Regions.Where(r => r.name != home))
        {
            TerritorySettings.RegionInfo info = Settings.Info(region.name);
            Assert.IsNotNull(info, $"{region.name} has no words in TerritorySettings (BuildTerritory.Regions).");
            Assert.IsNotEmpty(info.description, region.name);
            Assert.IsNotEmpty(info.tithe, region.name);
            Assert.AreNotEqual(Vector2.zero, region.stoneAt, $"{region.name} has no boundary stone (BuildHavenAtlas).");
            Assert.AreEqual(region.name, atlas.RegionAt(new Vector3(region.stoneAt.x, 0f, region.stoneAt.y)).name, $"{region.name}'s stone stands in it.");
        }
    }

    [Test]
    public void EveryStone_StandsOnDryGround()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>("Assets/_Project/Data/World/Safe Haven/Haven World Recipe.asset");
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        foreach (WorldAtlas.Region region in WorldAtlas.For("SafeHaven").Regions.Where(r => r.stoneAt != Vector2.zero))
        {
            Vector2 at = region.stoneAt;
            Assert.Greater(plan.Lake.Edge(at), 3f, $"{region.name}'s stone stands in the lake.");
            Assert.IsTrue(plan.Meres.All(m => m.Edge(at) > 3f), $"{region.name}'s stone stands in a mere.");
            Assert.IsFalse(plan.HasBog && plan.BogWet(at), $"{region.name}'s stone stands in a bog pool.");
            for (int r = 0; r < plan.Rivers.Count; r++)
            {
                for (int k = 0; k + 1 < plan.Rivers[r].Count; k++)
                {
                    Vector2 a = plan.Rivers[r][k], b = plan.Rivers[r][k + 1], ab = b - a;
                    float along = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude) : 0f;
                    Assert.Greater((a + ab * along - at).magnitude, plan.RiverWidths[r] * 0.5f + 2f, $"{region.name}'s stone stands in a river.");
                }
            }
        }
    }

    // ----- Co-op: a guest's lands mirror the host's, so what they do goes to the host's game (CoopLands).

    private static void Visiting(bool visiting) => typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting)).SetValue(null, visiting);

    private static void ForgetBurnedHearts() =>
        ((System.Collections.Generic.HashSet<int>)typeof(Territory).GetField("burnedOnRuns", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();

    private static void CountTask(string region, RegionTaskKind kind) =>
        typeof(Territory).GetMethod("Count", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { region, kind });

    [Test]
    public void AGuest_SendsTheirWorkToTheHost_AndChangesNothingHere()
    {
        GameSession.StartNewGame();
        TerritoryState lands = GameSession.Here.Territory;
        lands.Claim(Region, Settings);
        while (lands.Incursion == null)
        {
            lands.AdvanceTurn(Settings, 0f, Always, out _);
        }

        float hold = lands.HoldOf(Region);
        var sent = new System.Collections.Generic.List<(LandWork work, string region, int detail)>();
        var user = new GameObject("Guest");
        try
        {
            Visiting(true);
            Territory.ForHost = (work, region, detail) => sent.Add((work, region, detail));
            user.AddComponent<ItemInventory>().Container.SetSlot(0, new ItemStack(Settings.PactRune, 1));

            Assert.IsFalse(Territory.CanClaim("Eastwood", user, out string why), "Lighting the host's stones is theirs.");
            StringAssert.Contains("Pact Rune", why, "and the guest is told how they can help");
            Assert.IsFalse(Territory.CanStandWatch(out _), "The host calls the forest to a stone; friends stand with them.");

            Assert.IsTrue(Territory.CanOffer(Region, user, out _));
            Territory.Offer(Region, user);
            Assert.AreEqual(0, Territory.RunesHeld(user), "The guest's own rune is spent.");

            RegionTaskKind kind = Settings.Info(Region).task;
            CountTask(Region, kind);
            Territory.HeartBurned();

            Assert.AreEqual(hold, lands.HoldOf(Region), 0.01f, "Nothing changes in the mirror: the host's answer comes back with the settlement.");
            Assert.AreEqual(0, lands.Of(Region).TaskProgress);
            Assert.AreEqual(0f, lands.Incursion.Weakened, 0.01f);
            CollectionAssert.AreEqual(new[] { LandWork.Rune, LandWork.Task, LandWork.Heart }, sent.Select(s => s.work).ToArray());
            Assert.IsTrue(sent.All(s => s.region == Region));
            Assert.AreEqual((int)kind, sent[1].detail, "The task's kind goes with it, for the host to check.");

            int turn = lands.Turn;
            Territory.Turn(true);
            Assert.AreEqual(turn, lands.Turn, "A guest's dawn is no turn for the host's lands.");
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
            Territory.ForHost = null;
            Object.DestroyImmediate(user);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void TheHost_TakesAFriendsWork_AndOneHeartARun()
    {
        GameSession.StartNewGame();
        ForgetBurnedHearts();
        TerritoryState lands = GameSession.Here.Territory;
        try
        {
            lands.Claim(Region, Settings);
            float hold = lands.HoldOf(Region);
            Territory.FromFriend(LandWork.Rune, Region, 0, "Ingrid");
            Assert.AreEqual(hold + Settings.RuneHold, lands.HoldOf(Region), 0.01f, "A friend's rune holds as the host's would.");
            hold = lands.HoldOf(Region);
            Territory.FromFriend(LandWork.Find, Region, 0, "Ingrid");
            Assert.AreEqual(hold + Settings.FindHold, lands.HoldOf(Region), 0.01f);

            TerritorySettings.RegionInfo info = Settings.Info(Region);
            RegionTaskKind other = info.task == RegionTaskKind.Hunt ? RegionTaskKind.Fish : RegionTaskKind.Hunt;
            Territory.FromFriend(LandWork.Task, Region, (int)other, "Ingrid");
            Assert.AreEqual(0, lands.Of(Region).TaskProgress, "Work the region didn't ask for doesn't count.");
            hold = lands.HoldOf(Region);
            for (int i = 0; i < info.taskCount; i++)
            {
                Territory.FromFriend(LandWork.Task, Region, (int)info.task, "Ingrid");
            }

            Assert.AreEqual(Mathf.Min(100f, hold + Settings.TaskHold), lands.HoldOf(Region), 0.01f, "A friend's work finishes the task.");

            Territory.FromFriend(LandWork.Rune, "Eastwood", 0, "Ingrid");
            Assert.IsFalse(lands.IsClaimed("Eastwood"), "A rune at an unlit stone claims nothing.");

            while (lands.Incursion == null)
            {
                lands.AdvanceTurn(Settings, 0f, Always, out _);
            }

            Territory.FromFriend(LandWork.Heart, lands.Incursion.Region, 4242, "Ingrid");
            Assert.AreEqual(Settings.HeartShare, lands.Incursion.Weakened, 0.01f);
            Territory.FromFriend(LandWork.Heart, lands.Incursion.Region, 4242, "Bjarne");
            Assert.AreEqual(Settings.HeartShare, lands.Incursion.Weakened, 0.01f, "Everyone on a run has the heart's rumour, but it is one heart.");
            Territory.FromFriend(LandWork.Heart, lands.Incursion.Region, 4343, "Ingrid");
            Assert.AreEqual(Mathf.Min(1f, Settings.HeartShare * 2f), lands.Incursion.Weakened, 0.01f, "Another run's heart counts.");
        }
        finally
        {
            ForgetBurnedHearts();
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void TheLandWorkMessage_SurvivesTheWire()
    {
        using var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp);
        CoopLands.Write(writer, LandWork.Heart, "The Drowning Fen", -123456789);
        using var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp);
        CoopLands.Read(reader, out LandWork work, out string region, out int detail);
        Assert.AreEqual(LandWork.Heart, work);
        Assert.AreEqual("The Drowning Fen", region);
        Assert.AreEqual(-123456789, detail);
        Assert.IsTrue(WorldAtlas.For("SafeHaven").Regions.All(r => System.Text.Encoding.UTF8.GetByteCount(r.name) < 60), "Region names fit the message.");
    }

    // ----- The quest line: the portal's word after the third raid opens the stones (BuildHavensReach) -----

    // The portal has told of the stones (the conversation's end).
    private static void Told() => GameSession.Quests.RestoreHappened(Settings.UnlockedWhen.Id);

    private static QuestDefinition QuestAt(string file) => AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/_Project/Data/Quests/{file}.asset");

    [Test]
    public void TheStones_AreCold_UntilThePortalSpeaks()
    {
        GameSession.StartNewGame();
        try
        {
            Assert.IsNotNull(Settings.UnlockedBy, "Run AgentScripts/BuildHavensReach.cs.");
            Assert.IsFalse(Territory.Unlocked);
            Assert.IsFalse(Territory.CanClaim(Region, null, out string why));
            StringAssert.Contains("cold", why);
            StringAssert.Contains("cold", HavenLands.Summary(), "the Lands tab says so too");

            Assert.IsTrue(GameSession.Quests.TryStart(Settings.UnlockedBy));
            Assert.IsFalse(Territory.Unlocked, "called, but not yet told");
            Assert.IsFalse(Territory.CanClaim(Region, null, out why));
            StringAssert.Contains("wants a word", why);
            Told();
            Assert.IsTrue(Territory.Unlocked);
            Assert.IsFalse(Territory.CanClaim(Region, null, out why));
            StringAssert.Contains("places first", why, "open now: it only wants one of its places found");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void AStoneThatDoesntKnowYou_SaysWhichWayToLook()
    {
        GameSession.StartNewGame();
        var user = new GameObject("Player at the stone");
        try
        {
            GameSession.Quests.TryStart(Settings.UnlockedBy);
            Told();
            WorldAtlas.Region region = WorldAtlas.For("SafeHaven").Regions.First(r => r.name == Region);
            user.transform.position = new Vector3(region.stoneAt.x, 0f, region.stoneAt.y);
            Assert.IsFalse(Territory.CanClaim(Region, user, out string why));
            StringAssert.IsMatch(@"the nearest lies about \d+ m to the (north|south|east|west)", why);
        }
        finally
        {
            Object.DestroyImmediate(user);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void TheLand_TellsTheQuests()
    {
        GameSession.StartNewGame();
        ForgetBurnedHearts();
        var heard = new System.Collections.Generic.List<GameEventDefinition>();
        var used = new System.Collections.Generic.List<QuestTargetDefinition>();
        System.Action<GameEvent> listen = e =>
        {
            if (e.Kind == GameEventKind.Custom && e.Subject is GameEventDefinition said)
            {
                heard.Add(said);
            }
            else if (e.Kind == GameEventKind.Used && e.Subject is QuestTargetDefinition thing)
            {
                used.Add(thing);
            }
        };
        var user = new GameObject("Player");
        GameEvents.Raised += listen;
        try
        {
            GameSession.Quests.TryStart(Settings.UnlockedBy);
            Told();
            foreach (WorldAtlas.Place place in Territory.PlacesIn(Region))
            {
                GameSession.Progress.Journal.NoteDiscovered(place.id);
            }

            Territory.Claim(Region, user);
            CollectionAssert.Contains(heard, Settings.StoneLit);
            QuestTargetDefinition stone = HavenRegionSettings.Current.LookOf(Region)?.stoneTarget;
            Assert.IsNotNull(stone, $"{Region}: a stone a region's arc can end with");
            CollectionAssert.AreEqual(new[] { stone }, used, "its own stone is told, and no other region's");

            user.AddComponent<ItemInventory>().Container.SetSlot(0, new ItemStack(Settings.PactRune, 1));
            Territory.Offer(Region, user);
            CollectionAssert.Contains(heard, Settings.RuneOffered);
            CollectionAssert.DoesNotContain(heard, Settings.LandSecure, "65 is not Secure yet");

            RegionTaskKind kind = Settings.Info(Region).task;
            for (int i = 0; i < Settings.Info(Region).taskCount; i++)
            {
                CountTask(Region, kind);
            }

            CollectionAssert.Contains(heard, Settings.TaskDone);
            Assert.AreEqual(RegionStanding.Secure, Territory.StandingOf(Region));
            Assert.AreEqual(1, heard.Count(e => e == Settings.LandSecure), "Secure is told once, as it is reached");

            for (int turn = 0; turn < 300 && GameSession.Here.Territory.Incursion == null; turn++)
            {
                Territory.Turn(false);
            }

            Assert.IsNotNull(GameSession.Here.Territory.Incursion);
            Assert.AreEqual(1, heard.Count(e => e == Settings.ForestGathers), "the warning is told once");
            Territory.Defended(Region);
            CollectionAssert.Contains(heard, Settings.PushAnswered);
        }
        finally
        {
            GameEvents.Raised -= listen;
            Object.DestroyImmediate(user);
            ForgetBurnedHearts();
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void TheQuestLine_IsWired()
    {
        QuestDefinition cold = QuestAt("TheColdStones"), asks = QuestAt("WhatTheLandAsks"), forest = QuestAt("TheForestAnswers");
        Assert.IsNotNull(cold, "Run AgentScripts/BuildHavensReach.cs.");
        Assert.AreEqual(cold, Settings.UnlockedBy, "the first quest opens the stones");
        foreach (QuestDefinition quest in new[] { cold, asks, forest })
        {
            Assert.AreEqual(QuestRank.Haven, quest.Rank, quest.name + ": beside the story, never in its way");
            Assert.AreEqual("The Haven's Reach", quest.Chain);
            Assert.IsNotEmpty(quest.JournalNotes, quest.name);
            Assert.IsTrue(quest.RewardItems.Any(r => r.Item == Settings.PactRune), quest.name + " pays with what the next stone wants");
        }

        Assert.AreEqual(QuestStart.ByQuestGiver, cold.Start, "the portal starts it (Story - The Cold Stones)");
        Assert.AreEqual(Settings.UnlockedWhen, cold.Objectives[0].QuestEvent, "heard only from the stones conversation: any other talk with the portal (its menu) must not tick it");
        Assert.IsTrue(cold.ObjectivesInOrder);
        Assert.AreEqual(Settings.StoneLit, cold.Objectives[Settings.StonesObjective - 1].QuestEvent, "the stones point the way for the objective that lights one");
        CollectionAssert.AreEqual(new[] { cold }, asks.Prerequisites);
        CollectionAssert.AreEqual(new[] { Settings.TaskDone, Settings.RuneOffered, Settings.LandSecure }, asks.Objectives.Select(o => o.QuestEvent).ToArray());
        Assert.AreEqual(QuestStart.OnQuestEvent, forest.Start);
        Assert.AreEqual(Settings.ForestGathers, forest.StartEvent, "it starts when the forest first gathers");
        Assert.AreEqual(Settings.PushAnswered, forest.Objectives[0].QuestEvent);
        Assert.IsTrue(forest.Objectives[0].CountAlreadyOwned, "an answer in the seconds before it is announced counts (a friend's heart, live 2026-11-11)");
    }

    [Test]
    public void ThePortalSpeaks_AfterTheThirdRaid()
    {
        GameSession.StartNewGame();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(WhenRaidsDone.Done(3), $"not after {i} raids");
                GameSession.Progress.Journal.AddRaid(new RaidRecord { Raid = "Misty Woods", Outcome = i == 1 ? "Fallen" : "Extracted" });
            }

            Assert.IsTrue(WhenRaidsDone.Done(3), "three raids, however they ended");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void EveryUnlitStone_ShowsTheWay_WhileTheQuestAsks()
    {
        var stone = (GameObject)PrefabUtility.InstantiatePrefab(Settings.StonePrefab);
        try
        {
            stone.GetComponent<BoundaryStone>().Configure(Region);
            var waypoint = stone.GetComponent<QuestWaypoint>();
            Assert.IsNotNull(waypoint);
            Assert.AreEqual(Settings.UnlockedBy, waypoint.Quest);
            StringAssert.Contains(Region, waypoint.Label);
        }
        finally
        {
            Object.DestroyImmediate(stone);
        }
    }

    [Test]
    public void AVisitor_IsntPointedAtTheHostsStones()
    {
        GameSession.StartNewGame();
        var stone = (GameObject)PrefabUtility.InstantiatePrefab(Settings.StonePrefab);
        try
        {
            GameSession.Quests.TryStart(Settings.UnlockedBy);
            var boundary = stone.GetComponent<BoundaryStone>();
            typeof(BoundaryStone).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boundary, null);
            boundary.Configure(Region);
            Assert.IsTrue(stone.GetComponent<QuestWaypoint>().enabled, "an unlit stone of one's own shows the way");

            Visiting(true);
            boundary.Configure(Region);
            Assert.IsFalse(stone.GetComponent<QuestWaypoint>().enabled, "the host's stones aren't the visitor's to light");
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
            Object.DestroyImmediate(stone);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void AFriendsWork_CountsForTheHostsQuests()
    {
        GameSession.StartNewGame();
        var heard = new System.Collections.Generic.List<GameEventDefinition>();
        System.Action<GameEvent> listen = e =>
        {
            if (e.Kind == GameEventKind.Custom && e.Subject is GameEventDefinition said)
            {
                heard.Add(said);
            }
        };
        GameEvents.Raised += listen;
        try
        {
            GameSession.Here.Territory.Claim(Region, Settings);
            Territory.FromFriend(LandWork.Rune, Region, 0, "Ingrid");
            CollectionAssert.Contains(heard, Settings.RuneOffered);
            TerritorySettings.RegionInfo info = Settings.Info(Region);
            for (int i = 0; i < info.taskCount; i++)
            {
                Territory.FromFriend(LandWork.Task, Region, (int)info.task, "Ingrid");
            }

            CollectionAssert.Contains(heard, Settings.TaskDone);
            CollectionAssert.Contains(heard, Settings.LandSecure, "40 + 25 + 10: a friend's work made it Secure");
        }
        finally
        {
            GameEvents.Raised -= listen;
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void ThePushHasARaidAnswer_AndTheRuneTurnsUp()
    {
        var rumour = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/RotsHeart.asset");
        Assert.IsNotNull(rumour);
        Assert.IsTrue(rumour.ForestPushing);
        var settlement = new SettlementState();
        Assert.IsFalse(rumour.CanComeUp(null, settlement, null), "Only while the forest is pushing.");
        settlement.Territory.Claim(Region, Settings);
        while (settlement.Territory.Incursion == null)
        {
            settlement.Territory.AdvanceTurn(Settings, 0f, Always, out _);
        }

        Assert.IsTrue(rumour.CanComeUp(null, settlement, null));
        StringAssert.Contains(Region, rumour.BriefingFor(settlement));
        Assert.IsTrue(rumour.Site.Choices.Any(c => c.outcomes.Any(o => o is WeakenIncursionOutcome)));
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var system = raid.Systems.OfType<RaidRumoursSystem>().FirstOrDefault();
            // The forest's push is answered in the forest: the Valley and the Marsh are lands of their own (raid identity, 2026-11-17).
            if (system != null && raid.Land == "forest")
            {
                Assert.IsTrue(system.Pool.Contains(rumour), $"{raid.name}'s rumours can't answer the forest's push.");
            }
        }

        bool inACache = AssetDatabase.FindAssets("t:LootTable", new[] { "Assets/_Project/Data/Loot" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(guid)))
            .Any(table => table.Kind != LootTableKind.Creature && table.Drops.Entries.Any(entry => entry.Item == Settings.PactRune));
        Assert.IsTrue(inACache, "Supply caches can hold a Pact Rune (BuildTerritory; a cache builder run since may have dropped it).");
    }

    [Test]
    public void NoTithe_GivesWhatOnlyARaidGives()
    {
        // Creature parts are raid-only (the user's loot answer; GDD §7.7): a region's tithe gives what grows or is made at home. The
        // Wolfwood's Wolf Fang and the Fen's Bog Venom came every dawn (Docs/SystemDesignReview_2026-11-17.md K4).
        foreach (TerritorySettings.RegionInfo region in Settings.Regions)
        {
            foreach (ItemAmount given in region.tithe)
            {
                Assert.IsFalse(given.Item != null && given.Item.RaidOnly, $"{region.region}'s tithe gives {given.Item?.name}, which only a raid gives");
            }
        }
    }
}
