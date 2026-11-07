using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interactables by where they stand, so "what is near this point" looks at the few cells around it instead of every
/// Interactable in the scene (well over a thousand trees and rocks round the Haven, asked several times a frame). Each one
/// is filed under every cell its colliders' bounds cover. Things that move (people, carts) are filed again as they go:
/// a slice of them each frame, and anything newly enabled every frame for its first moments; a query reaches a little
/// further than asked to cover what moved since. Those with an interaction shape of their own are always offered.
/// </summary>
public static class InteractableGrid
{
    private const float CellSize = 8f;
    private const float Slack = 2f;
    private const int RefiledPerFrame = 64;
    private const int FreshFrames = 30;

    private static readonly Dictionary<long, List<Interactable>> cells = new Dictionary<long, List<Interactable>>();
    private static readonly List<Interactable> shaped = new List<Interactable>();
    private static readonly List<(Interactable target, int until)> fresh = new List<(Interactable, int)>();
    private static int stamp;
    private static int cursor;
    private static int refiledFrame = -1;

    /// <summary>The longest reach of any Interactable filed (metres), for queries that must find everything in reach.</summary>
    public static float LongestRange { get; private set; }

    /// <summary>Adds every Interactable within about <paramref name="radius"/> metres (horizontally) of a point to
    /// <paramref name="into"/>, plus some a little further. Callers still test the distance themselves.</summary>
    public static void Near(Vector3 point, float radius, List<Interactable> into)
    {
        Refile();
        stamp++;
        float reach = radius + Slack;
        int minX = Cell(point.x - reach), maxX = Cell(point.x + reach);
        int minZ = Cell(point.z - reach), maxZ = Cell(point.z + reach);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (!cells.TryGetValue(Key(x, z), out List<Interactable> members)) continue;
                for (int i = 0; i < members.Count; i++)
                {
                    Interactable member = members[i];
                    if (member != null && member.GridStamp != stamp)
                    {
                        member.GridStamp = stamp;
                        into.Add(member);
                    }
                }
            }
        }

        for (int i = 0; i < shaped.Count; i++)
        {
            if (shaped[i] != null && shaped[i].GridStamp != stamp)
            {
                shaped[i].GridStamp = stamp;
                into.Add(shaped[i]);
            }
        }
    }

    internal static void Add(Interactable target)
    {
        LongestRange = Mathf.Max(LongestRange, target.Range);
        if (target.HasShape)
        {
            if (!shaped.Contains(target)) shaped.Add(target);
            return;
        }

        File(target);
        fresh.Add((target, Time.frameCount + FreshFrames));
    }

    internal static void Remove(Interactable target)
    {
        shaped.Remove(target);
        Unfile(target);
    }

    private static void File(Interactable target)
    {
        RectInt area = Area(target);
        if (target.IsFiled && target.FiledArea.Equals(area)) return;
        Unfile(target);
        for (int x = area.xMin; x < area.xMax; x++)
        {
            for (int z = area.yMin; z < area.yMax; z++)
            {
                long key = Key(x, z);
                if (!cells.TryGetValue(key, out List<Interactable> members)) cells[key] = members = new List<Interactable>();
                members.Add(target);
            }
        }

        target.FiledArea = area;
        target.IsFiled = true;
    }

    private static void Unfile(Interactable target)
    {
        if (!target.IsFiled) return;
        RectInt area = target.FiledArea;
        for (int x = area.xMin; x < area.xMax; x++)
        {
            for (int z = area.yMin; z < area.yMax; z++)
            {
                if (cells.TryGetValue(Key(x, z), out List<Interactable> members)) members.Remove(target);
            }
        }

        target.IsFiled = false;
    }

    // Once a frame: the newly enabled, and the next slice of the rest, filed again where they now stand.
    private static void Refile()
    {
        if (Application.isPlaying && refiledFrame == Time.frameCount) return;
        refiledFrame = Time.frameCount;
        for (int i = fresh.Count - 1; i >= 0; i--)
        {
            (Interactable target, int until) = fresh[i];
            if (target == null || !target.IsFiled || Time.frameCount > until)
            {
                fresh.RemoveAt(i);
                continue;
            }

            File(target);
        }

        IReadOnlyList<Interactable> active = Interactable.Active;
        int count = Mathf.Min(RefiledPerFrame, active.Count);
        for (int i = 0; i < count; i++)
        {
            cursor = (cursor + 1) % active.Count;
            Interactable target = active[cursor];
            if (target != null && target.IsFiled) File(target);
        }
    }

    // The cells covered by the bounds of its colliders (those in use), or by its position when it has none.
    private static RectInt Area(Interactable target)
    {
        Bounds bounds = target.FilingBounds();
        int minX = Cell(bounds.min.x), maxX = Cell(bounds.max.x);
        int minZ = Cell(bounds.min.z), maxZ = Cell(bounds.max.z);
        return new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
    }

    private static int Cell(float metres) => Mathf.FloorToInt(metres / CellSize);

    private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        cells.Clear();
        shaped.Clear();
        fresh.Clear();
        LongestRange = 0f;
        cursor = 0;
        refiledFrame = -1;
    }
}
