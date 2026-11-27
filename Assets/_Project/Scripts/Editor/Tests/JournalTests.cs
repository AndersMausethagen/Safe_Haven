using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>What the journal shows and hides, and that its record is kept in the save.</summary>
public sealed class JournalTests
{
    private static JournalSettings Settings => AssetDatabase.LoadAssetAtPath<JournalSettings>("Assets/_Project/Resources/JournalSettings.asset");

    [Test]
    public void EveryCreature_IsListed_WithSomethingToSay()
    {
        var listed = Settings.Creatures.ToList();
        // The Lost Ones' bodies are kept in the journal's own page for them (The Lost: each one by name), not as creatures.
        var lost = Resources.Load<LostOneSettings>(LostOneSettings.ResourceName).BodyGrades.SelectMany(grade => grade.bodies).ToList();
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var creature = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (lost.Contains(creature))
            {
                Assert.IsNotEmpty(creature.JournalLore, creature.name);
                continue;
            }

            CollectionAssert.Contains(listed, creature, $"{creature.name} isn't in the journal.");
            Assert.IsNotEmpty(creature.JournalLore, creature.name);
            Assert.IsNotEmpty(creature.JournalAdvice, creature.name);
            Assert.IsNotNull(creature.Portrait, creature.name);
        }
    }

    [Test]
    public void EveryQuest_HasNotes()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("StoryDemo"))
            {
                Assert.IsNotEmpty(AssetDatabase.LoadAssetAtPath<QuestDefinition>(path).JournalNotes, path);
            }
        }
    }

    [Test]
    public void ACreatureNotMet_GivesNothingAway()
    {
        var progress = new CharacterProgress();
        var shelves = JournalContent.Creatures(Settings, progress, out string summary);
        Assert.IsEmpty(shelves[0].Entries, "Nothing has been met yet.");
        foreach (HavenEntry entry in shelves[1].Entries)
        {
            Assert.AreEqual(JournalContent.UnknownName, entry.Name);
            Assert.IsNull(entry.Icon);
            Assert.IsEmpty(entry.Details);
        }

        MonsterDefinition wolf = Settings.Creatures[0];
        progress.NoteMet(wolf.Id);
        shelves = JournalContent.Creatures(Settings, progress, out summary);
        Assert.AreEqual(wolf.DisplayName, shelves[0].Entries[0].Name);
        Assert.IsNotEmpty(shelves[0].Entries[0].Details);
    }

    [Test]
    public void Slaying_MeetsItToo_AndKeepsCount()
    {
        var progress = new CharacterProgress();
        progress.NoteSlain("wolf");
        progress.NoteSlain("wolf");
        progress.NoteMet("wolf");
        Assert.IsTrue(progress.HasMet("wolf"));
        Assert.AreEqual(2, progress.TimesSlain("wolf"), "Meeting it again mustn't reset the count.");
        Assert.AreEqual(0, progress.TimesSlain("troll"));
    }

    [Test]
    public void TheRecord_SurvivesASave()
    {
        var progress = new CharacterProgress();
        progress.NoteMet("seen only");
        progress.NoteSlain("wolf");
        progress.NoteSlain("wolf");
        var saved = new ProgressSaveData();
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(SaveMapper).GetMethod("WriteProgress", Hidden).Invoke(null, new object[] { progress, saved });
        // Through the file format too, as that is where a field goes missing.
        ProgressSaveData read = JsonUtility.FromJson<ProgressSaveData>(JsonUtility.ToJson(saved));
        var loaded = (CharacterProgress)typeof(SaveMapper).GetMethod("ReadProgress", Hidden).Invoke(null, new object[] { read });
        Assert.IsTrue(loaded.HasMet("seen only"));
        Assert.AreEqual(0, loaded.TimesSlain("seen only"));
        Assert.AreEqual(2, loaded.TimesSlain("wolf"));
    }

    [Test]
    public void AnOldSave_HasARecordToo()
    {
        ProgressSaveData read = JsonUtility.FromJson<ProgressSaveData>("{\"level\":3}");
        var loaded = (CharacterProgress)typeof(SaveMapper).GetMethod("ReadProgress", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { read });
        Assert.AreEqual(SaveMapper.LegacyExperience(3, 0), loaded.Insight, "its level comes back as Insight");
        Assert.IsEmpty(loaded.Creatures);
    }

    [Test]
    public void ABackstoryPage_WaitsForItsCreature()
    {
        JournalPage page = Settings.Backstory.First(p => p.unlockedByCreature != null);
        var progress = new CharacterProgress();
        var log = new QuestLog();
        Assert.IsFalse(JournalContent.IsUnlocked(page, log, progress));
        progress.NoteMet(page.unlockedByCreature.Id);
        Assert.IsTrue(JournalContent.IsUnlocked(page, log, progress));
    }

    [Test]
    public void ThePagesAreWritten()
    {
        foreach (JournalPage page in Settings.Backstory.Concat(Settings.Mechanics))
        {
            Assert.IsNotEmpty(page.title);
            Assert.IsNotEmpty(page.group, page.title);
            Assert.IsNotEmpty(page.body, page.title);
        }

        // At the start of a game there is something to read on every written tab.
        var progress = new CharacterProgress();
        var log = new QuestLog();
        Assert.IsNotEmpty(JournalContent.Backstory(Settings, log, progress, out _));
        Assert.IsNotEmpty(JournalContent.Mechanics(Settings, log, progress, out _));
    }

    [Test]
    public void TheJournalHasAKey_ThatCanBeRebound()
    {
        Assert.IsNotNull(GameInput.Journal, "The action is in the Input Actions asset.");
        Assert.IsTrue(GameInput.Journal.bindings.Any(b => b.path == "<Keyboard>/j"));
    }

    [Test]
    public void ACreature_GivesUpMore_AsMoreOfThemFall()
    {
        MonsterDefinition wolf = Settings.Creatures.First(c => c.name == "ForestBittenWolf");
        var progress = new CharacterProgress();
        progress.NoteMet(wolf.Id);
        Assert.AreEqual(0, JournalContent.KnowledgeOf(wolf, progress));
        progress.NoteSlain(wolf.Id);
        Assert.AreEqual(1, JournalContent.KnowledgeOf(wolf, progress));
        for (int i = 1; i < wolf.JournalKillsToKnow; i++)
        {
            progress.NoteSlain(wolf.Id);
        }

        Assert.AreEqual(2, JournalContent.KnowledgeOf(wolf, progress));
        for (int i = 0; i < wolf.JournalKillsToKnow; i++)
        {
            progress.NoteSlain(wolf.Id);
        }

        Assert.AreEqual(3, JournalContent.KnowledgeOf(wolf, progress));
    }

    [Test]
    public void BeingKilled_TeachesHowToLive_ButNotTheRest()
    {
        MonsterDefinition wolf = Settings.Creatures.First(c => c.name == "ForestBittenWolf");
        var progress = new CharacterProgress();
        progress.NoteMet(wolf.Id);
        progress.Journal.NoteKilledBy(wolf.Id);
        Assert.AreEqual(2, JournalContent.KnowledgeOf(wolf, progress));
    }

    [Test]
    public void WhatIsOnlyFoughtOnce_IsKnownAtTheFirstKill()
    {
        MonsterDefinition boss = Settings.Creatures.First(c => c.name == "TheHeartroot");
        var progress = new CharacterProgress();
        progress.NoteSlain(boss.Id);
        Assert.AreEqual(3, JournalContent.KnowledgeOf(boss, progress));
    }

    [Test]
    public void AFoundPage_WaitsToBeFound()
    {
        JournalPage find = Settings.Backstory.First(p => p.foundInRaids);
        var progress = new CharacterProgress();
        var log = new QuestLog();
        Assert.IsFalse(JournalContent.IsUnlocked(find, log, progress));
        progress.Journal.NoteFound(find.title);
        Assert.IsTrue(JournalContent.IsUnlocked(find, log, progress));
    }

    [Test]
    public void AMechanicsPage_WaitsForItsQuest()
    {
        JournalPage page = Settings.Mechanics.First(p => p.title == "Nights and Breaches");
        Assert.IsNotNull(page.unlockedByQuest, "Breaches aren't explained before there are any.");
        var progress = new CharacterProgress();
        var log = new QuestLog();
        Assert.IsFalse(JournalContent.IsUnlocked(page, log, progress));
        Assert.IsTrue(Settings.Mechanics.Any(p => JournalContent.IsUnlocked(p, log, progress)), "The basics are there from the start.");
    }

    [Test]
    public void EveryOneOfTheSeven_HasAPage()
    {
        foreach (string who in new[] { "Sigrid", "Eirik", "Runa", "Bjarne", "Tor", "Liv" })
        {
            Assert.IsTrue(Settings.Backstory.Any(p => p.unlockedByResident != null && p.unlockedByResident.name == who), who);
        }
    }

    [Test]
    public void EveryNoteAboutACreature_SaysWhoAndWhat()
    {
        Assert.IsNotEmpty(Settings.ResidentNotes);
        foreach (ResidentNote note in Settings.ResidentNotes)
        {
            Assert.IsNotNull(note.who);
            Assert.IsNotNull(note.about);
            Assert.IsNotEmpty(note.text);
        }
    }

    [Test]
    public void EveryCreature_SaysWhereItLives()
    {
        foreach (MonsterDefinition creature in Settings.Creatures)
        {
            Assert.IsNotEmpty(creature.JournalHabitat, creature.name);
        }
    }

    [Test]
    public void TheJournalsRecord_SurvivesASave()
    {
        var progress = new CharacterProgress();
        progress.Journal.MarkRead("page:The Pact");
        progress.Journal.Announce("The Pact");
        progress.Journal.NoteFound("A Letter, Unsent");
        progress.Journal.NoteKilledBy("wolf");
        progress.Journal.PinnedQuest = "quest";
        progress.Journal.AddRaid(new RaidRecord { Raid = "Misty Woods", Outcome = "Extracted!", Seconds = 90, Value = 12, Lines = { "Took a chest." } });
        var saved = new ProgressSaveData();
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(SaveMapper).GetMethod("WriteProgress", Hidden).Invoke(null, new object[] { progress, saved });
        ProgressSaveData read = JsonUtility.FromJson<ProgressSaveData>(JsonUtility.ToJson(saved));
        var loaded = (CharacterProgress)typeof(SaveMapper).GetMethod("ReadProgress", Hidden).Invoke(null, new object[] { read });
        Assert.IsTrue(loaded.Journal.HasRead("page:The Pact"));
        Assert.IsFalse(loaded.Journal.Announce("The Pact"), "It has already been told about.");
        Assert.IsTrue(loaded.Journal.HasFound("A Letter, Unsent"));
        Assert.IsTrue(loaded.Journal.WasKilledBy("wolf"));
        Assert.AreEqual("quest", loaded.Journal.PinnedQuest);
        Assert.AreEqual(1, loaded.Journal.Raids.Count);
        Assert.AreEqual("Took a chest.", loaded.Journal.Raids[0].Lines[0]);
        JournalContent.Raids(loaded, out string summary);
        StringAssert.Contains("1 came home", summary);
    }

    [Test]
    public void EveryBody_IsFittedAndCanMove()
    {
        foreach (MonsterDefinition creature in Settings.Creatures)
        {
            // A Marsh Light is only a light.
            if (creature.Prefab.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
                continue;
            }

            Animator animator = creature.Prefab.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, creature.name);
            Assert.IsNotNull(animator.runtimeAnimatorController, $"{creature.name} has no controller: bodies of one model share theirs.");
            // The game animals (Quaternius deer, stag and fox) play generic clips bound by path, which need no avatar.
            if (!creature.Timid)
            {
                Assert.IsNotNull(animator.avatar, creature.name);
            }
        }

        var heartroot = Settings.Creatures.First(c => c.name == "TheHeartroot").Prefab.GetComponent<CapsuleCollider>();
        Assert.Greater(heartroot.radius, 0.9f, "Blows should land on what they appear to.");
        Assert.Greater(heartroot.height, 4f);
    }

    [Test]
    public void TheHeartroot_ShowsItsStages()
    {
        var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/_Project/Data/Monsters/Boss The Heartroot.asset");
        var phases = new SerializedObject(boss).FindProperty("phases");
        Assert.Greater(phases.arraySize, 0);
        for (int i = 0; i < phases.arraySize; i++)
        {
            Assert.Greater(phases.GetArrayElementAtIndex(i).FindPropertyRelative("glow").colorValue.maxColorComponent, 0f);
        }

        Renderer body = Settings.Creatures.First(c => c.name == "TheHeartroot").Prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.IsNotNull(body.sharedMaterial.GetTexture("_EmissionMap"), "The veins can only wake if the material has a map to glow by.");
    }

    [Test]
    public void Eyes_ComeThroughACreaturesLook_AndClose()
    {
        var body = new GameObject("Body");
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var skin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var eyeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = CreatureLook.EyeMaterial + "Test" };
        var skinMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Skin" };
        try
        {
            eye.transform.SetParent(body.transform);
            skin.transform.SetParent(body.transform);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;
            skin.GetComponent<Renderer>().sharedMaterial = skinMaterial;
            eyeMaterial.SetColor("_BaseColor", Color.green);
            skinMaterial.SetColor("_BaseColor", Color.white);
            // Edit mode warns about asking a renderer for its own copies, and gives them all the same.
            LogAssert.ignoreFailingMessages = true;
            CreatureLook.Tint(body, Color.red);
            Assert.AreEqual(Color.green, eye.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor"), "A tint leaves the eyes alone.");
            Assert.AreEqual(Color.red, skin.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor"));
            CreatureLook.CloseEyes(body);
            Assert.AreEqual(Color.black, eye.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor"));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(eyeMaterial);
            Object.DestroyImmediate(skinMaterial);
        }
    }

    private static JournalCinematic Told(string asset) => Settings.Cinematics.First(c => c.cinematic != null && c.cinematic.name == asset);

    private static string[] CinematicsListed(QuestLog log, CharacterProgress progress)
    {
        HavenShelf shelf = JournalContent.Backstory(Settings, log, progress, out _).FirstOrDefault(s => s.Title == JournalContent.CinematicShelf);
        return shelf == null ? new string[0] : shelf.Entries.Select(e => ((CinematicDefinition)e.Key).name).ToArray();
    }

    [Test]
    public void EveryCinematic_CanBeWatchedAgain()
    {
        var listed = Settings.Cinematics.Select(c => c.cinematic).ToList();
        // The start menu's living picture is a cinematic's shot held behind the menu (MenuBackdrop), not a cinematic to watch.
        string menu = System.IO.File.ReadAllText("Assets/_Project/Scenes/MainMenu.unity");
        foreach (string guid in AssetDatabase.FindAssets("t:CinematicDefinition", new[] { "Assets/_Project/Data" })
                     .Where(id => !menu.Contains("backdrop: {fileID: 11400000, guid: " + id)))
        {
            var cinematic = AssetDatabase.LoadAssetAtPath<CinematicDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            CollectionAssert.Contains(listed, cinematic, $"{cinematic.name} can't be watched again.");
            Assert.IsNotEmpty(cinematic.Title, cinematic.name);
            Assert.IsNotNull(cinematic.Poster, $"{cinematic.name} has no picture to show in the list.");
        }

        foreach (JournalCinematic told in Settings.Cinematics)
        {
            Assert.IsNotEmpty(told.line, told.cinematic.name);
        }
    }

    [Test]
    public void ACinematic_IsInTheJournal_OnceTheCharacterHasReachedIt()
    {
        var log = new QuestLog();
        var progress = new CharacterProgress();
        CollectionAssert.AreEqual(new[] { "Backstory" }, CinematicsListed(log, progress), "Every character has seen the opening.");

        var hold = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Prologue/Quests/HoldPortal.asset");
        Assert.AreSame(hold, Told("TheCatch").afterQuest, "The Catch plays as the portal is held.");
        log.Restore(hold, QuestStatus.Active, null);
        CollectionAssert.AreEqual(new[] { "Backstory" }, CinematicsListed(log, progress), "Still holding the portal.");
        log.Restore(hold, QuestStatus.Completed, null);
        CollectionAssert.AreEqual(new[] { "Backstory", "TheCatch" }, CinematicsListed(log, progress));

        RaidDefinition woods = Told("MistyWoods").afterRaid;
        Assert.AreSame(AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset"), woods);
        progress.MarkRaidVisited(woods.Id);
        CollectionAssert.AreEqual(new[] { "Backstory", "TheCatch", "MistyWoods" }, CinematicsListed(log, progress));

        // A save from before raid visits were kept: the raid is in its record.
        var older = new CharacterProgress();
        Assert.IsFalse(JournalContent.IsUnlocked(Told("MistyWoods"), log, older));
        older.Journal.AddRaid(new RaidRecord { Raid = woods.DisplayName, Outcome = "Extracted!" });
        Assert.IsTrue(JournalContent.IsUnlocked(Told("MistyWoods"), log, older));
    }

    [Test]
    public void AWatchButton_PlaysItsCinematic_OrSaysWhyNot()
    {
        CinematicDefinition played = null;
        HavenEntry entry = JournalContent.CinematicEntry(Told("TheCatch"), c => played = c);
        Assert.AreEqual("Watch", entry.Actions[0].Label);
        Assert.IsTrue(entry.Actions[0].Enabled);
        Assert.AreSame(Told("TheCatch").cinematic.Poster, entry.Icon);
        StringAssert.Contains(":", entry.Status, "Its length.");
        entry.Actions[0].Run();
        Assert.AreSame(Told("TheCatch").cinematic, played);

        entry = JournalContent.CinematicEntry(Told("TheCatch"), c => played = c, "not in co-op");
        Assert.IsFalse(entry.Actions[0].Enabled);
        StringAssert.Contains("not in co-op", entry.Actions[0].Label);
        Assert.IsEmpty(JournalContent.CinematicEntry(Told("TheCatch"), null).Actions, "Nothing to press where nothing can play.");
    }

    [Test]
    public void TheArchive_RemembersWhatThisComputerHasSeen()
    {
        const string key = "Cinematics.Seen";
        bool had = PlayerPrefs.HasKey(key);
        string kept = PlayerPrefs.GetString(key, "");
        try
        {
            CinematicArchive.Forget();
            CinematicDefinition opening = Told("Backstory").cinematic;
            Assert.IsFalse(CinematicArchive.HasSeen(opening));
            CinematicArchive.Remember(opening);
            Assert.IsTrue(CinematicArchive.HasSeen(opening));

            // A save that has reached a cinematic fills the record in.
            CinematicArchive.Forget();
            var log = new QuestLog();
            log.Restore(Told("TheCatch").afterQuest, QuestStatus.Completed, null);
            CinematicArchive.RememberReached(Settings, log, new CharacterProgress());
            Assert.IsTrue(CinematicArchive.HasSeen(opening));
            Assert.IsTrue(CinematicArchive.HasSeen(Told("TheCatch").cinematic));
            Assert.IsFalse(CinematicArchive.HasSeen(Told("MistyWoods").cinematic), "Not entered the woods yet.");
        }
        finally
        {
            CinematicArchive.Forget();
            if (had)
            {
                PlayerPrefs.SetString(key, kept);
            }
        }
    }

    [Test]
    public void EveryPrologueCreature_WearsItsBody()
    {
        foreach (string name in new[] { "ExpeditionWolf", "ExpeditionBoar", "ExpeditionMistwing", "ExpeditionSmallMossTroll", "ExpeditionRootling" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Data/Prologue/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            Assert.IsNotNull(prefab.GetComponentInChildren<SkinnedMeshRenderer>(), $"{name} is still built of parts.");
        }
    }
}
