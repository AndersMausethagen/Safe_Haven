using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How good a home is (Docs/BuildingSystemResearch.md §3.4.2), kept pure so it can be tested and shown on a ghost before
/// anything is built: its surroundings (warm, water close, a lit or paved lane, a place to sit, near work; HomeLifeSettings
/// › Features), its tier, the comforts put in it and its yard. Points only add: nothing near a home makes it worse (the
/// user, 2026-11-15). The weakest missing feature is offered as the hint for what to build next.
/// </summary>
public static class HomeQuality
{
    /// <summary>A building standing near, by kind and place on the ground.</summary>
    public readonly struct Around
    {
        public readonly BuildingDefinition Kind;
        public readonly Vector2 At;

        public Around(BuildingDefinition kind, Vector2 at)
        {
            Kind = kind;
            At = at;
        }
    }

    /// <summary>What is known about a home and what lies round it.</summary>
    public sealed class Input
    {
        public Vector2 Door;
        public int Tier = 1;
        public readonly List<string> Comforts = new List<string>();
        public readonly List<Around> Buildings = new List<Around>();
        /// <summary>Lights (a building's VisibilitySource): where, and how far they reach.</summary>
        public readonly List<(Vector2 at, float radius)> Lights = new List<(Vector2, float)>();
        /// <summary>Paved path pieces, end to end.</summary>
        public readonly List<(Vector2 a, Vector2 b)> Paved = new List<(Vector2, Vector2)>();
        /// <summary>Where those who live here work.</summary>
        public readonly List<Vector2> Work = new List<Vector2>();
        /// <summary>Points from the yard it stands in (YardRoleDefinition), and the yard's name.</summary>
        public int YardPoints;
        public string YardName;

        public void Clear()
        {
            Door = default;
            Tier = 1;
            Comforts.Clear();
            Buildings.Clear();
            Lights.Clear();
            Paved.Clear();
            Work.Clear();
            YardPoints = 0;
            YardName = null;
        }
    }

    /// <summary>One thing a home has or lacks.</summary>
    public sealed class Line
    {
        public string Name;
        public string Icon;
        public bool Met;
        public int Points;
        /// <summary>What it is ("Well, 8 m") or, missing, what would give it.</summary>
        public string Detail;
    }

    public sealed class Report
    {
        public readonly List<Line> Lines = new List<Line>();
        public int Quality;
        public HomeLifeSettings.Level Level;

        /// <summary>The first missing feature's hint: what to build next.</summary>
        public string Next
        {
            get
            {
                foreach (Line line in Lines)
                {
                    if (!line.Met && line.Points > 0 && !string.IsNullOrEmpty(line.Detail))
                    {
                        return line.Detail;
                    }
                }

                return null;
            }
        }

        public bool Has(string feature)
        {
            foreach (Line line in Lines)
            {
                if (line.Met && line.Name == feature)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// What a home is like. A comfort that also gives a feature (a Kitchen Hearth makes a house warm) counts once for its one
    /// purchase (review 2026-11-16): when nothing round the house gives that feature, the comfort meets it and its own line
    /// adds nothing more; when something does, the comfort adds its own point.
    /// </summary>
    public static Report Rate(Input input, HomeLifeSettings settings)
    {
        var report = new Report();
        List<string> spent = null;
        foreach (HomeLifeSettings.Feature feature in settings.Features)
        {
            string found = Find(feature, input);
            if (found == null && !string.IsNullOrEmpty(feature.orComfort) && input.Comforts.Contains(feature.orComfort))
            {
                found = "Its own hearth";
                (spent ??= new List<string>()).Add(feature.orComfort);
            }

            report.Lines.Add(new Line
            {
                Name = feature.name,
                Icon = feature.icon,
                Met = found != null,
                Points = feature.points,
                Detail = found ?? feature.hint,
            });
        }

        int tierPoints = Mathf.Max(0, input.Tier - 1) * settings.PointsPerTier;
        report.Lines.Add(new Line { Name = "House tier", Icon = "House", Met = tierPoints > 0, Points = tierPoints,
            Detail = tierPoints > 0 ? $"Tier {Tiers.ToRoman(input.Tier)}" : "Upgrade the house" });
        foreach (HomeLifeSettings.Comfort comfort in settings.Comforts)
        {
            bool has = input.Comforts.Contains(comfort.id);
            bool counted = has && spent != null && spent.Contains(comfort.id);
            report.Lines.Add(new Line { Name = comfort.name, Icon = comfort.icon, Met = has, Points = counted ? 0 : comfort.points,
                Detail = counted ? "In the house: it is what keeps it warm" : has ? "In the house" : comfort.description });
        }

        if (input.YardPoints > 0)
        {
            report.Lines.Add(new Line { Name = input.YardName ?? "Yard", Icon = "Fence", Met = true, Points = input.YardPoints, Detail = "Its yard" });
        }

        foreach (Line line in report.Lines)
        {
            report.Quality += line.Met ? line.Points : 0;
        }

        report.Level = settings.LevelFor(report.Quality);
        return report;
    }

    // What round the house gives this feature, in words ("Well, 8 m"), or null when nothing does (a comfort that gives it is
    // looked at by Rate).
    private static string Find(HomeLifeSettings.Feature feature, Input input)
    {
        switch (feature.kind)
        {
            case HomeFeatureKind.Work:
            {
                float best = float.MaxValue;
                foreach (Vector2 work in input.Work)
                {
                    best = Mathf.Min(best, Vector2.Distance(work, input.Door));
                }

                return best <= feature.within ? $"{best:0} m to work" : null;
            }

            case HomeFeatureKind.Lane:
            {
                string near = Nearest(feature, input);
                if (near != null)
                {
                    return near;
                }

                foreach ((Vector2 at, float radius) in input.Lights)
                {
                    if (Vector2.Distance(at, input.Door) <= radius)
                    {
                        return "In the light";
                    }
                }

                foreach ((Vector2 a, Vector2 b) in input.Paved)
                {
                    if (DistanceToSegment(input.Door, a, b) <= feature.pathWithin)
                    {
                        return "A paved path";
                    }
                }

                return null;
            }

            default:
                return Nearest(feature, input);
        }
    }

    private static string Nearest(HomeLifeSettings.Feature feature, Input input)
    {
        float best = float.MaxValue;
        BuildingDefinition kind = null;
        foreach (Around around in input.Buildings)
        {
            if (around.Kind == null || !feature.near.Contains(around.Kind))
            {
                continue;
            }

            float distance = Vector2.Distance(around.At, input.Door);
            if (distance <= feature.within && distance < best)
            {
                best = distance;
                kind = around.Kind;
            }
        }

        return kind != null ? $"{kind.DisplayName}, {best:0} m" : null;
    }

    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 along = b - a;
        float length = along.sqrMagnitude;
        float t = length > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, along) / length) : 0f;
        return Vector2.Distance(point, a + along * t);
    }
}
