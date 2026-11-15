using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds the closed rings that fence pieces make on the ground (Docs/BuildingSystemResearch.md §3.4.4), kept pure so it
/// can be tested: each piece is an edge between its two ends; ends within <c>join</c> metres are one post; an end touching
/// the side of another edge (a fence run up to a house wall) splits it there. Every bounded face of that drawing is a ring.
/// A house's walls may be edges too, so a house can close one side of a yard. The same edges always give the same rings,
/// so every machine in co-op finds the same yards from the same synced pieces.
/// </summary>
public static class YardRings
{
    /// <summary>One straight piece of boundary: a fence piece, a gate, or a house wall.</summary>
    public readonly struct Edge
    {
        public readonly Vector2 A;
        public readonly Vector2 B;
        public readonly bool Gate;
        /// <summary>A fence piece (not a house wall).</summary>
        public readonly bool Fence;
        /// <summary>Whose edge it is (the caller's index), so a ring can name its pieces.</summary>
        public readonly int Owner;

        public Edge(Vector2 a, Vector2 b, bool gate, bool fence, int owner)
        {
            A = a;
            B = b;
            Gate = gate;
            Fence = fence;
            Owner = owner;
        }
    }

    public sealed class Ring
    {
        public readonly List<Vector2> Corners = new List<Vector2>();
        /// <summary>The owners of its edges (fences, gates, house walls), each once.</summary>
        public readonly List<int> Owners = new List<int>();
        public float Area;
        public bool HasGate;
        public bool HasFence;
        public Vector2 Centre;

        public bool Contains(Vector2 point) => Inside(Corners, point);
    }

    private sealed class Link
    {
        public int From;
        public int To;
        public float Angle;
        public int EdgeIndex;
        public bool Seen;
        public Link Twin;
    }

    /// <summary>Every ring whose area is within <paramref name="minArea"/>..<paramref name="maxArea"/> square metres.</summary>
    public static List<Ring> Find(IReadOnlyList<Edge> edges, float join, float minArea, float maxArea)
    {
        var rings = new List<Ring>();
        // Posts: ends within join of each other are one.
        var nodes = new List<Vector2>();
        var ends = new int[edges.Count * 2];
        for (int e = 0; e < edges.Count; e++)
        {
            ends[e * 2] = Post(nodes, edges[e].A, join);
            ends[e * 2 + 1] = Post(nodes, edges[e].B, join);
        }

        // An end against the side of another edge splits that edge: a fence run up to a wall meets it there.
        var pieces = new List<(int a, int b, int edge)>();
        for (int e = 0; e < edges.Count; e++)
        {
            int a = ends[e * 2], b = ends[e * 2 + 1];
            if (a == b)
            {
                continue;
            }

            var splits = new List<(float t, int node)>();
            for (int n = 0; n < nodes.Count; n++)
            {
                if (n == a || n == b)
                {
                    continue;
                }

                Vector2 along = nodes[b] - nodes[a];
                float length = along.sqrMagnitude;
                float t = length > 1e-6f ? Vector2.Dot(nodes[n] - nodes[a], along) / length : 0f;
                if (t > 0.02f && t < 0.98f && Vector2.Distance(nodes[a] + along * t, nodes[n]) <= join)
                {
                    splits.Add((t, n));
                }
            }

            splits.Sort((x, y) => x.t.CompareTo(y.t));
            int from = a;
            foreach ((float _, int node) in splits)
            {
                pieces.Add((from, node, e));
                from = node;
            }

            pieces.Add((from, b, e));
        }

        // One link each way per pair of posts (a gate and a fence over the same span count as the gate).
        var links = new List<Link>();
        var byPair = new Dictionary<long, Link>();
        var outgoing = new List<List<Link>>();
        for (int n = 0; n < nodes.Count; n++)
        {
            outgoing.Add(new List<Link>());
        }

        foreach ((int a, int b, int edge) in pieces)
        {
            if (a == b)
            {
                continue;
            }

            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (byPair.TryGetValue(key, out Link known))
            {
                if (edges[edge].Gate || (edges[edge].Fence && !edges[known.EdgeIndex].Fence))
                {
                    known.EdgeIndex = edge;
                    known.Twin.EdgeIndex = edge;
                }

                continue;
            }

            var there = new Link { From = a, To = b, EdgeIndex = edge, Angle = Angle(nodes[b] - nodes[a]) };
            var back = new Link { From = b, To = a, EdgeIndex = edge, Angle = Angle(nodes[a] - nodes[b]), Twin = there };
            there.Twin = back;
            byPair[key] = there;
            links.Add(there);
            links.Add(back);
            outgoing[a].Add(there);
            outgoing[b].Add(back);
        }

        foreach (List<Link> around in outgoing)
        {
            around.Sort((x, y) => x.Angle.CompareTo(y.Angle));
        }

        // Faces: from each link, turn to the next link clockwise from the way back, until round again.
        var corners = new List<int>();
        var faceLinks = new List<Link>();
        foreach (Link start in links)
        {
            if (start.Seen)
            {
                continue;
            }

            corners.Clear();
            faceLinks.Clear();
            Link link = start;
            int guard = links.Count + 1;
            while (link != null && !link.Seen && guard-- > 0)
            {
                link.Seen = true;
                corners.Add(link.From);
                faceLinks.Add(link);
                List<Link> around = outgoing[link.To];
                int back = around.IndexOf(link.Twin);
                link = around[(back - 1 + around.Count) % around.Count];
            }

            if (link != start || corners.Count < 3)
            {
                continue;
            }

            var ring = new Ring();
            foreach (int corner in corners)
            {
                ring.Corners.Add(nodes[corner]);
            }

            ring.Area = SignedArea(ring.Corners);
            if (ring.Area < minArea || ring.Area > maxArea)
            {
                continue;
            }

            Vector2 sum = Vector2.zero;
            foreach (Vector2 corner in ring.Corners)
            {
                sum += corner;
            }

            ring.Centre = sum / ring.Corners.Count;
            foreach (Link side in faceLinks)
            {
                Edge edge = edges[side.EdgeIndex];
                ring.HasGate |= edge.Gate;
                ring.HasFence |= edge.Fence;
                if (!ring.Owners.Contains(edge.Owner))
                {
                    ring.Owners.Add(edge.Owner);
                }
            }

            if (ring.HasFence)
            {
                rings.Add(ring);
            }
        }

        return rings;
    }

    /// <summary>True when <paramref name="point"/> lies inside the polygon (even-odd rule).</summary>
    public static bool Inside(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>The polygon's area, positive when its corners run anticlockwise (x right, y up).</summary>
    public static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        float twice = 0f;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            twice += (polygon[j].x * polygon[i].y) - (polygon[i].x * polygon[j].y);
        }

        return twice * 0.5f;
    }

    private static int Post(List<Vector2> nodes, Vector2 at, float join)
    {
        for (int n = 0; n < nodes.Count; n++)
        {
            if ((nodes[n] - at).sqrMagnitude <= join * join)
            {
                return n;
            }
        }

        nodes.Add(at);
        return nodes.Count - 1;
    }

    private static float Angle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x);
}
