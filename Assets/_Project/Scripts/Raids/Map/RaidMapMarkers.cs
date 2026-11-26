using System.Collections.Generic;
using UnityEngine;

/// <summary>What a marker on a raid map stands for. See <see cref="RaidMapMarkers"/> for the colours.</summary>
public enum RaidSiteKind
{
    None = 0,
    PlayerStart = 1,
    Extraction = 2,
    EnemyCamp = 3,
    ResourceSite = 4,
    SupplyCache = 5,
    EventSite = 6,
    Patrol = 7,
    /// <summary>Generated maps: a place off the beaten track, for what stands apart (story objects, stones).</summary>
    Side = 8,
    /// <summary>Generated maps: where the raid's greatest danger lives.</summary>
    Apex = 9,
    /// <summary>Generated maps: where what the briefing's rumours speak of turns out to be.</summary>
    Rumour = 10,
}

/// <summary>One marker found on a marker image: its kind and its centre, 0–1 across the map (x right, y up = north).</summary>
public readonly struct RaidSite
{
    public readonly RaidSiteKind Kind;
    public readonly Vector2 Position;

    public RaidSite(RaidSiteKind kind, Vector2 position)
    {
        Kind = kind;
        Position = position;
    }

    public override string ToString() => $"{Kind} at {Position}";
}

/// <summary>
/// A site of a generated map where it stands in the world, and what it is: its kind and, where groups share the kind, the group that
/// took it ("Ore Veins"). RaidMap keeps them for what asks where the map's places are: raid events taking an event room, the playtest
/// log's "came upon" rows (Docs/RaidLayoutAnalysis_2026-11-26.md R3, R9).
/// </summary>
public readonly struct RaidPlace
{
    public readonly RaidSiteKind Kind;
    public readonly string Label;
    public readonly Vector3 Position;

    public RaidPlace(RaidSiteKind kind, string label, Vector3 position)
    {
        Kind = kind;
        Label = string.IsNullOrEmpty(label) ? kind.ToString() : label;
        Position = position;
    }
}

/// <summary>
/// The colours of raid map markers (the painter's handles use them) and the reader of the older marker images: dots of
/// pure colour on black mark where the player starts, the exits, enemy camps, resource sites, supply caches, events and
/// patrol spots. Each connected blob of one colour is one site, at the blob's centre. Colours that aren't in the legend
/// are ignored. Kept free of the editor so it can be tested.
/// </summary>
public static class RaidMapMarkers
{
    /// <summary>The marker colours. Paint dots of a few pixels in exactly these colours.</summary>
    public static readonly IReadOnlyList<(RaidSiteKind Kind, Color32 Color)> Legend = new[]
    {
        (RaidSiteKind.PlayerStart, new Color32(255, 255, 255, 255)),  // white
        (RaidSiteKind.Extraction, new Color32(0, 0, 255, 255)),       // blue
        (RaidSiteKind.EnemyCamp, new Color32(255, 0, 0, 255)),        // red
        (RaidSiteKind.ResourceSite, new Color32(255, 255, 0, 255)),   // yellow
        (RaidSiteKind.SupplyCache, new Color32(0, 255, 0, 255)),      // green
        (RaidSiteKind.EventSite, new Color32(255, 0, 255, 255)),      // magenta
        (RaidSiteKind.Patrol, new Color32(0, 255, 255, 255)),         // cyan
    };

    // How far (0–255 per channel, summed) a pixel may be from a legend colour, for images saved with slight blending.
    private const int Tolerance = 90;

    public static Color32 ColorOf(RaidSiteKind kind)
    {
        foreach ((RaidSiteKind Kind, Color32 Color) entry in Legend)
        {
            if (entry.Kind == kind)
            {
                return entry.Color;
            }
        }

        return new Color32(0, 0, 0, 255);
    }

    public static RaidSiteKind KindOf(Color32 pixel)
    {
        RaidSiteKind best = RaidSiteKind.None;
        int bestDistance = Tolerance + 1;
        foreach ((RaidSiteKind Kind, Color32 Color) entry in Legend)
        {
            int distance = Mathf.Abs(pixel.r - entry.Color.r) + Mathf.Abs(pixel.g - entry.Color.g) + Mathf.Abs(pixel.b - entry.Color.b);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = entry.Kind;
            }
        }

        return best;
    }

    /// <summary>
    /// The sites on a marker image given as Texture2D.GetPixels32 (rows from the bottom). Sorted by kind, then south to
    /// north, then west to east, so the same image always gives the same order.
    /// </summary>
    public static List<RaidSite> Decode(Color32[] pixels, int width, int height)
    {
        var sites = new List<RaidSite>();
        var kinds = new RaidSiteKind[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            kinds[i] = KindOf(pixels[i]);
        }

        var visited = new bool[pixels.Length];
        var stack = new Stack<int>();
        for (int start = 0; start < pixels.Length; start++)
        {
            RaidSiteKind kind = kinds[start];
            if (kind == RaidSiteKind.None || visited[start])
            {
                continue;
            }

            // Flood fill the blob (4-connected) and average its pixel centres.
            double sumX = 0d;
            double sumY = 0d;
            int count = 0;
            visited[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int index = stack.Pop();
                int x = index % width;
                int y = index / width;
                sumX += x + 0.5d;
                sumY += y + 0.5d;
                count++;
                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
            }

            sites.Add(new RaidSite(kind, new Vector2((float)(sumX / count / width), (float)(sumY / count / height))));

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                int index = y * width + x;
                if (!visited[index] && kinds[index] == kind)
                {
                    visited[index] = true;
                    stack.Push(index);
                }
            }
        }

        Sort(sites);
        return sites;
    }

    /// <summary>Sorts sites by kind, then south to north, then west to east, so the same map always gives the same order.</summary>
    public static void Sort(List<RaidSite> sites)
    {
        sites.Sort((a, b) =>
        {
            int order = a.Kind.CompareTo(b.Kind);
            if (order == 0) order = a.Position.y.CompareTo(b.Position.y);
            return order != 0 ? order : a.Position.x.CompareTo(b.Position.x);
        });
    }

    /// <summary>
    /// Shares sites among the groups that use them, one whole site per group in turn, so a camp is all wolves or all
    /// guardians and two groups never share a spot. Returns, for each site, the index of the group that gets it.
    /// </summary>
    public static int[] ShareOut(int siteCount, int groupCount)
    {
        var owners = new int[siteCount];
        for (int i = 0; i < siteCount; i++)
        {
            owners[i] = groupCount > 0 ? i % groupCount : -1;
        }

        return owners;
    }

    /// <summary>
    /// Shares sites among groups as <see cref="ShareOut(int,int)"/> does, but by weight and, for a group that asks, from the far end
    /// (Docs/RaidLayoutAnalysis_2026-11-26.md R1: ore veins lie farther out, timber stands are commoner). How many sites each group gets is
    /// its weight's share, rounded by largest remainder (ties to the earlier group). Groups that take the farthest go first, in order,
    /// each taking the farthest sites left (by <paramref name="distances"/>, the metres from the start); the others take the rest in
    /// turn, as the plain share does. With equal weights and none taking the farthest it gives exactly the plain share.
    /// </summary>
    public static int[] ShareOut(IReadOnlyList<float> distances, IReadOnlyList<float> weights, IReadOnlyList<bool> farthest)
    {
        int siteCount = distances.Count;
        int groupCount = weights.Count;
        var owners = new int[siteCount];
        for (int i = 0; i < siteCount; i++)
        {
            owners[i] = -1;
        }

        if (groupCount == 0)
        {
            return owners;
        }

        int[] wanted = Apportion(siteCount, weights);
        var order = new List<int>(siteCount);
        for (int i = 0; i < siteCount; i++)
        {
            order.Add(i);
        }

        // Farthest first; ties keep the plan's order.
        order.Sort((a, b) => distances[b] != distances[a] ? distances[b].CompareTo(distances[a]) : a.CompareTo(b));
        int next = 0;
        for (int group = 0; group < groupCount; group++)
        {
            if (!farthest[group])
            {
                continue;
            }

            for (int taken = 0; taken < wanted[group] && next < order.Count; taken++)
            {
                owners[order[next++]] = group;
            }

            wanted[group] = 0;
        }

        // The rest in turn, in the plan's order, skipping a group once it has its share.
        int turn = 0;
        for (int site = 0; site < siteCount; site++)
        {
            if (owners[site] >= 0)
            {
                continue;
            }

            for (int tries = 0; tries < groupCount; tries++)
            {
                int group = (turn + tries) % groupCount;
                if (wanted[group] > 0)
                {
                    owners[site] = group;
                    wanted[group]--;
                    turn = group + 1;
                    break;
                }
            }
        }

        return owners;
    }

    // Whole sites per group by weight: floors, then the largest remainders (earlier groups first on a tie).
    private static int[] Apportion(int total, IReadOnlyList<float> weights)
    {
        int count = weights.Count;
        var shares = new int[count];
        float sum = 0f;
        foreach (float weight in weights)
        {
            sum += Mathf.Max(0f, weight);
        }

        if (sum <= 0f)
        {
            for (int i = 0; i < total; i++)
            {
                shares[i % count]++;
            }

            return shares;
        }

        var remainders = new float[count];
        int given = 0;
        for (int i = 0; i < count; i++)
        {
            float exact = total * Mathf.Max(0f, weights[i]) / sum;
            shares[i] = Mathf.FloorToInt(exact + 1e-4f);
            remainders[i] = exact - shares[i];
            given += shares[i];
        }

        while (given < total)
        {
            int best = 0;
            for (int i = 1; i < count; i++)
            {
                if (remainders[i] > remainders[best] + 1e-4f)
                {
                    best = i;
                }
            }

            shares[best]++;
            remainders[best] = -1f;
            given++;
        }

        return shares;
    }
}
