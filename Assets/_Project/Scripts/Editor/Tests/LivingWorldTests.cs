using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The rules behind how creatures live and close in, how companions fight and speak, how residents spend the day, and how the year and the morning turn.</summary>
public sealed class LivingWorldTests
{
    // ----- Creatures -----

    [Test]
    public void Flanking_PartsThoseOnTheSameLine_AndLeavesALoneOneAlone()
    {
        Vector3 target = Vector3.zero;
        var self = new Vector3(0f, 0f, 8f);
        var beside = new Vector3(0.5f, 0f, 8f);
        Assert.AreEqual(target, AttackTurns.FlankSpot(self, target, new List<Vector3>(), 1f, 2f), "Nobody else: straight at it.");
        Assert.AreEqual(target, AttackTurns.FlankSpot(self, target, new List<Vector3> { beside }, 0f, 2f), "A creature that doesn't flank.");

        Vector3 mine = AttackTurns.FlankSpot(self, target, new List<Vector3> { beside }, 1f, 2f);
        Vector3 theirs = AttackTurns.FlankSpot(beside, target, new List<Vector3> { self }, 1f, 2f);
        Assert.Greater(Vector3.Angle(mine, theirs), 60f, "They come at it from different sides.");
        Assert.GreaterOrEqual(mine.magnitude, 2f, "Not closer than it stands off.");
    }

    [Test]
    public void Flanking_LeavesThoseAlreadyApartWhereTheyAre()
    {
        var self = new Vector3(0f, 0f, 8f);
        var opposite = new Vector3(0f, 0f, -8f);
        Vector3 spot = AttackTurns.FlankSpot(self, Vector3.zero, new List<Vector3> { opposite }, 1f, 2f);
        Assert.Less(Vector3.Angle(self, spot), 1f);
    }

    [Test]
    public void IdleLife_SettlesByChance_AndWandersInsideItsRadius()
    {
        Assert.AreEqual(MonsterIdleLife.Doing.Sleeping, MonsterIdleLife.Settle(0.05f, 0.5f, 0.1f));
        Assert.AreEqual(MonsterIdleLife.Doing.Grazing, MonsterIdleLife.Settle(0.3f, 0.5f, 0.1f));
        Assert.AreEqual(MonsterIdleLife.Doing.Standing, MonsterIdleLife.Settle(0.9f, 0.5f, 0.1f));
        var home = new Vector3(3f, 1f, -2f);
        for (int i = 0; i <= 10; i++)
        {
            Vector3 point = MonsterIdleLife.WanderPoint(home, 8f, i / 10f, (10 - i) / 10f);
            Assert.LessOrEqual(Vector3.Distance(point, home), 8.001f);
        }
    }

    [Test]
    public void Packs_BreakWhenTheyAreMeantTo()
    {
        Assert.IsTrue(MonsterPack.Breaks(2, 3, leaderFell: true, breaksWithLeader: true, atLosses: 0f));
        Assert.IsFalse(MonsterPack.Breaks(2, 3, leaderFell: true, breaksWithLeader: false, atLosses: 0f), "A swarm has no nerve to lose.");
        Assert.IsFalse(MonsterPack.Breaks(3, 4, leaderFell: false, breaksWithLeader: true, atLosses: 0.6f));
        Assert.IsTrue(MonsterPack.Breaks(1, 4, leaderFell: false, breaksWithLeader: true, atLosses: 0.6f));
        Assert.IsFalse(MonsterPack.Breaks(0, 4, leaderFell: true, breaksWithLeader: true, atLosses: 0.6f), "Nobody left to flee.");
    }

    [Test]
    public void EveryCreaturesAim_LeavesADodgeWorking()
    {
        // How far ahead each creature aims, wanders or flanks is its own tuning; that no aim leads so far a dodge stops working is
        // the rule, for every creature.
        int attacks = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (MonsterAttack attack in monster.Attacks)
            {
                attacks++;
                Assert.Less(attack.leadTarget, 0.6f, $"{monster.name}: a dodge must still work");
            }
        }

        Assert.Greater(attacks, 0, "the creatures' attacks were found");
    }

    // ----- Companions -----

    [Test]
    public void Formation_TurnsWithTheWayTheLeaderIsHeading()
    {
        var behind = new Vector3(2f, 0f, -3f);
        Vector3 north = CompanionTactics.FormationPoint(Vector3.zero, Vector3.forward, behind);
        Assert.AreEqual(-3f, north.z, 0.001f, "Heading north, behind is south.");
        Vector3 west = CompanionTactics.FormationPoint(Vector3.zero, Vector3.left, behind);
        Assert.AreEqual(3f, west.x, 0.001f, "Heading west, behind is east.");
        Assert.AreEqual(2f, west.z, 0.001f, "...and right of them is north.");
    }

    [Test]
    public void Targets_ALeaveOneSomeoneElseHas_WhenThereIsAnother()
    {
        var threats = new List<CompanionThreat>
        {
            new CompanionThreat(new Vector3(2f, 0f, 0f), true, false, takenBy: 2),
            new CompanionThreat(new Vector3(0f, 0f, 4f), true, false, takenBy: 0),
        };
        var situation = new CompanionSituation { Order = CompanionOrder.Follow, Self = Vector3.zero, Leader = Vector3.zero, FollowPoint = Vector3.zero, LeaderTarget = -1, Threats = threats };
        var ranges = new CompanionRanges(9f, 3.5f, 6f, 10f, 1.5f);
        Assert.AreEqual(1, CompanionTactics.Decide(situation, ranges).Target);
    }

    [Test]
    public void Blows_AreSeenAndSteppedOutOf()
    {
        Vector3 from = Vector3.zero;
        Assert.IsTrue(CompanionTactics.InTheWay(new Vector3(0f, 0f, 2f), from, Vector3.forward, 3f, 90f));
        Assert.IsFalse(CompanionTactics.InTheWay(new Vector3(0f, 0f, 5f), from, Vector3.forward, 3f, 90f), "Out of reach.");
        Assert.IsFalse(CompanionTactics.InTheWay(new Vector3(2.5f, 0f, -0.5f), from, Vector3.forward, 3f, 90f), "Beside it.");
        var self = new Vector3(0.4f, 0f, 2f);
        Vector3 step = CompanionTactics.StepOutOfTheWay(self, from, Vector3.forward);
        Assert.Greater(step.x, self.x, "The short way out of the line.");
        Assert.IsFalse(CompanionTactics.InTheWay(step, from, Vector3.forward, 3f, 40f));
    }

    [Test]
    public void Archers_BackOff_FindALine_AndCloseIn()
    {
        Vector3 enemy = Vector3.zero;
        Vector3? back = CompanionTactics.ArcherStep(new Vector3(0f, 0f, 2f), enemy, 5f, 14f, true, 1f);
        Assert.Greater(back.Value.z, 2f, "Too close: it backs away.");
        Assert.IsNull(CompanionTactics.ArcherStep(new Vector3(0f, 0f, 9f), enemy, 5f, 14f, true, 1f), "In range and clear: it stands and shoots.");
        Vector3? round = CompanionTactics.ArcherStep(new Vector3(0f, 0f, 9f), enemy, 5f, 14f, false, 1f);
        Assert.Greater(Mathf.Abs(round.Value.x), 1f, "Blocked: it steps sideways.");
        Assert.AreEqual(enemy, CompanionTactics.ArcherStep(new Vector3(0f, 0f, 20f), enemy, 5f, 14f, true, 1f).Value, "Out of range: it closes in.");
    }

    [Test]
    public void FallingBehind_Hurries_ThenComesUpFromBehind()
    {
        Assert.AreEqual(CompanionTactics.CatchUp.Keeping, CompanionTactics.HowToCatchUp(5f, 14f, 45f, 0f, 5f));
        Assert.AreEqual(CompanionTactics.CatchUp.Hurrying, CompanionTactics.HowToCatchUp(20f, 14f, 45f, 0f, 5f));
        Assert.AreEqual(CompanionTactics.CatchUp.Warp, CompanionTactics.HowToCatchUp(50f, 14f, 45f, 0f, 5f));
        Assert.AreEqual(CompanionTactics.CatchUp.Warp, CompanionTactics.HowToCatchUp(10f, 14f, 45f, 6f, 5f), "Stuck, and not at its place.");
        Assert.AreEqual(CompanionTactics.CatchUp.Keeping, CompanionTactics.HowToCatchUp(2f, 14f, 45f, 6f, 5f), "Standing at its place isn't stuck.");
    }

    [Test]
    public void Barks_SpeakOnce_ForEachMoment()
    {
        var barks = new CompanionBarks();
        Assert.AreEqual(CompanionBarks.Moment.Spotted, barks.Think(true, false, 1f, 10f));
        Assert.AreEqual(CompanionBarks.Moment.None, barks.Think(true, false, 1f, 11f));
        Assert.AreEqual(CompanionBarks.Moment.BehindYou, barks.Think(true, true, 1f, 12f), "A warning comes first.");
        Assert.AreEqual(CompanionBarks.Moment.Hurt, barks.Think(true, false, 0.2f, 13f));
        Assert.AreEqual(CompanionBarks.Moment.None, barks.Think(true, false, 0.2f, 14f), "Once.");
        Assert.AreEqual(CompanionBarks.Moment.None, barks.Think(false, false, 0.2f, 15f), "Not over until it has stayed over.");
        Assert.AreEqual(CompanionBarks.Moment.Won, barks.Think(false, false, 0.2f, 17f));
        Assert.AreEqual(CompanionBarks.Moment.None, barks.Think(false, false, 0.2f, 30f));
    }

    [Test]
    public void Barks_TakeThePersonsLineFirst_ThenTheirKinds()
    {
        var own = new FightLines { spotted = new[] { "Mine." } };
        var kind = new FightLines { spotted = new[] { "Kind." }, won = new[] { "Done." } };
        Assert.AreEqual("Mine.", CompanionBarks.LinesFor(CompanionBarks.Moment.Spotted, own, kind)[0]);
        Assert.AreEqual("Done.", CompanionBarks.LinesFor(CompanionBarks.Moment.Won, own, kind)[0]);
        Assert.IsFalse(CompanionBarks.MaySpeak(CompanionBarks.Moment.Spotted, 100f, someoneIsSpeaking: true), "Nobody talks over someone.");
        Assert.IsFalse(CompanionBarks.MaySpeak(CompanionBarks.Moment.None, 100f, false));
    }

    [Test]
    public void Data_ThePartyHasSomethingToSay()
    {
        foreach (string who in new[] { "Sigrid", "Eirik", "Runa" })
        {
            FightLines lines = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/" + who + ".asset").FightLines;
            Assert.Greater(lines.spotted.Length, 0, who);
            Assert.Greater(lines.behindYou.Length, 0, who);
        }
    }

    // ----- Residents -----

    private static readonly ResidentHours Hours = new ResidentHours(6.5f, 8f, 12f, 13f, 18f, 22f);

    [TestCase(3f, ResidentActivity.Sleep)]
    [TestCase(7f, ResidentActivity.Fire)]
    [TestCase(9f, ResidentActivity.Work)]
    [TestCase(12.5f, ResidentActivity.Meal)]
    [TestCase(15f, ResidentActivity.Work)]
    [TestCase(19f, ResidentActivity.Fire)]
    [TestCase(23f, ResidentActivity.Sleep)]
    public void Day_GoesByTheClock(float hour, ResidentActivity expected)
    {
        Assert.AreEqual(expected, ResidentRoutine.Decide(hour, Hours, hasWorkplace: true, danger: false));
    }

    [Test]
    public void Day_DangerComesFirst_AndNoWorkplaceMeansTheFire()
    {
        Assert.AreEqual(ResidentActivity.Shelter, ResidentRoutine.Decide(3f, Hours, true, true), "Woken by it.");
        Assert.AreEqual(ResidentActivity.Fire, ResidentRoutine.Decide(9f, Hours, false, false));
    }

    [Test]
    public void Data_JobsHaveHandsToUse_AndTheHavenHasAFire()
    {
        var smith = AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("Smith t:JobDefinition")[0]));
        Assert.NotNull(smith.WorkGesture);
        Assert.NotNull(SettlementSettings.Current.ResidentGatheringPlace);
        Assert.NotNull(SettlementSettings.Current.ResidentSleepGesture);
        Assert.Greater(SettlementSettings.Current.ResidentFireGestures.Length, 0);
    }

    // ----- The world round the Haven -----

    [Test]
    public void MorningMist_RisesBeforeDawnAndBurnsOffByMidMorning()
    {
        Assert.AreEqual(0f, AtmosphereConditions.MorningAt(1f), 0.001f, "No morning mist in the dead of night.");
        Assert.AreEqual(1f, AtmosphereConditions.MorningAt(6f), 0.001f, "Thickest about first light.");
        Assert.Greater(AtmosphereConditions.MorningAt(4.5f), 0f, "Rising before dawn.");
        Assert.AreEqual(0f, AtmosphereConditions.MorningAt(11f), 0.001f, "Burnt off by mid-morning.");
        Assert.AreEqual(0f, AtmosphereConditions.MorningAt(15f), 0.001f);
    }

    [Test]
    public void SeasonCycle_HoldsEachSeasonThenTurnsIntoTheNextAndWrapsTheYear()
    {
        var cycle = ScriptableObject.CreateInstance<SeasonCycle>();
        try
        {
            cycle.Configure(new List<SeasonCycle.Season>
            {
                new SeasonCycle.Season { name = "Summer", days = 10 },
                new SeasonCycle.Season { name = "Autumn", days = 6, autumn = 1f, leafFall = 0.2f },
                new SeasonCycle.Season { name = "Winter", days = 4, autumn = 1f, leafFall = 1f, frost = 1f },
            }, 2);

            cycle.At(3f, out SeasonCycle.Season season, out SeasonCycle.Season next, out float turned);
            Assert.AreEqual("Summer", season.name);
            Assert.AreEqual("Autumn", next.name);
            Assert.AreEqual(0f, turned, 0.001f, "Mid-season does not turn yet.");
            Assert.AreEqual(Vector3.zero, cycle.LookAt(3f));

            Vector3 turning = cycle.LookAt(9f);
            Assert.That(turning.x, Is.GreaterThan(0f).And.LessThan(1f), "The last days blend into the next season.");

            Assert.AreEqual(new Vector3(1f, 0.2f, 0f), cycle.LookAt(12f));
            cycle.At(17f, out season, out next, out _);
            Assert.AreEqual("Winter", season.name);
            Assert.AreEqual("Summer", next.name, "Winter turns back into summer.");
            Assert.AreEqual(cycle.LookAt(3f), cycle.LookAt(23f), "The year wraps round after 20 days.");
        }
        finally
        {
            Object.DestroyImmediate(cycle);
        }
    }

    [Test]
    public void SeasonCycle_WithoutSeasonsLooksLikeSummer()
    {
        var cycle = ScriptableObject.CreateInstance<SeasonCycle>();
        try
        {
            Assert.AreEqual(Vector3.zero, cycle.LookAt(5f));
        }
        finally
        {
            Object.DestroyImmediate(cycle);
        }
    }

    [Test]
    public void SeasonCycle_AssetRunsSummerAutumnWinterSpring()
    {
        var cycle = AssetDatabase.LoadAssetAtPath<SeasonCycle>("Assets/_Project/Data/World/Season Cycle.asset");
        Assert.IsNotNull(cycle, "BuildLivingWorld makes the Season Cycle asset.");
        Assert.GreaterOrEqual(cycle.Seasons.Count, 4);
        Assert.AreEqual(0f, cycle.Seasons[0].frost, "The year begins in a frostless summer.");
        float winter = 0f;
        foreach (SeasonCycle.Season season in cycle.Seasons)
        {
            winter = Mathf.Max(winter, season.frost);
        }

        Assert.Greater(winter, 0.5f, "Some season frosts the leaves.");
    }

    [Test]
    public void GroundFrost_WinterWhitensAllDay_ALightFrostLiesInTheMorningOnly()
    {
        Assert.AreEqual(0f, Seasons.GroundFrost(0f, 6f, 1f), "No frost in summer, mist or not.");
        Assert.AreEqual(1f, Seasons.GroundFrost(0.75f, 6f, 1f), 0.001f, "Winter: the ground is white at dawn.");
        Assert.Greater(Seasons.GroundFrost(0.75f, 14f, 0f), 0.8f, "...and stays white through the day.");
        float dawn = Seasons.GroundFrost(0.15f, 6f, 1f), afternoon = Seasons.GroundFrost(0.15f, 14f, 0f);
        Assert.Greater(dawn, 0.3f, "Late autumn: patches of frost at dawn.");
        Assert.Less(afternoon, dawn * 0.5f, "...mostly gone by the afternoon.");
    }

    [Test]
    public void Snow_BuildsWhileItFalls_LiesThroughWinter_AndThawsAfter()
    {
        float snow = 0f;
        for (int i = 0; i < 600; i++)
        {
            snow = WeatherSystem.AdvanceSnow(snow, 1f, 0.6f, true, 1f, 100f, 400f);
        }

        Assert.AreEqual(1f, snow, 0.001f, "A heavy snowfall covers the ground.");
        float after = WeatherSystem.AdvanceSnow(snow, 0f, 0.6f, true, 60f, 100f, 400f);
        Assert.That(after, Is.LessThan(1f).And.GreaterThan(0.95f), "Once it stops it lingers, settling slowly.");
        Assert.AreEqual(0.6f, WeatherSystem.AdvanceSnow(0.6f, 0f, 0.6f, true, 3600f, 100f, 400f), 0.001f, "Never below the winter's own layer.");
        Assert.AreEqual(0f, WeatherSystem.AdvanceSnow(1f, 0f, 0f, false, 400f, 100f, 400f), 0.001f, "Out of winter it thaws away.");
    }

    [Test]
    public void SeasonCycle_OnlyWinterSnows_AndItsLayerComesAndGoesWithTheTurn()
    {
        var cycle = AssetDatabase.LoadAssetAtPath<SeasonCycle>("Assets/_Project/Data/World/Season Cycle.asset");
        cycle.SnowAt(5f, out bool snows, out float lying);
        Assert.IsFalse(snows, "Summer rains.");
        Assert.AreEqual(0f, lying);
        float winterStart = 0f;
        SeasonCycle.Season winter = null;
        foreach (SeasonCycle.Season season in cycle.Seasons)
        {
            if (season.snows)
            {
                winter = season;
                break;
            }

            winterStart += season.days;
        }

        Assert.IsNotNull(winter, "Some season snows.");
        cycle.SnowAt(winterStart + 1f, out snows, out lying);
        Assert.IsTrue(snows);
        Assert.AreEqual(winter.lyingSnow, lying, 0.001f);
        Assert.Greater(lying, 0f, "Snow lies through the winter.");
    }

    [Test]
    public void Weather_SnowFallsInsteadOfRain_AndDoesNotMakePuddles()
    {
        var go = new GameObject("Snow weather test");
        try
        {
            var set = WeatherSetup.EnsureAssets();
            var weather = go.AddComponent<WeatherSystem>();
            weather.ChangesByItself = false;
            weather.SetWeather(set.Storm, 0f);
            weather.SetSnowfall(true, 0.6f);
            float wetBefore = weather.Wetness;
            for (int i = 0; i < 400; i++)
            {
                weather.Tick(1f);
            }

            Assert.AreEqual(1f, weather.Snow, 0.01f, "A storm in winter buries the ground in snow.");
            Assert.Less(weather.Wetness, Mathf.Max(0.05f, wetBefore), "...and leaves no puddles.");
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_SafeHavenSnow"), 0.01f);
            Assert.IsNotNull(set.RainPrefab.transform.Find("Snowflakes"), "The rain prefab has its snowflakes.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ----- The year in play -----

    [Test]
    public void Cold_ChillsSlowlyOutside_LessInFurs_AndAFireWarmsQuickly()
    {
        float exposure = 0f;
        for (int i = 0; i < 300; i++) exposure = PlayerWarmth.Advance(exposure, 1f, 0f, false, 1f);
        Assert.GreaterOrEqual(exposure, 1f, "Five minutes out in the full cold chills.");
        float furs = 0f;
        for (int i = 0; i < 300; i++) furs = PlayerWarmth.Advance(furs, 1f, 0.5f, false, 1f);
        Assert.Less(furs, 1f, "Frost resistance halves it.");
        float warmed = exposure;
        for (int i = 0; i < 30; i++) warmed = PlayerWarmth.Advance(warmed, 1f, 0f, true, 1f);
        Assert.AreEqual(0f, warmed, 0.001f, "Half a minute at a fire.");
        Assert.Less(PlayerWarmth.Advance(0.5f, 0f, 0f, false, 10f), 0.5f, "A mild day eases it.");
        Assert.AreEqual(0f, ColdBreath.Coldness(0f, 0f, 1f), "Summer: no breath, no cold.");
        Assert.Greater(ColdBreath.Coldness(0.6f, 0.75f, 0f), 0.8f, "Winter.");
    }

    [Test]
    public void Residents_KeepToTheFireInTheCold_AndSometimesGoOut()
    {
        Assert.AreEqual(ResidentActivity.Work, ResidentRoutine.InTheCold(ResidentActivity.Work, 15.5f, 0.2f), "A mild day.");
        Assert.AreEqual(ResidentActivity.Fire, ResidentRoutine.InTheCold(ResidentActivity.Work, 15.5f, 0.6f), "A cold afternoon.");
        Assert.AreEqual(ResidentActivity.Work, ResidentRoutine.InTheCold(ResidentActivity.Work, 11f, 0.9f), "Even a bitter day has some work.");
        Assert.AreEqual(ResidentActivity.Fire, ResidentRoutine.InTheCold(ResidentActivity.Work, 8.5f, 0.9f));
        Assert.AreEqual(ResidentActivity.Sleep, ResidentRoutine.InTheCold(ResidentActivity.Sleep, 15.5f, 1f));
        int outings = 0;
        for (int day = 0; day < 300; day++)
        {
            if (ResidentRoutine.Outing("Sigrid", day, out float start, out float hours))
            {
                outings++;
                Assert.That(start, Is.InRange(10f, 14f));
                Assert.That(hours, Is.InRange(0.9f, 1.7f));
            }

            ResidentRoutine.Outing("Sigrid", day, out float again, out _);
            ResidentRoutine.Outing("Sigrid", day, out float again2, out _);
            Assert.AreEqual(again, again2, "The same answer however often it is asked.");
        }

        Assert.That(outings, Is.InRange(60, 140), "About one day in three.");
    }

    [Test]
    public void SeasonalNodes_OnlyGrowInTheirSeasons_ButASpotIsNeverEmpty()
    {
        var berries = ScriptableObject.CreateInstance<GatheringNodeDefinition>();
        var sticks = ScriptableObject.CreateInstance<GatheringNodeDefinition>();
        try
        {
            berries.ConfigureSeasons(new List<string> { "Summer", "Autumn" });
            Assert.IsTrue(berries.InSeason("summer"));
            Assert.IsFalse(berries.InSeason("Winter"));
            Assert.IsTrue(berries.InSeason(null), "Raids have no season.");
            Assert.IsTrue(sticks.InSeason("Winter"), "No seasons: all year.");
        }
        finally
        {
            Object.DestroyImmediate(berries);
            Object.DestroyImmediate(sticks);
        }

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var picked = new List<GatheringNodeDefinition>();
        RandomResourceNode.Collect(content.GatheringNodes, ResourceNodeKind.Forage, new Vector2Int(0, 1), picked, false, "Winter");
        Assert.IsNotEmpty(picked);
        Assert.IsFalse(picked.Exists(node => node.name == "Berries" || node.name == "Chanterelles"), "No berries or mushrooms in winter.");
        RandomResourceNode.Collect(content.GatheringNodes, ResourceNodeKind.Forage, new Vector2Int(0, 1), picked, false, "Autumn");
        Assert.IsTrue(picked.Exists(node => node.name == "Chanterelles"), "Mushrooms in autumn.");
    }

    [Test]
    public void Festivals_FallOnTheirDayAndHours()
    {
        var feast = AssetDatabase.LoadAssetAtPath<FestivalDefinition>("Assets/_Project/Data/World/Festivals/Harvest Feast.asset");
        var midwinter = AssetDatabase.LoadAssetAtPath<FestivalDefinition>("Assets/_Project/Data/World/Festivals/Midwinter Bonfire.asset");
        Assert.IsNotNull(feast);
        Assert.IsNotNull(midwinter);
        var year = new List<FestivalDefinition> { feast, midwinter };
        Assert.AreEqual(feast, Festivals.On(year, feast.DayOfYear, 19f));
        Assert.IsNull(Festivals.On(year, feast.DayOfYear, 12f), "Not before evening.");
        Assert.IsNull(Festivals.On(year, feast.DayOfYear + 1, 19f));
        Assert.AreEqual(midwinter, Festivals.On(year, midwinter.DayOfYear, 22f));
        Assert.Greater(feast.Boon.Count, 0);
        Assert.Greater(feast.Lines.Count, 0);
        Assert.IsNotNull(feast.Music);
        var cycle = AssetDatabase.LoadAssetAtPath<SeasonCycle>("Assets/_Project/Data/World/Season Cycle.asset");
        cycle.At(midwinter.DayOfYear + 0.5f, out SeasonCycle.Season season, out _, out _);
        Assert.AreEqual("Winter", season.name, "Midwinter falls in winter.");
        cycle.At(feast.DayOfYear + 0.5f, out season, out _, out _);
        Assert.AreEqual("Autumn", season.name, "The harvest feast ends autumn.");
    }

    [Test]
    public void Huldra_RemembersBeingLetBe_AndBeingChased()
    {
        Assert.AreEqual((1, 0, false), SpiritBonds.AfterHuldra(0, 0, false, 5, 2, 3));
        Assert.AreEqual((0, 0, true), SpiritBonds.AfterHuldra(1, 0, false, 5, 2, 3), "The second time: a gift.");
        Assert.AreEqual((0, 8, false), SpiritBonds.AfterHuldra(1, 0, true, 5, 2, 3), "Chased: away three days, respect gone.");
    }

    [Test]
    public void Winter_IceSetsAndThaws_PrintsLastInSnow_AndTrailsLeadOut()
    {
        Assert.AreEqual(0.5f, WinterIce.Advance(0f, true, 30f, 60f), 0.001f);
        Assert.AreEqual(0f, WinterIce.Advance(0.2f, false, 30f, 60f), 0.001f);
        Assert.AreEqual(1f, TerrainFootprints.SnowLifetime(0f, 0f), 0.001f, "No snow: as usual.");
        Assert.AreEqual(5f, TerrainFootprints.SnowLifetime(0.8f, 0f), 0.001f, "Lying snow keeps them.");
        Assert.Less(TerrainFootprints.SnowLifetime(0.8f, 1f), 1f, "A heavy fall fills them in.");
        Vector3 den = new Vector3(100f, 0f, 100f);
        List<(Vector3 at, Vector3 forward)> trail = WinterTracks.Trail(den, Vector3.zero, 40f, 0.8f);
        Assert.That(trail.Count, Is.InRange(48, 52));
        Assert.Less(trail[trail.Count - 1].at.magnitude, den.magnitude, "It leads out towards home.");
        Assert.AreEqual(trail[10].at, WinterTracks.Trail(den, Vector3.zero, 40f, 0.8f)[10].at, "The same trail each visit.");
    }

    [Test]
    public void TheYear_FirefliesInSummer_RiversHighInSpring_GeeseInAV()
    {
        var cycle = AssetDatabase.LoadAssetAtPath<SeasonCycle>("Assets/_Project/Data/World/Season Cycle.asset");
        // The middle of each season by its name, so the test follows the cycle's own lengths.
        float MiddleOf(string season)
        {
            float start = 0f;
            foreach (SeasonCycle.Season each in cycle.Seasons)
            {
                if (each.name == season)
                {
                    return start + each.days * 0.5f;
                }

                start += each.days;
            }

            Assert.Fail("no " + season + " in the cycle");
            return 0f;
        }

        Assert.AreEqual(1f, cycle.FirefliesAt(MiddleOf("Summer")), 0.001f, "Fireflies in summer.");
        Assert.AreEqual(0f, cycle.FirefliesAt(MiddleOf("Winter")), 0.001f, "None in winter.");
        Assert.Greater(cycle.RiverRiseAt(MiddleOf("Spring")), 0.3f, "The thaw.");
        Assert.AreEqual(0f, cycle.RiverRiseAt(MiddleOf("Summer")), 0.001f);
        Vector3 heading = Vector3.forward;
        Assert.AreEqual(Vector3.zero, MigratingGeese.PlaceInSkein(0, heading));
        Vector3 left = MigratingGeese.PlaceInSkein(1, heading), right = MigratingGeese.PlaceInSkein(2, heading);
        Assert.Less(left.z, 0f, "Behind the leader.");
        Assert.AreEqual(-left.x, right.x, 0.001f, "Either side.");
    }

    [Test]
    public void Blight_SpreadsAndEases_AndBringsTheHollowedNearer()
    {
        var settlement = new SettlementState();
        settlement.ChangeBlight(0.5f);
        settlement.PassDay();
        Assert.AreEqual(0.5f - SettlementState.BlightEasedPerDay, settlement.Blight, 0.001f);
        settlement.ChangeBlight(5f);
        Assert.AreEqual(1f, settlement.Blight);
        var entry = new GlimpseWatcher.Entry { beyondPortal = 250f, weight = 1f, moreWithBlight = 3f };
        Assert.AreEqual(4f, entry.WeightNow(1f), 0.001f);
        Assert.AreEqual(100f, entry.BeyondNow(1f), 0.001f);
        Assert.AreEqual(250f, new GlimpseWatcher.Entry { beyondPortal = 250f }.BeyondNow(1f), "Others keep their distance.");
    }
}
