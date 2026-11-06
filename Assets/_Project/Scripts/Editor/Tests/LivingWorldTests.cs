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
    public void Data_CreaturesLive_WolvesFlankAndBreak_GreatBeastsKeepToThemselves()
    {
        MonsterDefinition Of(string name) => AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/" + name + ".asset");
        Assert.Greater(Of("BlightBoar").IdleWanderRadius, 0f);
        Assert.Greater(Of("BlightBoar").IdleGrazeChance, 0f);
        Assert.AreEqual(1f, Of("ForestBittenWolf").FlankSpread);
        Assert.AreEqual(0f, Of("OldMossTroll").IdleWanderRadius, "It walks its own round.");
        Assert.AreEqual(0f, Of("SmallMossTroll").FlankSpread, "It shoots.");
        foreach (MonsterAttack attack in Of("BogSpitter").Attacks)
        {
            if (attack.kind == MonsterAttackKind.Projectile) Assert.Greater(attack.leadTarget, 0f);
            Assert.Less(attack.leadTarget, 0.6f, "A dodge must still work.");
        }

        var wolves = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/WolfPack.prefab").GetComponentInChildren<MonsterPack>(true);
        Assert.IsTrue(new SerializedObject(wolves).FindProperty("breaksWithItsLeader").boolValue);
        var swarm = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/RootlingSwarm.prefab").GetComponentInChildren<MonsterPack>(true);
        Assert.IsFalse(new SerializedObject(swarm).FindProperty("breaksWithItsLeader").boolValue);
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
}
