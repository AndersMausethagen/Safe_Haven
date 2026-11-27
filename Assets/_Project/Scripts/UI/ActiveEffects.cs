using System.Collections.Generic;
using UnityEngine;

/// <summary>One thing at work on the player, as the character sheet shows it: a picture, its name, its time left, what gave it and
/// what it does.</summary>
public struct ActiveEffect
{
    public string Name;
    /// <summary>Its own picture (a meal's item, a skill's icon), or null.</summary>
    public Sprite Picture;
    /// <summary>A kit icon when there is no picture.</summary>
    public string Icon;
    /// <summary>Seconds left; below 0 when it lasts as long as something else does (the mist).</summary>
    public float SecondsLeft;
    /// <summary>What gave it, in words ("Eaten: Roast Boar").</summary>
    public string Source;
    /// <summary>What it does, in words.</summary>
    public string Does;
    public bool Harmful;
}

/// <summary>
/// Everything at work on the player for the character sheet's Active Effects (the user, 2026-11-27: "It should be easier to see which
/// buffs are active, It should show on the C menu under 'Active effects' and the player should easily see what gave that buff and what
/// it does"): the boons (meals, tonics, coatings, the sauna, a festival, being rested; and hindrances such as the cold), the skills'
/// buffs, poisons and other statuses, and the mist's grip on a raid. Reads only.
/// </summary>
public static class ActiveEffects
{
    private static IContentLookup content;

    private static IContentLookup Content() => content != null ? content : content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    /// <summary>What is at work on <paramref name="player"/> now, harmful things first.</summary>
    public static List<ActiveEffect> On(Player player)
    {
        var list = new List<ActiveEffect>();
        if (player == null)
        {
            return list;
        }

        if (player.TryGetComponent(out StatusEffects statuses))
        {
            foreach (StatusEffects.Active status in statuses.Current)
            {
                if (status.Definition == null)
                {
                    continue;
                }

                string from = status.Source != null ? Who(status.Source) : null;
                list.Add(new ActiveEffect
                {
                    Name = status.Definition.DisplayName, Picture = status.Definition.Icon, Icon = "Skull", SecondsLeft = status.SecondsLeft,
                    Source = from != null ? $"Laid on you by {from}" : "Taken in a fight",
                    Does = string.IsNullOrWhiteSpace(status.Definition.Description) ? "Harms you while it lasts." : status.Definition.Description,
                    Harmful = true,
                });
            }
        }

        if (RaidDirector.Current != null && RaidDirector.Current.MistNow.Count > 0)
        {
            list.Add(new ActiveEffect
            {
                Name = "Mist-touched", Icon = "Skull", SecondsLeft = -1f, Source = "Staying long in the mist; it lifts when you go home",
                Does = Bonuses(RaidDirector.Current.MistNow), Harmful = true,
            });
        }

        if (player.Boons != null)
        {
            ConditionIconSettings icons = ConditionIconSettings.Current;
            foreach (BoonRecord boon in player.Boons.Capture())
            {
                ItemDefinition item = Content() != null ? Content().FindItem(boon.Id) : null;
                bool harmful = boon.Id == PlayerWarmth.ChillId;
                string does = Bonuses(boon.Bonuses);
                if (boon.HungerHeldLeft > 0f)
                {
                    does += $"\nYou won't hunger for {RaidStats.FormatTime(boon.HungerHeldLeft)} more.";
                }

                list.Add(new ActiveEffect
                {
                    Name = boon.Name, Picture = item != null && item.Icon != null ? item.Icon : icons.ForBoon(boon.Id), Icon = harmful ? "Snowflake" : "Star",
                    SecondsLeft = boon.SecondsLeft, Source = SourceOf(boon, item), Does = does, Harmful = harmful,
                });
            }
        }

        if (player.Skills != null)
        {
            foreach (KeyValuePair<SkillDefinition, float> buff in player.Skills.ActiveBuffs)
            {
                float left = buff.Value - Time.time;
                if (left <= 0f || buff.Key == null)
                {
                    continue;
                }

                var gives = new List<StatBonus>();
                foreach (SkillStatBuff part in buff.Key.Buffs)
                {
                    gives.Add(new StatBonus(part.stat, part.value, part.op));
                }

                list.Add(new ActiveEffect
                {
                    Name = buff.Key.BuffName, Picture = buff.Key.Icon, Icon = "Spark", SecondsLeft = left,
                    Source = $"Your skill: {buff.Key.DisplayName}", Does = Bonuses(gives),
                });
            }
        }

        // The harmful ones first: they are what the player must see to.
        list.Sort((a, b) => b.Harmful.CompareTo(a.Harmful));
        return list;
    }

    /// <summary>What gave a boon, in words: the meal eaten, the tonic drunk, the place, the festival. Pure but for the item, for tests.</summary>
    public static string SourceOf(BoonRecord boon, ItemDefinition item)
    {
        string id = boon.Id ?? "";
        if (item != null)
        {
            return boon.IsTonic ? $"Drunk: {item.DisplayName}" : boon.IsCoating ? $"Rubbed on your weapon: {item.DisplayName}" : $"Eaten: {item.DisplayName}";
        }

        if (id == HavenReadiness.RestedId) return "A good rest at home before the raid";
        if (id == PlayerWarmth.ChillId) return "The cold: get warm by a fire or under a roof";
        if (id.StartsWith("festival-")) return "The festival in the Haven";
        if (id.StartsWith("technique.")) return "One of your techniques";
        if (id.StartsWith("event_")) return "Something that happened on the raid";
        if (id.StartsWith("place_")) return "A place you rested at";
        if (id == "offering") return "An offering at the stones";
        return boon.IsMeal ? "A meal" : boon.IsTonic ? "A tonic" : "A blessing";
    }

    private static string Bonuses(IReadOnlyList<StatBonus> bonuses)
    {
        if (bonuses == null || bonuses.Count == 0)
        {
            return "";
        }

        var parts = new List<string>(bonuses.Count);
        foreach (StatBonus bonus in bonuses)
        {
            parts.Add(bonus.Describe());
        }

        return string.Join(", ", parts) + ".";
    }

    private static string Who(Combatant source)
    {
        if (source.TryGetComponent(out MonsterAI monster) && monster.Definition != null)
        {
            return "a " + monster.Definition.DisplayName;
        }

        return Player.IsPlayer(source.gameObject) ? "yourself" : source.name;
    }
}
