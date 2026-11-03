using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cracks a closed mesh into solid pieces by cutting it with planes, largest piece first. The model's surface stays as it was, in
/// its own space, so the pieces laid together are the model; each cut is sealed with a rough, faceted face in a second submesh.
/// </summary>
public static class MeshCracker
{
    // How far the broken faces stray from the flat cut, as a share of the piece.
    private const float Roughness = 0.05f;
    private const float PlanarUvPerUnit = 0.7f;
    private const int MaxTries = 8;

    private sealed class Part
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color> Colours = new List<Color>();
        public readonly List<int> Skin = new List<int>();
        public readonly List<int> Broken = new List<int>();

        public int Triangles => (Skin.Count + Broken.Count) / 3;

        public Bounds Bounds
        {
            get
            {
                var bounds = new Bounds(Positions.Count > 0 ? Positions[0] : Vector3.zero, Vector3.zero);
                foreach (int index in Skin) bounds.Encapsulate(Positions[index]);
                foreach (int index in Broken) bounds.Encapsulate(Positions[index]);
                return bounds;
            }
        }

        public int Copy(Part from, int index)
        {
            Positions.Add(from.Positions[index]);
            Normals.Add(from.Normals[index]);
            Tangents.Add(from.Tangents[index]);
            Uvs.Add(from.Uvs[index]);
            Colours.Add(from.Colours[index]);
            return Positions.Count - 1;
        }

        public int Between(Part from, int a, int b, float t)
        {
            Positions.Add(Vector3.Lerp(from.Positions[a], from.Positions[b], t));
            Normals.Add(Vector3.Lerp(from.Normals[a], from.Normals[b], t).normalized);
            Tangents.Add(Vector4.Lerp(from.Tangents[a], from.Tangents[b], t));
            Uvs.Add(Vector2.Lerp(from.Uvs[a], from.Uvs[b], t));
            Colours.Add(Color.Lerp(from.Colours[a], from.Colours[b], t));
            return Positions.Count - 1;
        }

        public int Add(Vector3 position, Vector3 normal, Vector3 u, Vector3 v)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Tangents.Add(new Vector4(u.x, u.y, u.z, 1f));
            Uvs.Add(new Vector2(Vector3.Dot(position, u), Vector3.Dot(position, v)) * PlanarUvPerUnit);
            Colours.Add(Color.white);
            return Positions.Count - 1;
        }
    }

    /// <summary>Cracks <paramref name="source"/> into up to <paramref name="pieces"/> meshes, the same way every time for the same
    /// <paramref name="seed"/>. Fewer when a shape can't be cut further.</summary>
    public static List<Mesh> Crack(Mesh source, int pieces, int seed)
    {
        var random = new System.Random(seed);
        var parts = new List<Part> { Read(source) };
        for (int stuck = 0; parts.Count < pieces && stuck < MaxTries;)
        {
            int largest = 0;
            for (int i = 1; i < parts.Count; i++)
            {
                if (parts[i].Bounds.size.sqrMagnitude > parts[largest].Bounds.size.sqrMagnitude) largest = i;
            }

            Part whole = parts[largest];
            Bounds bounds = whole.Bounds;
            Plane plane = ChoosePlane(bounds, random);
            Slice(whole, plane, bounds.size.magnitude * Roughness, random, out Part above, out Part below);
            // A cut has to leave two real pieces, or it only shaved the edge.
            if (above.Triangles < 8 || below.Triangles < 8 || above.Bounds.size.sqrMagnitude < bounds.size.sqrMagnitude * 0.04f
                || below.Bounds.size.sqrMagnitude < bounds.size.sqrMagnitude * 0.04f)
            {
                stuck++;
                continue;
            }

            stuck = 0;
            parts[largest] = above;
            parts.Add(below);
        }

        // A scanned rock is often open underneath, unseen until a piece rolls over: lined with the inside of the rock, it isn't a shell.
        bool open = !IsClosed(source);
        var meshes = new List<Mesh>(parts.Count);
        for (int i = 0; i < parts.Count; i++)
        {
            if (open) Line(parts[i]);
            meshes.Add(ToMesh(parts[i], $"{source.name} piece {i + 1}"));
        }

        return meshes;
    }

    private static bool IsClosed(Mesh mesh)
    {
        Vector3[] points = mesh.vertices;
        Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
        var edges = new Dictionary<(Vector3Int, Vector3Int), int>();
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] triangles = mesh.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3Int a = Key(points[triangles[i + corner]]), b = Key(points[triangles[i + (corner + 1) % 3]]);
                    if (a == b) continue;
                    edges.TryGetValue((b, a), out int back);
                    if (back > 0) edges[(b, a)] = back - 1;
                    else { edges.TryGetValue((a, b), out int here); edges[(a, b)] = here + 1; }
                }
            }
        }

        foreach (int left in edges.Values)
        {
            if (left > 0) return false;
        }

        return true;
    }

    // The surface again, facing inwards, among the broken faces.
    private static void Line(Part part)
    {
        int skin = part.Skin.Count;
        for (int i = 0; i < skin; i += 3)
        {
            for (int corner = 2; corner >= 0; corner--)
            {
                int at = part.Skin[i + corner];
                part.Positions.Add(part.Positions[at]);
                part.Normals.Add(-part.Normals[at]);
                part.Tangents.Add(part.Tangents[at]);
                part.Uvs.Add(part.Uvs[at]);
                part.Colours.Add(Color.white);
                part.Broken.Add(part.Positions.Count - 1);
            }
        }
    }

    private static Part Read(Mesh mesh)
    {
        var part = new Part();
        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Vector2[] uvs = mesh.uv;
        Color[] colours = mesh.colors;
        for (int i = 0; i < positions.Length; i++)
        {
            part.Positions.Add(positions[i]);
            part.Normals.Add(i < normals.Length ? normals[i] : Vector3.up);
            part.Tangents.Add(i < tangents.Length ? tangents[i] : new Vector4(1f, 0f, 0f, 1f));
            part.Uvs.Add(i < uvs.Length ? uvs[i] : Vector2.zero);
            part.Colours.Add(i < colours.Length ? colours[i] : Color.white);
        }

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            part.Skin.AddRange(mesh.GetTriangles(sub));
        }

        return part;
    }

    // Across its longest side, tilted at random, and off centre so the pieces aren't all alike.
    private static Plane ChoosePlane(Bounds bounds, System.Random random)
    {
        Vector3 size = bounds.size;
        Vector3 across = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        var tilt = new Vector3(Roll(random), Roll(random), Roll(random));
        Vector3 normal = (across + tilt * 0.6f).normalized;
        float along = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        return new Plane(normal, bounds.center + normal * (along * 0.16f * Roll(random)));
    }

    private static float Roll(System.Random random) => (float)random.NextDouble() * 2f - 1f;

    private static void Slice(Part whole, Plane plane, float rough, System.Random random, out Part above, out Part below)
    {
        above = new Part();
        below = new Part();
        var aboveMap = new Dictionary<int, int>();
        var belowMap = new Dictionary<int, int>();
        var cuts = new List<(Vector3 from, Vector3 to)>();
        Cut(whole, whole.Skin, plane, above, below, aboveMap, belowMap, above.Skin, below.Skin, cuts);
        Cut(whole, whole.Broken, plane, above, below, aboveMap, belowMap, above.Broken, below.Broken, cuts);
        foreach (List<Vector3> loop in Loops(cuts))
        {
            Seal(above, loop, -plane.normal, rough, random);
            Seal(below, loop, plane.normal, rough, random);
        }
    }

    private static void Cut(Part whole, List<int> triangles, Plane plane, Part above, Part below, Dictionary<int, int> aboveMap, Dictionary<int, int> belowMap,
        List<int> aboveOut, List<int> belowOut, List<(Vector3, Vector3)> cuts)
    {
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            float da = Off(plane, whole.Positions[a]), db = Off(plane, whole.Positions[b]), dc = Off(plane, whole.Positions[c]);
            int up = (da > 0f ? 1 : 0) + (db > 0f ? 1 : 0) + (dc > 0f ? 1 : 0);
            if (up == 3 || up == 0)
            {
                Part to = up == 3 ? above : below;
                Dictionary<int, int> map = up == 3 ? aboveMap : belowMap;
                List<int> list = up == 3 ? aboveOut : belowOut;
                list.Add(Mapped(to, whole, map, a));
                list.Add(Mapped(to, whole, map, b));
                list.Add(Mapped(to, whole, map, c));
                continue;
            }

            // Turned so that a is alone on its side, keeping the winding.
            bool alone = up == 1 ? da > 0f : da <= 0f;
            if (!alone)
            {
                bool second = up == 1 ? db > 0f : db <= 0f;
                if (second) { (a, b, c) = (b, c, a); (da, db, dc) = (db, dc, da); }
                else { (a, b, c) = (c, a, b); (da, db, dc) = (dc, da, db); }
            }

            float toB = da / (da - db), toC = da / (da - dc);
            bool loneUp = da > 0f;
            Part lone = loneUp ? above : below, pair = loneUp ? below : above;
            Dictionary<int, int> loneMap = loneUp ? aboveMap : belowMap, pairMap = loneUp ? belowMap : aboveMap;
            List<int> loneOut = loneUp ? aboveOut : belowOut, pairOut = loneUp ? belowOut : aboveOut;

            int loneB = lone.Between(whole, a, b, toB), loneC = lone.Between(whole, a, c, toC);
            loneOut.Add(Mapped(lone, whole, loneMap, a));
            loneOut.Add(loneB);
            loneOut.Add(loneC);

            int pairB = pair.Between(whole, a, b, toB), pairC = pair.Between(whole, a, c, toC);
            int keepB = Mapped(pair, whole, pairMap, b), keepC = Mapped(pair, whole, pairMap, c);
            pairOut.Add(pairB); pairOut.Add(keepB); pairOut.Add(keepC);
            pairOut.Add(pairB); pairOut.Add(keepC); pairOut.Add(pairC);
            cuts.Add((Vector3.Lerp(whole.Positions[a], whole.Positions[b], toB), Vector3.Lerp(whole.Positions[a], whole.Positions[c], toC)));
        }
    }

    // Nothing lies exactly on the cut, or a triangle could fall between the two sides.
    private static float Off(Plane plane, Vector3 point)
    {
        float distance = plane.GetDistanceToPoint(point);
        return Mathf.Abs(distance) < 1e-5f ? 1e-5f : distance;
    }

    private static int Mapped(Part to, Part from, Dictionary<int, int> map, int index)
    {
        if (!map.TryGetValue(index, out int mapped))
        {
            mapped = to.Copy(from, index);
            map[index] = mapped;
        }

        return mapped;
    }

    // The cut's edges joined up into rings, one for each hole it opened.
    private static List<List<Vector3>> Loops(List<(Vector3 from, Vector3 to)> cuts)
    {
        Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 20000f), Mathf.RoundToInt(p.y * 20000f), Mathf.RoundToInt(p.z * 20000f));
        var joins = new Dictionary<Vector3Int, List<int>>();
        for (int i = 0; i < cuts.Count; i++)
        {
            foreach (Vector3 end in new[] { cuts[i].from, cuts[i].to })
            {
                Vector3Int key = Key(end);
                if (!joins.TryGetValue(key, out List<int> list)) joins[key] = list = new List<int>(2);
                list.Add(i);
            }
        }

        var used = new bool[cuts.Count];
        var loops = new List<List<Vector3>>();
        for (int start = 0; start < cuts.Count; start++)
        {
            if (used[start]) continue;
            used[start] = true;
            var loop = new List<Vector3> { cuts[start].from };
            Vector3 head = cuts[start].to;
            while (true)
            {
                loop.Add(head);
                int next = -1;
                foreach (int candidate in joins[Key(head)])
                {
                    if (!used[candidate]) { next = candidate; break; }
                }

                if (next < 0) break;
                used[next] = true;
                head = Key(cuts[next].from) == Key(head) ? cuts[next].to : cuts[next].from;
            }

            if (Key(loop[loop.Count - 1]) == Key(loop[0])) loop.RemoveAt(loop.Count - 1);
            if (loop.Count >= 3) loops.Add(loop);
        }

        return loops;
    }

    // Seals a ring with a rough face looking along outward: the ring, a coarser wandering ring inside it, and a middle.
    private static void Seal(Part part, List<Vector3> loop, Vector3 outward, float rough, System.Random random)
    {
        Vector3 middle = Vector3.zero;
        foreach (Vector3 point in loop) middle += point;
        middle /= loop.Count;

        Vector3 u = Vector3.Cross(outward, Mathf.Abs(outward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(outward, u);

        // Wound to face outward.
        float area = 0f;
        for (int i = 0; i < loop.Count; i++)
        {
            area += Vector3.Dot(Vector3.Cross(loop[i] - middle, loop[(i + 1) % loop.Count] - middle), outward);
        }

        var ring = new List<Vector3>(loop);
        if (area < 0f) ring.Reverse();

        int inner = Mathf.Clamp(ring.Count / 6, 5, 14);
        var inside = new Vector3[inner];
        for (int i = 0; i < inner; i++)
        {
            Vector3 onRing = ring[i * ring.Count / inner];
            inside[i] = Vector3.Lerp(middle, onRing, 0.45f + 0.2f * (float)random.NextDouble()) + outward * (rough * Roll(random));
        }

        Vector3 centre = middle + outward * (rough * Roll(random));
        for (int i = 0; i < ring.Count; i++)
        {
            int k = i * inner / ring.Count;
            int nextK = (i + 1) % ring.Count * inner / ring.Count;
            Facet(part, ring[i], ring[(i + 1) % ring.Count], inside[k % inner], u, v);
            if (nextK % inner != k % inner)
            {
                Facet(part, ring[(i + 1) % ring.Count], inside[nextK % inner], inside[k % inner], u, v);
            }
        }

        for (int i = 0; i < inner; i++)
        {
            Facet(part, inside[i], inside[(i + 1) % inner], centre, u, v);
        }
    }

    // Each facet its own flat face, the way broken stone catches the light.
    private static void Facet(Part part, Vector3 a, Vector3 b, Vector3 c, Vector3 u, Vector3 v)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        if (normal.sqrMagnitude < 0.5f) normal = Vector3.Cross(u, v);
        part.Broken.Add(part.Add(a, normal, u, v));
        part.Broken.Add(part.Add(b, normal, u, v));
        part.Broken.Add(part.Add(c, normal, u, v));
    }

    private static Mesh ToMesh(Part part, string name)
    {
        var mesh = new Mesh { name = name };
        if (part.Positions.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(part.Positions);
        mesh.SetNormals(part.Normals);
        mesh.SetTangents(part.Tangents);
        mesh.SetUVs(0, part.Uvs);
        mesh.SetColors(part.Colours);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(part.Skin, 0);
        mesh.SetTriangles(part.Broken, 1);
        mesh.RecalculateBounds();
        return mesh;
    }
}
