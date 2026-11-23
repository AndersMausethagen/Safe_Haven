using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// People worth caring about (Docs/PeopleResearch_2026-11-23.md, built on the user's "implement as recommended"): talents in the player's
/// skills with passions, traits asked for by kind, spirits and inspired days, friends and rivals, the marks raids leave, the kit they
/// fight with, and all of it surviving a save. Tested on the shipped people and settings, with the rules' own numbers read back.
/// </summary>
public sealed class PeopleTests
{
    private static PeopleSettings People => PeopleSettings.Current;
    private static LevelingSettings Leveling => LevelingSettings.Current;

    private static ResidentDefinition Person(string name) => AssetDatabase.FindAssets("t:ResidentDefinition " + name)
        .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(one => one != null && one.name == name);

    private static ProficiencyDefinition Skill(string name) => AssetDatabase.LoadAssetAtPath<ProficiencyDefinition>($"Assets/_Project/Data/Proficiencies/{name}.asset");

    private static IEnumerable<T> All<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
        .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(found => found != null);

    private static MarkDefinition Mark(string id) => People.Marks.First(mark => mark != null && mark.Id == id);

    private static SettlementState Home(params string[] names)
    {
        var home = new SettlementState();
        foreach (string name in names)
        {
            home.AddResident(Person(name));
        }

        return home;
    }

    private static void SetLevel(ResidentRecord record, ProficiencyDefinition skill, int level) =>
        record.Talents.SetExperience(skill.Id, Leveling.TotalExperienceFor(level));

    // ----- Talents and passions -----

    [Test]
    public void WrittenPeople_StartWhereTheirTalentsSay_AndEveryOtherSkillLow()
    {
        int people = 0, checkedSkills = 0;
        foreach (ResidentDefinition who in All<ResidentDefinition>().Where(one => one.Talents.Count > 0))
        {
            people++;
            var record = new ResidentRecord(who);
            ResidentTalents.Seed(record.Talents, who);
            foreach (ProficiencyDefinition skill in Leveling.Skills)
            {
                TalentStart start = who.TalentFor(skill);
                Assert.AreEqual(start.skill != null ? start.level : People.UnlistedLevel, ResidentTalents.LevelOf(record, skill), $"{who.name} {skill.name}");
                Assert.AreEqual(start.skill != null ? start.passion : Passion.None, ResidentTalents.PassionOf(record, skill), $"{who.name} {skill.name}");
                checkedSkills++;
            }

            ProficiencyDefinition job = who.Jobs.Length > 0 ? who.Jobs[0].Skill : null;
            if (job != null)
            {
                Assert.GreaterOrEqual(ResidentTalents.LevelOf(record, job), 15, $"{who.name} came home a professional at {job.name}");
            }
        }

        Assert.GreaterOrEqual(people, 11, "the eleven written people have talents");
        Assert.Greater(checkedSkills, people * 10);
    }

    [Test]
    public void Passion_SpeedsLearning_AndTheDaysPractice_IsCapped()
    {
        ProficiencyDefinition skill = Skill("Fishing");
        var gained = new Dictionary<Passion, float>();
        foreach (Passion passion in new[] { Passion.None, Passion.Keen, Passion.Burning })
        {
            var record = new ResidentRecord(null);
            SetLevel(record, skill, 10);
            record.Talents.SetPassion(skill.Id, passion);
            float before = record.Talents.ExperienceOf(skill.Id);
            ResidentTalents.Practise(record, skill, 5, 20f);
            gained[passion] = record.Talents.ExperienceOf(skill.Id) - before;
        }

        Assert.Greater(gained[Passion.Keen], gained[Passion.None]);
        Assert.Greater(gained[Passion.Burning], gained[Passion.Keen]);
        Assert.AreEqual(People.LearningFor(Passion.Burning) / People.LearningFor(Passion.Keen), gained[Passion.Burning] / gained[Passion.Keen], 0.001f);

        // A day's worth and three times more in one go: only Beyond The Day's share of what is past the cap counts.
        var busy = new ResidentRecord(null);
        SetLevel(busy, skill, 10);
        busy.Talents.SetPassion(skill.Id, Passion.Keen);
        float base3 = People.DailyPractice * 3f * Leveling.PracticeTimeScale / (People.WorkShare * People.LearningFor(Passion.Keen));
        float start = busy.Talents.ExperienceOf(skill.Id);
        ResidentTalents.Practise(busy, skill, 5, base3);
        float expected = People.DailyPractice + People.DailyPractice * 2f * People.BeyondTheDay;
        Assert.AreEqual(expected, busy.Talents.ExperienceOf(skill.Id) - start, 0.5f);
    }

    [Test]
    public void TheJobsSkill_SetsSpeedAndRank_AndAnOldSavesRank_BecomesItsLevel()
    {
        SettlementState home = Home("Bjarne");
        ResidentRecord bjarne = home.Find(Person("Bjarne"));
        home.AssignJob(bjarne.Resident, bjarne.Resident.Jobs[0]);
        ProficiencyDefinition job = ResidentTalents.JobSkill(bjarne);
        Assert.AreEqual(bjarne.Resident.Jobs[0].Skill, job);
        int level = ResidentTalents.LevelOf(bjarne, job);
        Assert.AreEqual(1f + People.SpeedPerLevel * (level - 10), ResidentTalents.StationSpeed(bjarne), 0.0001f);
        Assert.AreEqual(Mathf.Clamp(People.ExtraPerLevel * (level - 10), 0f, People.MostExtra), ResidentTalents.ExtraChance(bjarne), 0.0001f);

        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        var old = new ResidentRecord(bjarne.Resident, bjarne.Job);
        ResidentTalents.FromOldRank(old, ranks[ranks.Count - 1].crafts);
        Assert.AreEqual(ranks[ranks.Count - 1].name, ResidentTalents.RankName(old), "an old save's master is a master still");
    }

    [Test]
    public void AWorkersCraft_TrainsTheirJobsSkill()
    {
        int jobs = 0;
        foreach (ResidentDefinition who in All<ResidentDefinition>().Where(one => one.Jobs.Length > 0 && one.Jobs[0].WorkOrders.Count > 0))
        {
            JobDefinition job = who.Jobs[0];
            RecipeDefinition order = job.WorkOrders.Select(o => o.recipe).FirstOrDefault(r => r != null && r.Output != null);
            if (order == null || job.Skill == null)
            {
                continue;
            }

            jobs++;
            SettlementState home = Home(who.name);
            home.AssignJob(who, job);
            ResidentRecord record = home.Find(who);
            float before = record.Talents.ExperienceOf(job.Skill.Id);
            ResidentTalents.PractiseCraft(record, order);
            Assert.Greater(record.Talents.ExperienceOf(job.Skill.Id), before, $"{who.name}'s {order.name} trains {job.Skill.name}");
        }

        Assert.Greater(jobs, 3, "the trades with work orders");
    }

    [Test]
    public void ABondRank_OwesAPassionPick_FromTwoOffers()
    {
        SettlementState home = Home("Tor");
        ResidentRecord tor = home.Find(Person("Tor"));
        Assert.IsFalse(ResidentTalents.Pick(tor, Skill("Melee"), Passion.Keen), "nothing owed yet");
        ResidentTalents.OwePick(tor, "Trusted");
        List<(ProficiencyDefinition skill, Passion to)> offers = ResidentTalents.PickOffers(tor);
        Assert.AreEqual(2, offers.Count);
        Assert.AreNotEqual(offers[0].skill, offers[1].skill);
        Assert.IsTrue(offers.Any(offer => offer.to == Passion.Burning && ResidentTalents.PassionOf(tor, offer.skill) == Passion.Keen), "a Keen grown to Burning");
        Assert.IsTrue(ResidentTalents.Pick(tor, offers[1].skill, offers[1].to));
        Assert.AreEqual(offers[1].to, ResidentTalents.PassionOf(tor, offers[1].skill));
        Assert.AreEqual(0, tor.Talents.PicksOwed);
    }

    // ----- Traits -----

    [Test]
    public void EveryWrittenPerson_HasTwoOrThreeTraits_NoneExcludingAnother()
    {
        int people = 0;
        foreach (ResidentDefinition who in All<ResidentDefinition>().Where(one => one.Talents.Count > 0))
        {
            people++;
            Assert.That(who.Traits.Count, Is.InRange(2, 3), who.name);
            foreach (ResidentTraitDefinition trait in who.Traits)
            {
                Assert.NotNull(trait, who.name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(trait.Description), trait.name + " says what it means on hover");
                Assert.Greater(trait.Effects.Count, 0, trait.name + " changes something");
                foreach (ResidentTraitDefinition other in who.Traits)
                {
                    Assert.IsFalse(trait.Excludes.Contains(other), $"{who.name}: {trait.name} excludes {other.name}");
                }
            }
        }

        Assert.GreaterOrEqual(people, 11);
    }

    [Test]
    public void Traits_AreAskedForByKind_AndBySkill()
    {
        SettlementState home = Home("Liv", "Gunnar", "Tor");
        ResidentRecord liv = home.Find(Person("Liv"));
        Assert.Greater(ResidentTraits.LearningOf(liv, Skill("Runecraft")), ResidentTraits.LearningOf(liv, Skill("Melee")), "Bookish: Runecraft only");
        ResidentRecord gunnar = home.Find(Person("Gunnar"));
        Assert.Greater(ResidentTraits.ForayShare(gunnar, 1), 0f, "a loner alone");
        Assert.Less(ResidentTraits.ForayShare(gunnar, 4), 0f, "a loner in a crowd");
        ResidentRecord tor = home.Find(Person("Tor"));
        Assert.Less(ResidentTraits.Sum(tor, TraitEffectKind.WorkSpeed), 0f, "Patient Hands is slower");
        Assert.AreEqual(ResidentTraits.Sum(tor, TraitEffectKind.WorkSpeed), ResidentTraits.WorkShare(tor, Skill("Woodcutting")), 0.0001f,
            "an effect for any work counts once at work with a skill");
        Assert.Greater(ResidentTraits.Sum(tor, TraitEffectKind.QualityChance), 0f, "and finer");
        Assert.Greater(ResidentRoutine.HoursOf(tor, SettlementSettings.Current.ResidentHours).WorkFrom, -1f);
        Assert.Less(ResidentRoutine.HoursOf(tor, SettlementSettings.Current.ResidentHours).WorkFrom, SettlementSettings.Current.ResidentHours.WorkFrom, "an early riser");
    }

    [Test]
    public void AForayParty_CountsItsLeader_AndFriendsGoingTogether()
    {
        SettlementState home = Home("Sigrid", "Eirik");
        ResidentRecord sigrid = home.Find(Person("Sigrid"));
        ResidentRecord eirik = home.Find(Person("Eirik"));
        ForaySettings settings = ForaySettings.Current;
        Assert.AreEqual(1, PeopleStandings.LevelOf(home, sigrid, eirik), "written friends start Friendly");
        float alone = Forays.PowerOf(sigrid, settings) + Forays.PowerOf(eirik, settings);
        float together = Forays.PowerOf(new[] { sigrid, eirik }, settings, null, home);
        float led = ResidentTraits.Sum(sigrid, TraitEffectKind.LeadsParty);
        Assert.Greater(led, 0f, "Sigrid leads");
        float expected = (Forays.PowerOf(sigrid, settings) * (1f + ResidentTraits.ForayShare(sigrid, 2))
                          + Forays.PowerOf(eirik, settings) * (1f + ResidentTraits.ForayShare(eirik, 2) + led)) * (1f + People.ForayFriends);
        Assert.AreEqual(expected, together, 0.01f);
        Assert.Greater(together, alone);
    }

    // ----- Spirits and inspired days -----

    [Test]
    public void Spirits_RiseAndFall_WithWhatIs()
    {
        SettlementState home = Home("Tor", "Ingrid");
        ResidentRecord tor = home.Find(Person("Tor"));
        Assert.AreEqual(1, PeopleStandings.KindWordsFor(home, tor), "Ingrid is kind to anyone who doesn't stand badly with her");
        Assert.AreEqual(Spirits.Steady, ResidentSpirits.Of(tor, home));
        tor.Inspiration = Inspiration.WorkFever;
        Assert.AreEqual(Spirits.Bright, ResidentSpirits.Of(tor, home), "kind words and an inspired day");
        tor.Inspiration = Inspiration.None;
        tor.Bruised = true;
        tor.Feel(ResidentSpirits.LostFeeling + "someone", home.DaysPassed + 2);
        Assert.AreEqual(Spirits.Low, ResidentSpirits.Of(tor, home), "bruised and missing a friend");
        Assert.IsTrue(ResidentSpirits.ReasonsOf(tor, home).Any(reason => reason.Steps < 0 && reason.Icon == "Skull"));
    }

    [Test]
    public void ABrightDawn_MayInspire_AndADaysInspirationEnds()
    {
        SettlementState home = Home("Tor", "Ingrid", "Yrsa");
        ResidentRecord tor = home.Find(Person("Tor"));
        ResidentRecord yrsa = home.Find(Person("Yrsa"));
        tor.Inspiration = Inspiration.Story;
        tor.InspiredOn = home.DaysPassed;
        List<ResidentRecord> inspired = Inspirations.Dawn(home, () => 0f);
        Assert.AreEqual(Inspiration.None, tor.Inspiration, "a story lasts the day");
        foreach (ResidentRecord record in inspired)
        {
            Assert.AreNotEqual(Inspiration.None, record.Inspiration);
        }

        Assert.IsFalse(inspired.Contains(yrsa) && ResidentSpirits.Of(yrsa, home) != Spirits.Bright, "only the bright");
        Assert.AreEqual(0, Inspirations.Dawn(new SettlementState(), () => 0f).Count);

        // Yrsa burns for Cooking (meals, not gear): her inspirations are never a finer piece of gear.
        for (float pick = 0f; pick < 1f; pick += 0.05f)
        {
            Assert.AreNotEqual(Inspiration.Craft, Inspirations.Choose(yrsa, pick), $"at {pick}");
        }

        SettlementState smithy = Home("Bjarne");
        ResidentRecord bjarne = smithy.Find(Person("Bjarne"));
        Assert.AreNotEqual(Inspiration.Craft, Inspirations.Choose(bjarne, 0f), "no bench of his own yet: no idea for it");
        smithy.AssignJob(bjarne.Resident, bjarne.Resident.Jobs[0]);
        Assert.AreEqual(Inspiration.Craft, Inspirations.Choose(bjarne, 0f), "the smith burns for Weaponcrafting");
    }

    [Test]
    public void AnInspiredSmithsPiece_IsAStepFiner_AndSigned()
    {
        ResidentRecord bjarne = Home("Bjarne").Find(Person("Bjarne"));
        RecipeDefinition recipe = All<RecipeDefinition>().First(r => r.Output != null && r.Output.HasDurability && CraftedWork.Of(r) == CraftedGoods.Weapons);
        Assert.IsNull(ResidentTalents.WorkerPiece(bjarne, recipe, 0.9999f), "a plain piece leaves nothing waiting");
        bjarne.Inspiration = Inspiration.Craft;
        string piece = ResidentTalents.WorkerPiece(bjarne, recipe, 0.9999f);
        Assert.AreEqual((int)ItemQuality.Fine + "|" + bjarne.Resident.DisplayName + "|" + recipe.Output.Id, piece);
        Assert.AreEqual(Inspiration.None, bjarne.Inspiration, "used");

        var queue = new StationQueue("probe", CraftingStationType.Anvil, 1, 1);
        queue.Lifts.Add(piece);
        ItemDefinition other = All<ItemDefinition>().First(item => item.HasDurability && item != recipe.Output);
        Assert.AreEqual(ItemQuality.Plain, ResidentTalents.Lift(queue, new ItemStack(other, 1)).Quality, "another item is left as it is");
        Assert.AreEqual(1, queue.Lifts.Count);
        ItemStack lifted = ResidentTalents.Lift(queue, new ItemStack(recipe.Output, 1));
        Assert.AreEqual(ItemQuality.Fine, lifted.Quality);
        Assert.AreEqual(bjarne.Resident.DisplayName, lifted.Maker);
        Assert.AreEqual(0, queue.Lifts.Count);
    }

    // ----- Friends and rivals -----

    [Test]
    public void FriendsAndFrictions_StartWritten_RivalsArgueEitherWay_AndTheLostAreMissed()
    {
        SettlementState home = Home("Sigrid", "Eirik", "Gunnar");
        ResidentRecord sigrid = home.Find(Person("Sigrid"));
        ResidentRecord gunnar = home.Find(Person("Gunnar"));
        Assert.AreEqual(-1, PeopleStandings.LevelOf(home, sigrid, gunnar), "written friction starts Cool");
        float before = home.Standings.ValueOf(sigrid.Resident.Id, gunnar.Resident.Id);
        Assert.IsTrue(PeopleStandings.Argue(home, sigrid, gunnar, 0f));
        Assert.Greater(home.Standings.ValueOf(sigrid.Resident.Id, gunnar.Resident.Id), before, "cleared the air");
        float cleared = home.Standings.ValueOf(sigrid.Resident.Id, gunnar.Resident.Id);
        Assert.IsFalse(PeopleStandings.Argue(home, sigrid, gunnar, 0.9999f));
        Assert.Less(home.Standings.ValueOf(sigrid.Resident.Id, gunnar.Resident.Id), cleared, "hardened");

        ResidentDefinition eirik = Person("Eirik");
        home.Find(eirik).Talents.SetExperience(Skill("Melee").Id, 12345f);
        home.LoseResident(eirik, "probe");
        Assert.AreEqual(eirik, PeopleStandings.Misses(home, sigrid), "his friend misses him");
        Assert.IsTrue(home.FindAgain(eirik));
        Assert.IsNull(PeopleStandings.Misses(home, sigrid));
        ResidentRecord found = home.Find(eirik);
        Assert.AreEqual(12345f, found.Talents.ExperienceOf(Skill("Melee").Id), 0.01f, "who he is came back with him");
        Assert.IsTrue(found.HasMark(People.ForestMark.Id), "the forest's mark");
        Assert.IsFalse(ResidentMarks.CanHeal(found, People.ForestMark) && found.Forgotten.Count > 0);
    }

    // ----- Marks -----

    [Test]
    public void ARaid_LeavesMarksByWhatHappened_AndFearsTurn()
    {
        ResidentRecord eirik = Home("Eirik").Find(Person("Eirik"));
        var trolls = new Dictionary<string, int> { ["bog_troll"] = 3 };
        List<MarkDefinition> came = ResidentMarks.AfterRaid(eirik, new RaidFacts { Extracted = true, KillsBy = trolls }, () => 0.9999f);
        Assert.IsTrue(came.Any(mark => mark.Id == "troll_wary"), "three trolls along");
        Assert.IsFalse(came.Any(mark => !mark.Good), "a clean raid leaves nothing hard");

        ResidentMarks.AfterRaid(eirik, new RaidFacts { Extracted = true, TimesDowned = 1, DownedBy = new List<string> { "bog_troll" } }, () => 0f);
        Assert.IsTrue(eirik.HasMark("old_wound"), "downed");
        Assert.IsFalse(eirik.HasMark("afraid_of_widows"), "a troll downed him: no fear of spiders");
        eirik.RemoveMark("old_wound");
        ResidentMarks.AfterRaid(eirik, new RaidFacts { Extracted = true, TimesDowned = 1, DownedBy = new List<string> { "black_widow" } }, () => 0f);
        Assert.IsTrue(eirik.HasMark("afraid_of_widows") && eirik.HasMark("old_wound"), "downed by a widow");
        MarkDefinition afraid = Mark("afraid_of_widows");
        ResidentMarks.AfterRaid(eirik, new RaidFacts { Extracted = true, KillsBy = new Dictionary<string, int> { ["black_widow_small"] = afraid.Count } }, () => 0.9999f);
        Assert.IsFalse(eirik.HasMark(afraid.Id));
        Assert.IsTrue(eirik.HasMark(afraid.TurnsInto.Id), "faced down");

        MarkDefinition wound = Mark("old_wound");
        Assert.AreEqual(wound, ResidentMarks.HealedBy(eirik, wound.HealedBy));
        Assert.IsTrue(ResidentMarks.Heal(eirik, wound));
        Assert.IsFalse(eirik.HasMark(wound.Id));

        var crowded = new ResidentRecord(Person("Eirik"));
        foreach (MarkDefinition mark in People.Marks.Where(mark => mark.Good))
        {
            ResidentMarks.Give(crowded, mark);
        }

        Assert.AreEqual(Mathf.Min(People.MostMarks, People.Marks.Count(mark => mark.Good)), ResidentMarks.Of(crowded).Count, "at most Most Marks good");

        // Earned by deeds once: pushed out by others, Troll-wary doesn't come back every raid after.
        var veteran = new ResidentRecord(Person("Eirik"));
        ResidentMarks.AfterRaid(veteran, new RaidFacts { Extracted = true, KillsBy = trolls }, () => 0.9999f);
        Assert.IsTrue(veteran.HasMark("troll_wary"));
        veteran.RemoveMark("troll_wary");
        ResidentMarks.AfterRaid(veteran, new RaidFacts { Extracted = true }, () => 0.9999f);
        Assert.IsFalse(veteran.HasMark("troll_wary"), "once");

        // The forest's mark holds while memories are lost: three more hard marks don't push it out.
        var found = new ResidentRecord(Person("Eirik"));
        found.Forgotten.Add(CompanionMemory.HardFight);
        ResidentMarks.FoundAgain(found);
        foreach (MarkDefinition mark in People.Marks.Where(mark => !mark.Good && mark != People.ForestMark))
        {
            ResidentMarks.Give(found, mark);
        }

        Assert.IsTrue(found.HasMark(People.ForestMark.Id), "held by the forest");
        Assert.LessOrEqual(ResidentMarks.Of(found).Count(mark => !mark.Good), People.MostMarks);
    }

    [Test]
    public void ACompanionsDodge_CanStillGrow_PastTheirKindsOwn()
    {
        int kinds = 0;
        foreach (CompanionDefinition kind in All<CompanionDefinition>())
        {
            kinds++;
            Assert.Greater(People.MostDodge, kind.ReadsAttacks, kind.name + ": a Wary trait or Melee footwork must still count");
        }

        Assert.Greater(kinds, 0);
    }

    // ----- Kit -----

    [Test]
    public void Kit_SuitsHowTheyFight_ScalesTheirBlow_Wears_AndBecomesTheirs()
    {
        SettlementState home = Home("Runa", "Gunnar");
        ResidentRecord runa = home.Find(Person("Runa"));
        ResidentRecord gunnar = home.Find(Person("Gunnar"));
        List<ItemDefinition> items = All<ItemDefinition>().ToList();
        ItemDefinition bow = items.First(item => item.IsWeapon && item.Weapon.Kind == WeaponKind.Bow && item.HasDurability);
        ItemDefinition blade = items.Where(item => item.IsWeapon && item.Weapon.Kind == WeaponKind.Melee && item.HasDurability).OrderByDescending(item => item.Weapon.Damage).First();
        ItemDefinition body = items.First(item => item.IsArmor && item.ArmorSlot == ArmorSlot.Chest && item.HasDurability);
        Assert.IsTrue(CompanionKit.Fits(runa, bow, true));
        Assert.IsFalse(CompanionKit.Fits(runa, blade, true), "an archer takes a bow");
        Assert.IsTrue(CompanionKit.Fits(gunnar, blade, true));
        Assert.IsFalse(CompanionKit.Fits(gunnar, bow, true));
        Assert.IsTrue(CompanionKit.Fits(gunnar, body, false));

        var bag = new ItemContainer(6, 1);
        bag.Add(blade, 1);
        Assert.IsTrue(CompanionKit.Give(gunnar, bag, 0, null, null));
        Assert.AreEqual(blade, gunnar.KitWeapon.Item);
        Assert.IsTrue(bag.GetSlot(0).IsEmpty);
        CompanionDefinition how = gunnar.Resident.RaidCompanion;
        float own = how.Weapon.Weapon.Damage;
        Assert.AreEqual(Mathf.Clamp(blade.Weapon.Damage / own, 0.5f, 3f), CompanionKit.DamageScale(gunnar, how), 0.0001f);

        for (int raid = 0; raid < People.KitNamedAfter; raid++)
        {
            Assert.IsFalse(CompanionKit.IsTheirs(gunnar, true));
            CompanionKit.AfterRaid(gunnar, true);
        }

        Assert.IsTrue(CompanionKit.IsTheirs(gunnar, true));
        StringAssert.StartsWith(gunnar.Resident.DisplayName + "'s", CompanionKit.NameOf(gunnar, true));
        Assert.Less(gunnar.KitWeapon.Durability, 1f, "worn by the raids");
        Assert.IsTrue(CompanionKit.TakeBack(gunnar, true, bag, null, null));
        Assert.IsTrue(gunnar.KitWeapon.IsEmpty);
        Assert.AreEqual(blade, bag.GetSlot(0).Item);
        Assert.IsTrue(CompanionKit.Give(gunnar, bag, 0, null, null), "mended and given back");
        Assert.IsTrue(CompanionKit.IsTheirs(gunnar, true), "taking it back to mend cost nothing of its raids");
    }

    // ----- A save -----

    [Test]
    public void WhoTheyAre_SurvivesASave_AndAnOldSaveKeepsItsRank()
    {
        SettlementState home = Home("Gunnar", "Sigrid");
        ResidentDefinition gunnarDef = Person("Gunnar");
        ResidentRecord gunnar = home.Find(gunnarDef);
        gunnar.Talents.SetExperience(Skill("Archery").Id, 4321f);
        gunnar.Talents.SetPassion(Skill("Mining").Id, Passion.Keen);
        gunnar.Talents.SetPicksOwed(1);
        gunnar.AddMark("old_wound");
        gunnar.AddDeed("kills:bog_troll", 2);
        gunnar.Feel("lost:someone", 7);
        gunnar.Inspiration = Inspiration.GoodFind;
        gunnar.InspiredOn = 3;
        ItemDefinition blade = All<ItemDefinition>().First(item => item.IsWeapon && item.Weapon.Kind == WeaponKind.Melee && item.HasDurability);
        gunnar.KitWeapon = new ItemStack(blade, 1).WithDurability(0.5f);
        gunnar.AddDeed("kit:" + blade.Id, 4);
        PeopleStandings.Change(home, gunnar, home.Find(Person("Sigrid")), 0.7f);
        float standing = home.Standings.ValueOf(gunnarDef.Id, Person("Sigrid").Id);

        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, settlement: home);
        SettlementState read = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content).Settlement;
        ResidentRecord back = read.Find(gunnarDef);
        Assert.AreEqual(4321f, back.Talents.ExperienceOf(Skill("Archery").Id), 0.01f);
        Assert.AreEqual(Passion.Keen, back.Talents.PassionOf(Skill("Mining").Id));
        Assert.AreEqual(1, back.Talents.PicksOwed);
        Assert.IsTrue(back.HasMark("old_wound"));
        Assert.AreEqual(2, back.DeedOf("kills:bog_troll"));
        Assert.AreEqual(7, back.FeelingUntil("lost:someone"));
        Assert.AreEqual(Inspiration.GoodFind, back.Inspiration);
        Assert.AreEqual(3, back.InspiredOn);
        Assert.AreEqual(blade, back.KitWeapon.Item);
        Assert.AreEqual(0.5f, back.KitWeapon.Durability, 0.001f);
        Assert.AreEqual(4, CompanionKit.RaidsWith(back, true));
        Assert.AreEqual(standing, read.Standings.ValueOf(gunnarDef.Id, Person("Sigrid").Id), 0.0001f);

        // Lost with all of it, and saved while lost: it waits with them.
        home.LoseResident(gunnarDef, "probe");
        data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, settlement: home);
        read = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content).Settlement;
        FallenRecord lost = read.Fallen.First(one => one.Resident == gunnarDef);
        Assert.AreEqual(4321f, lost.Talents.ExperienceOf(Skill("Archery").Id), 0.01f);
        Assert.AreEqual(blade, lost.KitWeapon.Item);
        Assert.Contains("old_wound", lost.Marks);
        Assert.AreEqual(4, lost.Deeds["kit:" + blade.Id], "the kit's raids wait with them");

        // A save from before talents: the old rank, by crafts, becomes the job skill's level.
        var smithy = new SettlementState();
        ResidentDefinition bjarneDef = Person("Bjarne");
        smithy.AddResident(bjarneDef, bjarneDef.Jobs[0]);
        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        smithy.Find(bjarneDef).WorkDone = ranks[ranks.Count - 1].crafts;
        data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, settlement: smithy);
        data.settlement.residents[0].talents = new TalentSaveData();
        ResidentRecord bjarne = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content).Settlement.Find(bjarneDef);
        Assert.AreEqual(ranks[ranks.Count - 1].name, ResidentTalents.RankName(bjarne));
    }

    // ----- The second review's fixes -----

    [Test]
    public void SpiritsCountedForWork_MatchTheReasonsShown()
    {
        SettlementState home = Home("Tor", "Ingrid", "Gunnar");
        ResidentRecord tor = home.Find(Person("Tor"));
        ResidentRecord gunnar = home.Find(Person("Gunnar"));
        var states = new List<Action>
        {
            () => { },
            () => tor.Bruised = true,
            () => tor.Inspiration = Inspiration.Story,
            () => tor.Feel(ResidentSpirits.LostFeeling + "someone", home.DaysPassed + 1),
            () => tor.AddMark("old_wound"),
            () => home.AssignJob(tor.Resident, tor.Resident.Jobs[0]),
        };
        int checkedStates = 0;
        foreach (Action change in states)
        {
            change();
            foreach (ResidentRecord record in new[] { tor, gunnar })
            {
                int shown = ResidentSpirits.ReasonsOf(record, home).Sum(reason => reason.Steps);
                Assert.AreEqual(shown, ResidentSpirits.StepsOf(record, home), $"{record.Resident.name}, state {checkedStates}");
            }

            checkedStates++;
        }

        Assert.AreEqual(states.Count, checkedStates);
    }

    [Test]
    public void AFriendWhoCameWhileTheyWereLost_IsAFriendOnceTheyAreFound()
    {
        SettlementState home = Home("Eirik");
        ResidentDefinition eirik = Person("Eirik");
        home.LoseResident(eirik, "probe");
        home.AddResident(Person("Sigrid"));
        Assert.IsTrue(home.FindAgain(eirik));
        Assert.AreEqual(1, PeopleStandings.LevelOf(home, home.Find(eirik), home.Find(Person("Sigrid"))), "written friends, though one was lost");
    }

    [Test]
    public void EarningAFearsOpposite_PutsTheFearAway()
    {
        ResidentRecord eirik = Home("Eirik").Find(Person("Eirik"));
        MarkDefinition afraid = Mark("afraid_of_widows");
        Assert.IsTrue(ResidentMarks.Give(eirik, afraid));
        Assert.IsTrue(ResidentMarks.Give(eirik, afraid.TurnsInto));
        Assert.IsFalse(eirik.HasMark(afraid.Id), "not afraid of what they have learned to fell");
    }

    [Test]
    public void ABondRanksPick_IsPaidOnce_EvenFoundAgain()
    {
        ResidentRecord tor = Home("Tor").Find(Person("Tor"));
        ResidentTalents.OwePick(tor, "Trusted");
        ResidentTalents.OwePick(tor, "Trusted");
        Assert.AreEqual(1, tor.Talents.PicksOwed);
        ResidentTalents.OwePick(tor, "Sworn");
        Assert.AreEqual(2, tor.Talents.PicksOwed);
    }

    [Test]
    public void AnOldSavesRank_IsNeverSlowerThanItWas()
    {
        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        ResidentDefinition who = All<ResidentDefinition>().First(one => one.Jobs.Length > 0 && one.Jobs[0].Skill != null);
        int checkedRanks = 0;
        foreach (SettlementSettings.WorkerRank rank in ranks)
        {
            var old = new ResidentRecord(who, who.Jobs[0]);
            ResidentTalents.FromOldRank(old, rank.crafts);
            Assert.GreaterOrEqual(ResidentTalents.StationSpeed(old), rank.speed - 0.0001f, rank.name);
            Assert.AreEqual(rank.name, ResidentTalents.RankName(old), "and keeps its name");
            checkedRanks++;
        }

        Assert.Greater(checkedRanks, 1);
    }
}
