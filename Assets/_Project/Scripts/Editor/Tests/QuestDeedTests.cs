using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dynamic deed objectives: one objective type per kind of deed (talk to, reach, use, rescue, hire, defend, rank), with
/// the person, place or thing as an asset, so no Quest Event is made for each. They count only their own target, are
/// remembered in the log (and from old saves' event ids), start quests, and start stories.
/// </summary>
public sealed class QuestDeedTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            Object.DestroyImmediate(thing);
        }

        created.Clear();
    }

    [Test]
    public void TalkTo_CountsWhoWasTalkedTo_AndNoOneElse()
    {
        ResidentDefinition sigrid = Person("Sigrid"), bjarne = Person("Bjarne");
        QuestObjective talk = QuestObjective.Deed(ObjectiveType.TalkTo, sigrid);

        Assert.AreEqual(1, talk.Progress(new GameEvent(GameEventKind.Talked, sigrid)));
        Assert.AreEqual(0, talk.Progress(new GameEvent(GameEventKind.Talked, bjarne)), "someone else");
        Assert.AreEqual(0, talk.Progress(new GameEvent(GameEventKind.Reached, sigrid)), "another deed");
        Assert.AreEqual("Talk to Sigrid", talk.Describe());
        Assert.IsNull(talk.FindProblem());
        Assert.IsNotNull(QuestObjective.Deed(ObjectiveType.TalkTo, null).FindProblem(), "no one to talk to");
    }

    [Test]
    public void PlacesAndThings_CountByTarget_ReachingForZones_UsingForInteractions()
    {
        QuestTargetDefinition tower = Target("old-tower", "the Old Tower"), stone = Target("lake-stone", "the Lake Stone", "Touch");
        QuestObjective reach = QuestObjective.Deed(ObjectiveType.ReachPlace, tower);
        QuestObjective use = QuestObjective.Deed(ObjectiveType.UseObject, stone);

        Assert.AreEqual(1, reach.Progress(new GameEvent(GameEventKind.Reached, tower)));
        Assert.AreEqual(0, reach.Progress(new GameEvent(GameEventKind.Reached, stone)));
        Assert.AreEqual(0, reach.Progress(new GameEvent(GameEventKind.Used, tower)), "using it isn't reaching it");
        Assert.AreEqual(1, use.Progress(new GameEvent(GameEventKind.Used, stone)));
        Assert.AreEqual("Reach the Old Tower", reach.Describe());
        Assert.AreEqual("Touch the Lake Stone", use.Describe(), "the target's own verb");

        var trigger = Make<GameEventTrigger>();
        SetEnum(trigger, "when", (int)TriggerWhen.PlayerEntersZone);
        Assert.AreEqual(GameEventKind.Reached, trigger.DeedKind, "a zone counts as reaching");
        SetEnum(trigger, "when", (int)TriggerWhen.PlayerInteracts);
        Assert.AreEqual(GameEventKind.Used, trigger.DeedKind, "an interaction counts as using");
    }

    [Test]
    public void Rescue_FindingAndBringingHome_OnePersonOrAnyone()
    {
        ResidentDefinition ingrid = Person("Ingrid"), hild = Person("Hild");
        QuestObjective find = QuestObjective.Deed(ObjectiveType.RescuePerson, ingrid);
        QuestObjective home = QuestObjective.Deed(ObjectiveType.RescuePerson, ingrid, true);
        QuestObjective anyone = QuestObjective.Deed(ObjectiveType.RescuePerson, null);

        Assert.AreEqual(1, find.Progress(new GameEvent(GameEventKind.PersonFreed, ingrid)));
        Assert.AreEqual(0, find.Progress(new GameEvent(GameEventKind.PersonFreed, hild)), "rescuing someone else isn't finding Ingrid");
        Assert.AreEqual(0, home.Progress(new GameEvent(GameEventKind.PersonFreed, ingrid)), "found isn't home");
        Assert.AreEqual(1, home.Progress(new GameEvent(GameEventKind.PersonRescued, ingrid)));
        Assert.AreEqual(1, anyone.Progress(new GameEvent(GameEventKind.PersonFreed, hild)));
        Assert.AreEqual("Bring Ingrid home", home.Describe());
    }

    [Test]
    public void AssignJob_DefendBreach_AndHavenRank_CountTheirOwn()
    {
        ResidentDefinition ingrid = Person("Ingrid"), tor = Person("Tor");
        var alchemist = Create<JobDefinition>("Alchemist");
        var cook = Create<JobDefinition>("Cook");
        QuestObjective hire = QuestObjective.Create(ObjectiveType.AssignJob, 1, alchemist);
        // Naming nobody, it counts anyone who fills the trade (SettlementState.FillsTrade): the player's own people, and this job is nobody's
        // written trade.
        GameSession.StartNewGame();
        try
        {
            GameSession.Home.AddResident(tor);
            Assert.IsNull(GameSession.Home.Find(ingrid), "the canary: Ingrid isn't home in a new game");
            Assert.AreEqual(1, hire.Progress(new GameEvent(GameEventKind.JobAssigned, alchemist, detail: tor)), "anyone as alchemist");
            Assert.AreEqual(0, hire.Progress(new GameEvent(GameEventKind.JobAssigned, alchemist, detail: ingrid)), "not someone who isn't home");
            Assert.AreEqual(0, hire.Progress(new GameEvent(GameEventKind.JobAssigned, cook, detail: ingrid)));
        }
        finally
        {
            GameSession.StartNewGame();
        }

        var first = Create<WardBreachDefinition>("First");
        var second = Create<WardBreachDefinition>("Second");
        QuestObjective defend = QuestObjective.Create(ObjectiveType.DefendBreach, 1, first);
        Assert.AreEqual(1, defend.Progress(new GameEvent(GameEventKind.BreachRepelled, first)));
        Assert.AreEqual(0, defend.Progress(new GameEvent(GameEventKind.BreachRepelled, second)));

        QuestObjective rank = QuestObjective.HavenRank(2);
        Assert.AreEqual(1, rank.Progress(new GameEvent(GameEventKind.HavenRankReached, null, 3)), "past it counts");
        Assert.AreEqual(0, rank.Progress(new GameEvent(GameEventKind.HavenRankReached, null, 1)));
    }

    [Test]
    public void Deeds_AreRemembered_CountWhenAlreadyDone_AndOldSavesStillCount()
    {
        ResidentDefinition sigrid = Person("Sigrid");
        var log = new QuestLog();
        log.Handle(new GameEvent(GameEventKind.Talked, sigrid));
        Assert.IsTrue(log.HasDone(QuestDeeds.Key(GameEventKind.Talked, sigrid)));
        Assert.IsTrue(log.HappenedEventIds is IEnumerable<string> ids && new List<string>(ids).Contains("talked:sigrid"), "saved with the rest");

        QuestObjective talk = QuestObjective.Deed(ObjectiveType.TalkTo, sigrid);
        SetField(talk, "countAlreadyOwned", true);
        Assert.AreEqual(1, talk.Owned(null, log), "done before the quest: counts, even before the player is there");
        Assert.AreEqual(0, talk.Owned(null, new QuestLog()));

        var old = new QuestLog();
        old.RestoreHappened("prologue.talked_to_sigrid");
        Assert.IsTrue(old.HasDone("talked:expedition.sigrid"), "an old save's Quest Event id stands for the deed");
    }

    [Test]
    public void AQuestStartsWhenThePlayerDoesSomething_AlsoAfterLoading()
    {
        QuestTargetDefinition echo = Target("echo", "the echo");
        var quest = Create<QuestDefinition>("Echoes");
        SetField(quest, "objectives", new List<QuestObjective> { QuestObjective.Create(ObjectiveType.KillMonster, 1) });
        SetField(quest, "start", QuestStart.WhenPlayerDoes);
        SetField(quest, "startWhen", QuestObjective.Deed(ObjectiveType.ReachPlace, echo));
        Assert.IsEmpty(quest.FindProblems());
        var database = Create<ContentDatabase>("content");
        SetField(database, "quests", new List<QuestDefinition> { quest });

        GameSession.StartNewGame();
        var host = new GameObject("Quest Manager");
        QuestManager manager = host.AddComponent<QuestManager>();
        try
        {
            manager.Initialise(database);
            GameEvents.Raise(new GameEvent(GameEventKind.Reached, Target("elsewhere", "elsewhere")));
            Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(quest), "another place");
            GameEvents.Raise(new GameEvent(GameEventKind.Reached, echo));
            Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(quest));

            // A save made after the deed but before the quest began: it starts on loading (the manager re-binds to the new log).
            GameSession.StartNewGame();
            Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(quest));
            manager.Log.RestoreHappened("reached:echo");
            manager.StartQuestsWhoseEventHappened();
            Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(quest));
        }
        finally
        {
            typeof(QuestManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Object.DestroyImmediate(host);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void AStoryStartsWhenThePlayerDoesIt_AndCanCountADeedAsDone()
    {
        ResidentDefinition runa = Person("Runa");
        var when = new WhenPlayerDid { deed = QuestObjective.Deed(ObjectiveType.TalkTo, runa) };
        int fired = 0;
        System.Action stop = when.Listen(null, () => fired++);
        try
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Talked, Person("Tor")));
            Assert.AreEqual(0, fired);
            GameEvents.Raise(new GameEvent(GameEventKind.Talked, runa));
            Assert.AreEqual(1, fired);
            Assert.AreEqual("When Player: Talk to Runa", when.Title);

            var done = new CountAsDoneNode { deed = QuestObjective.Deed(ObjectiveType.TalkTo, runa) };
            Assert.IsNull(done.FindProblem());
            System.Collections.IEnumerator run = done.Run(new StoryContext(null));
            while (run.MoveNext())
            {
            }

            Assert.AreEqual(2, fired, "counting it as done is the deed");
            Assert.IsNotNull(new CountAsDoneNode { deed = QuestObjective.Create(ObjectiveType.KillMonster, 1) }.FindProblem(), "only deeds can be counted");
        }
        finally
        {
            stop();
        }
    }

    [Test]
    public void NoQuestOrSceneStillUsesAPerPersonEvent()
    {
        // The deeds (talked to, reached, used, freed, rescued, hired) replaced a Quest Event per person, place or traveller: none of
        // those may come back under any name of that kind, not only the six the change removed.
        string[] deedLike = { "TalkedTo", "Reached", "Used", "Freed", "Rescued", "Hired", "Traveler", "Traveller" };
        List<string> perPerson = AssetDatabase.FindAssets("t:GameEventDefinition")
            .Select(guid => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(name => deedLike.Any(prefix => name.StartsWith(prefix, System.StringComparison.Ordinal)))
            .ToList();
        CollectionAssert.IsEmpty(perPerson, "count these as deeds (QuestObjective.Deed), not a Quest Event each");
    }

    // ----- helpers

    private ResidentDefinition Person(string name)
    {
        var person = Create<ResidentDefinition>(name);
        SetField(person, "id", name.ToLowerInvariant());
        return person;
    }

    private QuestTargetDefinition Target(string id, string shown, string verb = "Use")
    {
        var target = Create<QuestTargetDefinition>(id);
        target.Configure(id, shown, verb);
        return target;
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }

    private T Make<T>() where T : Component
    {
        var holder = new GameObject(typeof(T).Name);
        created.Add(holder);
        return holder.AddComponent<T>();
    }

    private static void SetEnum(Object target, string field, int value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(info, field);
        info.SetValue(target, value);
    }
}
