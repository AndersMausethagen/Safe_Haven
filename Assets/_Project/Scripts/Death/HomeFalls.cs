using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// What a fall at home costs under the death rules (DeathSettings; Docs/DeathPenaltyResearch_2026-11-22.md §4.6): meals and tonics end,
/// each skill loses some of what its next level takes (never a level; not again within the grace), and the bag's things are left in a
/// cairn where the player fell, with half those lessons to give back. Nothing under Gentle, in the prologue or in the first breach
/// (they teach). In a friend's Haven nothing is left behind: there is no cairn in another's world, so only the lessons and meals go.
/// </summary>
public static class HomeFalls
{
    /// <summary>What one fall cost, for the card on waking.</summary>
    public sealed class Cost
    {
        public readonly Dictionary<ProficiencyDefinition, int> Lessons = new Dictionary<ProficiencyDefinition, int>();
        public bool Spared;
        public bool InGrace;
        public int MealsEnded;
        public Cairn Cairn;
        public int Stacks;
    }

    /// <summary>True when this fall costs nothing: Gentle rules, the prologue, or the first (scripted) breach.</summary>
    public static bool Spared(DeathRules rules)
    {
        WardBreachDirector breach = WardBreachDirector.Current;
        bool firstBreach = breach != null && breach.IsActive && breach.Definition != null && !breach.Definition.ThreatensHome;
        return rules == DeathRules.Gentle || DeathSettings.Spared || firstBreach;
    }

    /// <summary>Takes what a fall at <paramref name="fellAt"/> costs <paramref name="player"/>.</summary>
    public static Cost Take(GameObject player, Vector3 fellAt)
    {
        var cost = new Cost();
        DeathRules chosen = DeathSettings.Chosen;
        DeathRuleSet rules = DeathSettings.Current.For(chosen);
        if (player == null || Spared(chosen))
        {
            cost.Spared = true;
            return cost;
        }

        if (rules.boonsEnd && player.TryGetComponent(out PlayerBoons boons))
        {
            cost.MealsEnded = boons.EndMealsAndTonics();
        }

        CharacterProgress progress = GameSession.Progress;
        LevelingSettings leveling = LevelingSettings.Current;
        if (rules.lessonShare > 0f)
        {
            if (progress.FallGraceLeft > 0f)
            {
                cost.InGrace = true;
            }
            else
            {
                foreach (ProficiencyDefinition skill in leveling.Skills)
                {
                    int level = progress.LevelOf(skill, leveling);
                    if (skill == null || level >= leveling.MaxLevel)
                    {
                        continue;
                    }

                    int lost = progress.LoseExperience(skill, Mathf.RoundToInt(leveling.ExperienceToNext(level) * rules.lessonShare), leveling);
                    if (lost > 0)
                    {
                        cost.Lessons[skill] = lost;
                    }
                }
            }

            // Every fall starts it again (Valheim's "No Skill Drain").
            progress.StartFallGrace(rules.graceSeconds);
        }

        if (rules.cairn && !HavenVisit.IsVisiting)
        {
            var back = cost.Lessons.ToDictionary(lesson => lesson.Key, lesson => Mathf.RoundToInt(lesson.Value * rules.lessonsReturned));
            cost.Cairn = Cairn.Leave(player, fellAt, rules.cairnTakesGear, back);
            cost.Stacks = cost.Cairn != null ? cost.Cairn.Stacks : 0;
        }

        int forgotten = cost.Lessons.Values.Sum();
        PlaytestLogger.Note(PlaytestLog.HomeDeath, chosen.ToString(), cost.Stacks, $"lessons {forgotten}{(cost.InGrace ? " (grace)" : "")}, meals {cost.MealsEnded}");
        if (forgotten > 0)
        {
            PlaytestLogger.Note(PlaytestLog.LessonsLost, string.Join(";", cost.Lessons.Select(lesson => $"{lesson.Key.Id}={lesson.Value}")), forgotten);
        }

        return cost;
    }

    /// <summary>The card on waking: what the fall cost and where the rest of it lies. Null when it cost nothing.</summary>
    public static MenuPage Card(Cost cost)
    {
        if (cost == null || cost.Spared)
        {
            return null;
        }

        UIStyle style = UIStyle.Current;
        var lines = new List<string>();
        if (cost.Cairn != null)
        {
            lines.Add(cost.Stacks > 0
                ? $"Your things lie in a cairn where you fell ({cost.Stacks} stack{(cost.Stacks == 1 ? "" : "s")}): it is on your map. Fetch them back."
                : "A cairn marks where you fell: it holds some of what you forgot.");
        }

        if (cost.Lessons.Count > 0)
        {
            string lost = string.Join(", ", cost.Lessons.OrderByDescending(lesson => lesson.Value).Take(4).Select(lesson => $"{lesson.Key.DisplayName} −{lesson.Value}"));
            lines.Add($"<color={UIStyle.Hex(style.Warning)}>You forgot some of what you learned: {lost}.</color> The cairn gives some of it back.");
        }
        else if (cost.InGrace)
        {
            lines.Add("You fell again so soon that you forgot nothing more.");
        }

        if (cost.MealsEnded > 0)
        {
            lines.Add("What you ate and drank is spent.");
        }

        if (lines.Count == 0)
        {
            return null;
        }

        lines.Add($"<color={UIStyle.Hex(style.Muted)}>Death rules: {DeathSettings.Chosen} (Options › Gameplay).</color>");
        return new MenuPage { Title = "You fell", Body = string.Join("\n\n", lines) };
    }
}
