using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Residents' talents (Docs/PeopleResearch_2026-11-23.md §3.1-3.2): a level in each of the player's own skills, on the player's curve
/// (LevelingSettings), learned by doing their work and faster in what they love; their job's skill sets how fast their station works,
/// how often it gives one more, and their rank's name (Novice … Master, SettlementSettings › Worker Ranks by level). The rules, plain
/// and testable (ResidentTalentsTests); the numbers are PeopleSettings'.
/// </summary>
public static class ResidentTalents
{
    /// <summary>Their written start (ResidentDefinition › Talents; every other skill at PeopleSettings › Unlisted Level) in every skill
    /// their sheet doesn't hold yet: all of them at first; after a save or the forest, only a skill added since.</summary>
    public static void Seed(TalentSheet sheet, ResidentDefinition who, LevelingSettings rules = null)
    {
        if (sheet == null || who == null)
        {
            return;
        }

        rules ??= LevelingSettings.Current;
        PeopleSettings settings = PeopleSettings.Current;
        foreach (ProficiencyDefinition skill in rules.Skills)
        {
            if (skill == null || sheet.Experience.ContainsKey(skill.Id))
            {
                continue;
            }

            TalentStart start = who.TalentFor(skill);
            int level = start.skill != null ? start.level : settings.UnlistedLevel;
            sheet.SetExperience(skill.Id, rules.TotalExperienceFor(Mathf.Clamp(level, 1, rules.MaxLevel)));
            if (start.skill != null && start.passion != Passion.None)
            {
                sheet.SetPassion(skill.Id, start.passion);
            }
        }
    }

    /// <summary>
    /// A save from before talents: their job's rank, earned by crafts (Worker Ranks' Crafts), becomes that rank's level in the job's
    /// skill, if that is more than they were written with. Once, as they load.
    /// </summary>
    public static void FromOldRank(ResidentRecord record, int workDone)
    {
        ProficiencyDefinition skill = JobSkill(record);
        if (skill == null || workDone <= 0)
        {
            return;
        }

        SettlementSettings ranks = SettlementSettings.Current;
        int index = 0;
        for (int i = 0; i < ranks.WorkerRanks.Count; i++)
        {
            if (workDone >= ranks.WorkerRanks[i].crafts)
            {
                index = i;
            }
        }

        // At least the rank's level, and enough for the speed the rank gave before talents (never slower than before).
        int level = ranks.WorkerRanks.Count > 0 ? ranks.WorkerRanks[index].level : 1;
        float perLevel = PeopleSettings.Current.SpeedPerLevel;
        if (ranks.WorkerRanks.Count > 0 && perLevel > 0f)
        {
            level = Mathf.Max(level, 10 + Mathf.CeilToInt((ranks.WorkerRanks[index].speed - 1f) / perLevel - 0.0001f));
        }

        LevelingSettings rules = LevelingSettings.Current;
        float needed = rules.TotalExperienceFor(Mathf.Clamp(level, 1, rules.MaxLevel));
        if (record.Talents.ExperienceOf(skill.Id) < needed)
        {
            record.Talents.SetExperience(skill.Id, needed);
        }
    }

    public static int LevelOf(TalentSheet sheet, ProficiencyDefinition skill) =>
        sheet == null || skill == null ? 1 : LevelingSettings.Current.LevelFor(Mathf.FloorToInt(sheet.ExperienceOf(skill.Id)));

    public static int LevelOf(ResidentRecord record, ProficiencyDefinition skill) => LevelOf(record?.Talents, skill);

    public static Passion PassionOf(ResidentRecord record, ProficiencyDefinition skill) =>
        record == null || skill == null ? Passion.None : record.Talents.PassionOf(skill.Id);

    /// <summary>Progress through their present level in a skill, 0 to 1.</summary>
    public static float LevelShare(ResidentRecord record, ProficiencyDefinition skill)
    {
        LevelingSettings rules = LevelingSettings.Current;
        int level = LevelOf(record, skill);
        if (level >= rules.MaxLevel)
        {
            return 1f;
        }

        float from = rules.TotalExperienceFor(level);
        float to = rules.TotalExperienceFor(level + 1);
        return to > from ? Mathf.Clamp01((record.Talents.ExperienceOf(skill.Id) - from) / (to - from)) : 1f;
    }

    /// <summary>The skill their job is done with (JobDefinition › Skill); with no job, or a job without one (the Leader), their best.</summary>
    public static ProficiencyDefinition JobSkill(ResidentRecord record)
    {
        if (record == null)
        {
            return null;
        }

        return record.Job != null && record.Job.Skill != null ? record.Job.Skill : Best(record);
    }

    /// <summary>The skill they are best at (the first of the skills' list on a tie).</summary>
    public static ProficiencyDefinition Best(ResidentRecord record)
    {
        ProficiencyDefinition best = null;
        int bestLevel = 0;
        IReadOnlyList<ProficiencyDefinition> skills = LevelingSettings.Current.Skills;
        for (int i = 0; i < skills.Count; i++)
        {
            ProficiencyDefinition skill = skills[i];
            int level = skill != null ? LevelOf(record, skill) : 0;
            if (level > bestLevel)
            {
                best = skill;
                bestLevel = level;
            }
        }

        return best;
    }

    /// <summary>Every skill, best first (the window's list), the skills' own order on a tie.</summary>
    public static List<ProficiencyDefinition> BestFirst(ResidentRecord record)
    {
        var skills = new List<ProficiencyDefinition>();
        foreach (ProficiencyDefinition skill in LevelingSettings.Current.Skills)
        {
            if (skill != null)
            {
                skills.Add(skill);
            }
        }

        var order = new Dictionary<ProficiencyDefinition, int>();
        for (int i = 0; i < skills.Count; i++)
        {
            order[skills[i]] = i;
        }

        skills.Sort((a, b) =>
        {
            int byLevel = LevelOf(record, b).CompareTo(LevelOf(record, a));
            return byLevel != 0 ? byLevel : order[a].CompareTo(order[b]);
        });
        return skills;
    }

    /// <summary>The level of their job's skill (their best, with none).</summary>
    public static int JobLevel(ResidentRecord record) => LevelOf(record, JobSkill(record));

    /// <summary>Which Worker Rank (SettlementSettings) a level of the job's skill has reached: the highest whose Level it has.</summary>
    public static int RankIndexAt(int level)
    {
        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        int index = 0;
        for (int i = 0; i < ranks.Count; i++)
        {
            if (level >= ranks[i].level)
            {
                index = i;
            }
        }

        return index;
    }

    public static int RankIndex(ResidentRecord record) => record == null ? 0 : RankIndexAt(JobLevel(record));

    /// <summary>Their rank's name in their job (Novice … Master), by their job skill's level.</summary>
    public static string RankName(ResidentRecord record)
    {
        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        return ranks.Count > 0 ? ranks[RankIndex(record)].name : "";
    }

    /// <summary>The rank a level of any skill would be (a skill's band, for the window).</summary>
    public static string RankNameAt(int level)
    {
        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        return ranks.Count > 0 ? ranks[RankIndexAt(level)].name : "";
    }

    /// <summary>How much faster their station works for their job's skill: x1 at level 10, more above, never below Least Speed.</summary>
    public static float StationSpeed(ResidentRecord record)
    {
        PeopleSettings settings = PeopleSettings.Current;
        return Mathf.Max(settings.LeastSpeed, 1f + settings.SpeedPerLevel * (JobLevel(record) - 10));
    }

    /// <summary>The same for work done with <paramref name="skill"/> (a ring's felling, a scaffold): x1 at level 10.</summary>
    public static float SpeedAt(ResidentRecord record, ProficiencyDefinition skill)
    {
        PeopleSettings settings = PeopleSettings.Current;
        return Mathf.Max(settings.LeastSpeed, 1f + settings.SpeedPerLevel * (LevelOf(record, skill) - 10));
    }

    /// <summary>The chance their craft gives one more, for their job's skill.</summary>
    public static float ExtraChance(ResidentRecord record)
    {
        PeopleSettings settings = PeopleSettings.Current;
        return Mathf.Clamp(settings.ExtraPerLevel * (JobLevel(record) - 10), 0f, settings.MostExtra);
    }

    /// <summary>A little quicker at work in a skill they love (PeopleSettings › Passions); 0 otherwise.</summary>
    public static float Joy(ResidentRecord record, ProficiencyDefinition skill) => PeopleSettings.Current.JoyOf(PassionOf(record, skill));

    /// <summary>
    /// Practice in <paramref name="skill"/> at work of <paramref name="tier"/>: <paramref name="baseAmount"/> experience, less when they
    /// already know more than the work asks (as the player's: LevelingSettings.Multiplier), times their passion's learning, their
    /// traits' and Work Share; beyond the day's practice in it, only a share. Raises their rank's message when their job's skill rises
    /// into a new one. Returns the levels gained.
    /// </summary>
    public static int Practise(ResidentRecord record, ProficiencyDefinition skill, int tier, float baseAmount, bool atHome = true)
    {
        if (record == null || skill == null || baseAmount <= 0f)
        {
            return 0;
        }

        LevelingSettings rules = LevelingSettings.Current;
        PeopleSettings settings = PeopleSettings.Current;
        int before = LevelOf(record, skill);
        int rankBefore = RankIndex(record);
        float amount = baseAmount * rules.Multiplier(Mathf.Max(1, tier), before, false) * settings.WorkShare
                       * settings.LearningFor(PassionOf(record, skill)) * ResidentTraits.LearningOf(record, skill) * (atHome ? Mentored(record, skill, before) : 1f)
                       / Mathf.Max(0.01f, rules.PracticeTimeScale);
        int day = GameSession.Home != null ? GameSession.Home.DaysPassed : 0;
        amount = Capped(record.Talents, skill.Id, day, amount, settings);
        record.Talents.SetExperience(skill.Id, record.Talents.ExperienceOf(skill.Id) + amount);
        int after = LevelOf(record, skill);
        if (after > before && record.Resident != null)
        {
            PlaytestLogger.Note(PlaytestLog.PersonLevel, record.Resident.Id, after, skill.Id);
        }

        if (after > before && skill == JobSkill(record) && RankIndex(record) > rankBefore && record.Job != null)
        {
            PlaytestLogger.Note(PlaytestLog.PersonRank, record.Resident.Id, after, RankName(record));
            PeopleNews.Tell($"{record.Resident.DisplayName} is now a {RankName(record)} {record.Job.DisplayName} ({skill.DisplayName} {after}).", MessageKind.Gain);
            // For quests (QuestObjective › Resident Rank): only the player's own people, never a copy of a friend's.
            if (PeopleBond.IsOwn(record))
            {
                GameEvents.Raise(new GameEvent(GameEventKind.PersonRanked, record.Resident, RankIndex(record)));
            }
        }

        return after - before;
    }

    /// <summary>A Friend at home at least Mentor Gap levels better in <paramref name="skill"/>: they learn it Mentor Share faster
    /// (masters teach apprentices: Docs/PeopleSystemReview_2026-11-23.md §1.4). 1 without one.</summary>
    public static float Mentored(ResidentRecord record, ProficiencyDefinition skill, int level, IHavenView home = null)
    {
        home ??= GameSession.Home;
        if (home == null || record?.Resident == null || skill == null)
        {
            return 1f;
        }

        PeopleSettings settings = PeopleSettings.Current;
        IReadOnlyList<ResidentRecord> people = home.Residents;
        for (int i = 0; i < people.Count; i++)
        {
            ResidentRecord other = people[i];
            if (other != record && !other.IsAway && PeopleStandings.LevelOf(home, record, other) >= 2
                && LevelOf(other, skill) >= level + settings.MentorGap)
            {
                return 1f + settings.MentorShare;
            }
        }

        return 1f;
    }

    // The daily cap: what is past the day's practice counts only Beyond The Day's share.
    private static float Capped(TalentSheet sheet, string skillId, int day, float amount, PeopleSettings settings)
    {
        float done = sheet.PractisedToday(skillId, day);
        float room = Mathf.Max(0f, settings.DailyPractice - done);
        float counted = Mathf.Min(amount, room) + Mathf.Max(0f, amount - room) * settings.BeyondTheDay;
        sheet.NotePractised(skillId, day, amount);
        return counted;
    }

    /// <summary>A craft made at their station: practice in their job's skill (the smith's ingots train Weaponcrafting, the carpenter's
    /// planks Woodcutting: the station's own work, as the old ranks rose with it), at the craft's tier and kind of work.</summary>
    public static void PractiseCraft(ResidentRecord record, RecipeDefinition recipe)
    {
        if (record == null || recipe == null || recipe.Output == null)
        {
            return;
        }

        LevelingSettings rules = LevelingSettings.Current;
        CraftedGoods goods = CraftedWork.Of(recipe);
        ProficiencyDefinition skill = JobSkill(record) ?? rules.SkillCrafting(goods);
        int tier = Mathf.Max(1, recipe.Output.Tier);
        Practise(record, skill, tier, rules.TierExperience(tier) * Mathf.Max(0.5f, rules.CraftWork(goods)));
    }

    /// <summary>Work for the job that isn't a craft (an inscription, a quest's reward): Work Unit Practice each, at its tier.</summary>
    public static void PractiseWork(ResidentRecord record, int units, int tier = 1)
    {
        if (record == null || units <= 0)
        {
            return;
        }

        LevelingSettings rules = LevelingSettings.Current;
        Practise(record, JobSkill(record), tier, rules.TierExperience(Mathf.Max(1, tier)) * PeopleSettings.Current.WorkUnitPractice * units);
    }

    /// <summary>The skill they fight with: Archery as an archer, Melee otherwise; <paramref name="how"/> is the kind they fight as now
    /// (a guard at the ward may defend as their job's kind), their raid kind when not given.</summary>
    public static ProficiencyDefinition FightSkill(ResidentRecord record, CompanionDefinition how = null)
    {
        PeopleSettings settings = PeopleSettings.Current;
        how ??= record?.Resident != null ? record.Resident.RaidCompanion : null;
        return how != null && how.IsRanged ? settings.ArcherySkill : settings.MeleeSkill;
    }

    /// <summary>A raid of <paramref name="tier"/> come home from beside the player: practice in their fighting skill, more for foes felled.</summary>
    public static void PractiseRaid(ResidentRecord record, int tier, int kills)
    {
        if (record == null)
        {
            return;
        }

        LevelingSettings rules = LevelingSettings.Current;
        PeopleSettings settings = PeopleSettings.Current;
        float each = rules.TierExperience(Mathf.Max(1, tier));
        Practise(record, FightSkill(record), tier, each * (settings.RaidPractice + settings.KillPractice * Mathf.Max(0, kills)), atHome: false);
    }

    /// <summary>Extra damage beside the player for their fighting skill (a share: 0.15 is +15%).</summary>
    public static float CompanionDamage(ResidentRecord record, CompanionDefinition how = null) =>
        Mathf.Max(0f, PeopleSettings.Current.DamagePerLevel * (LevelOf(record, FightSkill(record, how)) - 10));

    /// <summary>More chance of reading a blow and stepping aside, for their Melee (an archer's too: footwork).</summary>
    public static float CompanionDodge(ResidentRecord record) =>
        Mathf.Max(0f, PeopleSettings.Current.DodgePerLevel * (LevelOf(record, PeopleSettings.Current.MeleeSkill) - 10));

    // ----- Their own hand's quality (§3.1: residents' gear is made as well as their skill makes it, and signed) -----

    /// <summary>
    /// The chance a piece of gear they make is Fine (Masterwork a share of that, as the player's: CraftedWork.QualityFor): their level in
    /// the craft's skill, as the skill's own Quality bonus gives the player at that level, plus their traits' Quality Chance.
    /// </summary>
    public static float QualityChance(ResidentRecord record, ProficiencyDefinition skill)
    {
        if (record == null || skill == null)
        {
            return 0f;
        }

        float chance = 0f;
        int level = LevelOf(record, skill);
        foreach (ProficiencyDefinition.LevelBonus bonus in skill.LevelBonuses)
        {
            if (bonus.stat == StatType.WeaponcraftQuality || bonus.stat == StatType.ArmorcraftQuality)
            {
                chance += ProficiencyDefinition.ValueAt(bonus, level, LevelingSettings.Current);
            }
        }

        return Mathf.Max(0f, chance + ResidentTraits.Sum(record, TraitEffectKind.QualityChance, skill));
    }

    /// <summary>
    /// A piece of gear they finished at their station (<paramref name="roll"/> 0-1): its quality by their hand, a step finer on an
    /// inspired morning (Inspirations › Craft, used up), as "quality|their name" for the station's Lifts; null for a plain piece or
    /// anything that isn't gear.
    /// </summary>
    public static string WorkerPiece(ResidentRecord record, RecipeDefinition recipe, float roll)
    {
        if (record == null || recipe == null || recipe.Output == null || !recipe.Output.HasDurability)
        {
            return null;
        }

        ProficiencyDefinition skill = LevelingSettings.Current.SkillCrafting(CraftedWork.Of(recipe)) ?? JobSkill(record);
        ItemQuality quality = CraftedWork.QualityFor(QualityChance(record, skill), roll, LevelingSettings.Current.MasterworkShare);
        if (Inspirations.Use(record, Inspiration.Craft))
        {
            quality = quality < ItemQuality.Masterwork ? quality + 1 : quality;
            PeopleNews.Tell($"{record.Resident.DisplayName}'s inspired morning at the bench: a {quality} {recipe.Output.DisplayName} waits to be collected.", MessageKind.Gain);
        }

        return quality == ItemQuality.Plain ? null : (int)quality + "|" + record.Resident.DisplayName + "|" + recipe.Output.Id;
    }

    /// <summary>A collected piece of gear from <paramref name="queue"/>: the oldest finer piece its worker made of that same item
    /// ("quality|maker|item id") lifts it (never lower) and signs it with their name; another item is left as it is.</summary>
    public static ItemStack Lift(StationQueue queue, ItemStack piece)
    {
        if (queue == null || queue.Lifts.Count == 0 || piece.IsEmpty)
        {
            return piece;
        }

        for (int i = 0; i < queue.Lifts.Count; i++)
        {
            string[] parts = queue.Lifts[i].Split('|');
            if (parts.Length < 3 || parts[2] != piece.Item.Id)
            {
                continue;
            }

            queue.Lifts.RemoveAt(i);
            if (!int.TryParse(parts[0], out int made) || !System.Enum.IsDefined(typeof(ItemQuality), made))
            {
                return piece;
            }

            ItemQuality quality = (ItemQuality)made;
            return quality > piece.Quality ? piece.WithQuality(quality).WithMaker(parts[1]) : piece;
        }

        return piece;
    }

    // ----- Passions the player helps grow (§3.2: at bond ranks Trusted and Sworn) -----

    /// <summary>A bond rank (<paramref name="rank"/>) reached: one more passion pick is owed them, once a rank (found again and
    /// rising through it a second time pays nothing more).</summary>
    public static void OwePick(ResidentRecord record, string rank)
    {
        string key = "pick:" + rank;
        if (record != null && PeopleSettings.Current.PicksAtBondRanks && record.DeedOf(key) == 0)
        {
            record.AddDeed(key, 1);
            record.Talents.SetPicksOwed(record.Talents.PicksOwed + 1);
        }
    }

    /// <summary>
    /// The two passions offered for a pick: their best Keen skill grown to Burning, and a new Keen in their best skill without a
    /// passion (two new Keens when nothing is Keen yet). Fewer when there is nothing left to grow.
    /// </summary>
    public static List<(ProficiencyDefinition skill, Passion to)> PickOffers(ResidentRecord record)
    {
        var offers = new List<(ProficiencyDefinition, Passion)>();
        if (record == null)
        {
            return offers;
        }

        List<ProficiencyDefinition> order = BestFirst(record);
        ProficiencyDefinition keen = order.Find(skill => PassionOf(record, skill) == Passion.Keen);
        if (keen != null)
        {
            offers.Add((keen, Passion.Burning));
        }

        foreach (ProficiencyDefinition skill in order)
        {
            if (offers.Count >= 2)
            {
                break;
            }

            if (PassionOf(record, skill) == Passion.None)
            {
                offers.Add((skill, Passion.Keen));
            }
        }

        return offers;
    }

    /// <summary>The player's pick: the passion grows, and one pick fewer is owed. False when none is owed or it isn't on offer.</summary>
    public static bool Pick(ResidentRecord record, ProficiencyDefinition skill, Passion to)
    {
        if (record == null || skill == null || record.Talents.PicksOwed <= 0 || !PickOffers(record).Contains((skill, to)))
        {
            return false;
        }

        record.Talents.SetPassion(skill.Id, to);
        record.Talents.SetPicksOwed(record.Talents.PicksOwed - 1);
        PlaytestLogger.Note(PlaytestLog.PassionPick, record.Resident != null ? record.Resident.Id : "", (int)to, skill.Id);
        return true;
    }

    /// <summary>The kit icon a passion is shown with: a heart (at 18-28 px the Flame reads as a drop, beside Fishing's).</summary>
    public const string PassionIcon = "Heart";

    /// <summary>A passion as inline kit icons, one for Keen and two for Burning ("" for none).</summary>
    public static string Flames(Passion passion) =>
        passion == Passion.Burning ? HearthKit.InlineIcon(PassionIcon) + HearthKit.InlineIcon(PassionIcon) : passion == Passion.Keen ? HearthKit.InlineIcon(PassionIcon) : "";
}
