using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// People (Resources/PeopleSettings; Docs/PeopleResearch_2026-11-23.md, Docs/PeopleSystemReview_2026-11-23.md §2.9): each written
/// resident's planned pace in their trade (crafts to their next rank at their first work order's tier, with their passion and traits),
/// beside what the playtest logs measured: levels and ranks reached, inspired mornings, marks, standings and how often the Haven is Bright.
/// </summary>
public sealed class PeoplePage : SystemsPage
{
    private static string measured;
    private static int runsRead;

    public override string Title => "People";
    public override string Group => "Settings";
    public override string Help => "Residents' talents, passions, spirits, standings, marks, kit and bond (Docs/PeopleResearch_2026-11-23.md). " +
                                   "Planned pace counts their trade's first work order, without the daily cap. Measured reads the playtest " +
                                   "logs (runs with cheats left out).";

    public override void Draw()
    {
        PeopleSettings settings = Settings<PeopleSettings>(PeopleSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "People", "Resources/PeopleSettings: talents, passions, spirits, inspired days, standings, marks, kit, bond");

        BeginDerived("Planned pace in their trade (from their written start)");
        LevelingSettings rules = LevelingSettings.Current;
        foreach (ResidentDefinition who in AssetDatabase.FindAssets("t:ResidentDefinition")
                     .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                     .Where(one => one != null && one.Jobs.Length > 0 && one.Jobs[0] != null).OrderBy(one => one.DisplayName))
        {
            Derived(who.DisplayName, Pace(who, who.Jobs[0], rules, settings));
        }

        EndDerived();

        ContentUtility.BeginPanel("Measured in playtests", "Over the runs read: what the people did.");
        if (GUILayout.Button(measured == null ? "Read the playtest logs" : $"Read again ({runsRead} runs read)", GUILayout.Width(260f)))
        {
            measured = Measure(out runsRead);
        }

        EditorGUILayout.LabelField(measured ?? "Not read yet.", EditorStyles.wordWrappedLabel);
        ContentUtility.EndPanel();
    }

    /// <summary>"Smith: Weaponcrafting 24 (Journeyman), x1.21; 38 crafts of Iron Ingot to Expert".</summary>
    public static string Pace(ResidentDefinition who, JobDefinition job, LevelingSettings rules, PeopleSettings settings)
    {
        var record = new ResidentRecord(who, job);
        ResidentTalents.Seed(record.Talents, who, rules);
        ProficiencyDefinition skill = ResidentTalents.JobSkill(record);
        if (skill == null)
        {
            return $"{job.DisplayName}: no skill";
        }

        int level = ResidentTalents.LevelOf(record, skill);
        string now = $"{job.DisplayName}: {skill.DisplayName} {level} ({ResidentTalents.RankName(record)}), x{ResidentTalents.StationSpeed(record):0.00}";
        RecipeDefinition order = job.WorkOrders.Select(o => o.recipe).FirstOrDefault(r => r != null && r.Output != null);
        IReadOnlyList<SettlementSettings.WorkerRank> ranks = SettlementSettings.Current.WorkerRanks;
        int next = ResidentTalents.RankIndex(record) + 1;
        if (order == null || next >= ranks.Count)
        {
            return now;
        }

        CraftedGoods goods = CraftedWork.Of(order);
        int tier = Mathf.Max(1, order.Output.Tier);
        float perCraft = rules.TierExperience(tier) * Mathf.Max(0.5f, rules.CraftWork(goods)) * rules.Multiplier(tier, level, false) * settings.WorkShare
                         * settings.LearningFor(ResidentTalents.PassionOf(record, skill)) * ResidentTraits.LearningOf(record, skill) / Mathf.Max(0.01f, rules.PracticeTimeScale);
        float needed = rules.TotalExperienceFor(ranks[next].level) - record.Talents.ExperienceOf(skill.Id);
        return $"{now}; about {Mathf.CeilToInt(needed / Mathf.Max(0.01f, perCraft))} crafts of {order.Output.DisplayName} to {ranks[next].name}";
    }

    private static string Measure(out int runs)
    {
        runs = 0;
        string folder = PlaytestLog.Folder;
        if (!Directory.Exists(folder))
        {
            return "No playtest logs yet.";
        }

        int levels = 0, ranks = 0, inspired = 0, marksCame = 0, marksHealed = 0, friends = 0, rivals = 0, picks = 0, mornings = 0, brightSum = 0, peopleSum = 0;
        float minutes = 0f;
        var kinds = new Dictionary<string, int>();
        foreach (string file in Directory.GetFiles(folder, "*.csv"))
        {
            List<PlaytestEntry> entries = PlaytestLog.Read(file);
            if (entries.Any(entry => entry.Event == PlaytestLog.Cheat))
            {
                continue;
            }

            runs++;
            minutes += entries.Count > 0 ? entries.Max(entry => entry.PlayedSeconds) / 60f : 0f;
            foreach (PlaytestEntry entry in entries)
            {
                switch (entry.Event)
                {
                    case PlaytestLog.PersonLevel: levels++; break;
                    case PlaytestLog.PersonRank: ranks++; break;
                    case PlaytestLog.PersonInspired:
                        inspired++;
                        kinds[entry.Detail] = kinds.TryGetValue(entry.Detail, out int had) ? had + 1 : 1;
                        break;
                    case PlaytestLog.PersonMark: if (entry.Amount > 0) marksCame++; else marksHealed++; break;
                    case PlaytestLog.Standing: if (entry.Amount == 2) friends++; else if (entry.Amount == -2) rivals++; break;
                    case PlaytestLog.PassionPick: picks++; break;
                    case PlaytestLog.Spirits:
                        mornings++;
                        brightSum += entry.Amount;
                        peopleSum += entry.Amount + Count(entry.Detail, "low") + Count(entry.Detail, "steady");
                        break;
                }
            }
        }

        if (levels + ranks + inspired + mornings == 0)
        {
            return $"No people rows in {runs} runs yet: the logs hold them from 2026-11-23 on.";
        }

        float hours = Mathf.Max(0.01f, minutes / 60f);
        return $"{runs} runs, {hours:0.#} h played.\n" +
               $"Levels gained by residents: {levels} ({levels / hours:0.#}/h); new ranks: {ranks}.\n" +
               $"Mornings: {mornings}; Bright: {(peopleSum > 0 ? brightSum * 100f / peopleSum : 0f):0}% of people-mornings.\n" +
               $"Inspired mornings: {inspired} ({inspired / hours:0.#}/h): {string.Join(", ", kinds.Select(pair => $"{pair.Key} {pair.Value}"))}.\n" +
               $"Marks: {marksCame} came, {marksHealed} healed. Friends made: {friends}; rivals made: {rivals}. Passions grown: {picks}.";
    }

    // "low 2 steady 5" → the number after a word.
    private static int Count(string detail, string word)
    {
        string[] parts = (detail ?? "").Split(' ');
        for (int i = 0; i + 1 < parts.Length; i++)
        {
            if (parts[i] == word && int.TryParse(parts[i + 1], out int n))
            {
                return n;
            }
        }

        return 0;
    }
}
