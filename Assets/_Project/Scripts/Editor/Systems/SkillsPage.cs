using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skills (Resources/LevelingSettings): the curve and its planned pace beside the pace measured in the playtest logs, so the
/// numbers in Docs/Skills.md can be checked against real play; and what every skill gives at level 1, the baseline and
/// the top.
/// </summary>
public sealed class SkillsPage : SystemsPage
{
    private static readonly int[] Marks = { 5, 10, 20, 30, 40, 50 };
    private static string measured;
    private static int runsRead;

    public override string Title => "Skills";
    public override string Group => "Settings";
    public override string Help => "Skills level by doing (Docs/Skills.md). Each level bonus is 0 at the Baseline Level, the player the game " +
                                   "was tuned for; a beginner is below it. Planned pace assumes 180 actions an hour, a quarter of them for " +
                                   "any one skill, at the tier of its band. Measured pace reads the playtest logs (runs with cheats left out).";

    public override void Draw()
    {
        LevelingSettings settings = Settings<LevelingSettings>(LevelingSettings.ResourceName);
        if (settings == null)
        {
            return;
        }

        SettingsPanel(settings, "Leveling", "Resources/LevelingSettings: the curve, experience per action, Insight and the skills");

        BeginDerived("Planned pace for one skill");
        foreach (int level in Marks.Where(level => level <= settings.MaxLevel))
        {
            Derived($"Level {level}", $"{settings.TotalExperienceFor(level):N0} experience, about {PlannedHours(settings, level):0.#} h of mixed play");
        }

        EndDerived();

        ContentUtility.BeginPanel("Measured in playtests", "Median played time at which a skill first reached each level by practice, over the runs that reached it.");
        if (GUILayout.Button(measured == null ? "Read the playtest logs" : $"Read again ({runsRead} runs read)", GUILayout.Width(260f)))
        {
            measured = Measure(out runsRead);
        }

        EditorGUILayout.LabelField(measured ?? "Not read yet.", EditorStyles.wordWrappedLabel);
        ContentUtility.EndPanel();

        ContentUtility.BeginPanel("What each skill gives", "At level 1, the baseline and the top; its mentor building.");
        foreach (ProficiencyDefinition skill in settings.Skills.Where(skill => skill != null))
        {
            string first = Describe(skill, 1, settings);
            string last = Describe(skill, settings.MaxLevel, settings);
            string mentor = skill.Mentor != null ? $"  ·  mentor {skill.Mentor.DisplayName}" : "";
            EditorGUILayout.LabelField(skill.DisplayName, $"1: {first}   {settings.MaxLevel}: {last}{mentor}", EditorStyles.wordWrappedLabel);
        }

        ContentUtility.EndPanel();
    }

    /// <summary>Hours of mixed play to reach a level: 180 actions an hour, a quarter for one skill, at each band's tier.</summary>
    public static double PlannedHours(LevelingSettings settings, int level)
    {
        double hours = 0;
        for (int at = 1; at < level; at++)
        {
            hours += settings.ExperienceToNext(at) / settings.TierExperience(settings.TierBand(at)) / (180.0 / 4.0);
        }

        return hours;
    }

    private static string Describe(ProficiencyDefinition skill, int level, LevelingSettings settings)
    {
        var parts = new List<string>();
        foreach (ProficiencyDefinition.LevelBonus bonus in skill.LevelBonuses)
        {
            float value = ProficiencyDefinition.ValueAt(bonus, level, settings);
            if (!Mathf.Approximately(value, 0f))
            {
                parts.Add(new StatBonus(bonus.stat, value).Describe());
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "-";
    }

    private static string Measure(out int runs)
    {
        runs = 0;
        var minutes = new Dictionary<string, Dictionary<int, List<float>>>();
        string folder = PlaytestLog.Folder;
        if (!Directory.Exists(folder))
        {
            return "No playtest logs yet.";
        }

        foreach (string file in Directory.GetFiles(folder, "*.csv"))
        {
            List<PlaytestEntry> entries = PlaytestLog.Read(file);
            if (entries.Any(entry => entry.Event == PlaytestLog.Cheat))
            {
                continue;
            }

            runs++;
            foreach (KeyValuePair<string, SortedDictionary<int, float>> skill in PlaytestLog.SkillPace(entries))
            {
                if (!minutes.TryGetValue(skill.Key, out Dictionary<int, List<float>> byLevel))
                {
                    byLevel = new Dictionary<int, List<float>>();
                    minutes[skill.Key] = byLevel;
                }

                foreach (int mark in Marks)
                {
                    if (skill.Value.TryGetValue(mark, out float at))
                    {
                        if (!byLevel.TryGetValue(mark, out List<float> list))
                        {
                            list = new List<float>();
                            byLevel[mark] = list;
                        }

                        list.Add(at);
                    }
                }
            }
        }

        if (minutes.Count == 0)
        {
            return $"No skill levels in {runs} runs yet: the logs only hold them from 2026-11-07 on.";
        }

        var lines = new List<string>();
        foreach (KeyValuePair<string, Dictionary<int, List<float>>> skill in minutes.OrderBy(pair => pair.Key))
        {
            IEnumerable<string> marks = skill.Value.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}: {Median(pair.Value):0} min ({pair.Value.Count})");
            lines.Add($"{skill.Key}  {string.Join("   ", marks)}");
        }

        return string.Join("\n", lines);
    }

    private static float Median(List<float> values)
    {
        values.Sort();
        int middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) * 0.5f;
    }
}
