using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// What the terrain looked like under the World Builder's features, so they can be moved or deleted without losing the
/// ground beneath (an asset next to the TerrainData, made by the World Builder). Only cells a feature touches are kept:
/// their ground before any feature ("base") and what the builder last wrote there ("last"). Hand edits made since the
/// last build show up as a difference from "last" and are taken into the base, so sculpting and painting by hand keep
/// working under rivers and paths. Removed grass and trees are kept the same way and come back when a feature leaves.
/// </summary>
public sealed class WorldTerrainStash : ScriptableObject
{
    [Serializable]
    public struct StashedTree
    {
        public Vector3 position;
        public float widthScale;
        public float heightScale;
        public float rotation;
        public Color32 color;
        public Color32 lightmapColor;
        public int prototypeIndex;

        public StashedTree(TreeInstance tree)
        {
            position = tree.position;
            widthScale = tree.widthScale;
            heightScale = tree.heightScale;
            rotation = tree.rotation;
            color = tree.color;
            lightmapColor = tree.lightmapColor;
            prototypeIndex = tree.prototypeIndex;
        }

        public TreeInstance ToTree() => new TreeInstance
        {
            position = position, widthScale = widthScale, heightScale = heightScale, rotation = rotation,
            color = color, lightmapColor = lightmapColor, prototypeIndex = prototypeIndex,
        };
    }

    [SerializeField] private int heightResolution;
    [SerializeField] private List<int> heightCells = new List<int>();
    [SerializeField] private List<float> heightBase = new List<float>();
    [SerializeField] private List<float> heightLast = new List<float>();

    [SerializeField] private int alphaResolution;
    [SerializeField] private int alphaLayers;
    [SerializeField] private List<int> alphaCells = new List<int>();
    [SerializeField] private List<byte> alphaBase = new List<byte>();
    [SerializeField] private List<byte> alphaLast = new List<byte>();
    // Splat cells painted by hand where a feature paints: builds leave them as painted, until handed back.
    [SerializeField] private List<int> handPaintedCells = new List<int>();

    [SerializeField] private int detailResolution;
    [SerializeField] private List<int> detailCells = new List<int>(); // layer * resolution² + cell
    [SerializeField] private List<int> detailBase = new List<int>();
    // What the last build left in each kept cell (HandKept: touched by hand since, builds leave it as it is).
    [SerializeField] private List<int> detailLast = new List<int>();

    [SerializeField] private List<StashedTree> trees = new List<StashedTree>();

    [NonSerialized] private Dictionary<int, int> heightIndex;
    [NonSerialized] private Dictionary<int, int> alphaIndex;
    [NonSerialized] private Dictionary<int, int> detailIndex;
    [NonSerialized] private HashSet<int> handPainted;

    public int HeightCount => heightCells.Count;
    public int AlphaCount => alphaCells.Count;
    public int DetailCount => detailCells.Count;
    public List<StashedTree> Trees => trees;
    public int AlphaLayers => alphaLayers;

    /// <summary>Clears whatever was kept for a grid whose resolution changed (the old cells no longer line up).</summary>
    public bool Prepare(int heights, int alphas, int layers, int details)
    {
        bool reset = false;
        if (heightResolution != heights)
        {
            heightResolution = heights;
            heightCells.Clear();
            heightBase.Clear();
            heightLast.Clear();
            reset |= heightIndex != null && heightIndex.Count > 0;
            heightIndex = null;
        }

        if (alphaResolution != alphas)
        {
            alphaResolution = alphas;
            alphaCells.Clear();
            alphaBase.Clear();
            alphaLast.Clear();
            alphaIndex = null;
            handPaintedCells.Clear();
            handPainted = null;
        }

        if (alphaLayers != layers)
        {
            Relayer(layers);
        }

        if (detailResolution != details)
        {
            detailResolution = details;
            detailCells.Clear();
            detailBase.Clear();
            detailLast.Clear();
            detailIndex = null;
        }

        // Stashes from before grass was grown by features only cleared it: what they last left was always nothing.
        while (detailLast.Count < detailCells.Count)
        {
            detailLast.Add(0);
        }

        return reset;
    }

    // ------------------------------------------------------------------ heights

    public IEnumerable<int> HeightCells => heightCells;

    public bool TryGetHeight(int cell, out float baseHeight, out float last)
    {
        heightIndex ??= Index(heightCells);
        if (heightIndex.TryGetValue(cell, out int i))
        {
            baseHeight = heightBase[i];
            last = heightLast[i];
            return true;
        }

        baseHeight = last = 0f;
        return false;
    }

    public void SetHeight(int cell, float baseHeight, float last)
    {
        heightIndex ??= Index(heightCells);
        if (heightIndex.TryGetValue(cell, out int i))
        {
            heightBase[i] = baseHeight;
            heightLast[i] = last;
            return;
        }

        heightIndex[cell] = heightCells.Count;
        heightCells.Add(cell);
        heightBase.Add(baseHeight);
        heightLast.Add(last);
    }

    public void RemoveHeights(ICollection<int> cells)
    {
        RemoveWhere(heightCells, cell => cells.Contains(cell), heightBase, heightLast);
        heightIndex = null;
    }

    /// <summary>Moves every kept height when the terrain is re-based (more room above or below): new = old * scale + offset.</summary>
    public void RemapHeights(float scale, float offset)
    {
        for (int i = 0; i < heightBase.Count; i++)
        {
            heightBase[i] = heightBase[i] * scale + offset;
            heightLast[i] = heightLast[i] * scale + offset;
        }
    }

    // ------------------------------------------------------------------ paint

    public IEnumerable<int> AlphaCells => alphaCells;
    public int HandPaintedCount => handPaintedCells.Count;

    /// <summary>Whether this splat cell was painted by hand over a feature's paint (builds then leave it alone).</summary>
    public bool IsHandPainted(int cell)
    {
        handPainted ??= new HashSet<int>(handPaintedCells);
        return handPainted.Contains(cell);
    }

    public void MarkHandPainted(int cell)
    {
        handPainted ??= new HashSet<int>(handPaintedCells);
        if (handPainted.Add(cell))
        {
            handPaintedCells.Add(cell);
        }
    }

    /// <summary>Gives hand-painted cells back to the features: those inside <paramref name="area"/> (splat grid
    /// cells, as x + y × resolution), or all when null. Returns how many.</summary>
    public int ReleaseHandPainted(RectInt? area)
    {
        int before = handPaintedCells.Count;
        if (area.HasValue)
        {
            RectInt rect = area.Value;
            int resolution = Mathf.Max(1, alphaResolution);
            handPaintedCells.RemoveAll(cell => rect.Contains(new Vector2Int(cell % resolution, cell / resolution)));
        }
        else
        {
            handPaintedCells.Clear();
        }

        handPainted = null;
        return before - handPaintedCells.Count;
    }

    public bool TryGetAlpha(int cell, float[] baseWeights, float[] lastWeights)
    {
        alphaIndex ??= Index(alphaCells);
        if (!alphaIndex.TryGetValue(cell, out int i))
        {
            return false;
        }

        for (int l = 0; l < alphaLayers; l++)
        {
            baseWeights[l] = alphaBase[i * alphaLayers + l] / 255f;
            lastWeights[l] = alphaLast[i * alphaLayers + l] / 255f;
        }

        return true;
    }

    public void SetAlpha(int cell, float[] baseWeights, float[] lastWeights)
    {
        alphaIndex ??= Index(alphaCells);
        if (!alphaIndex.TryGetValue(cell, out int i))
        {
            i = alphaCells.Count;
            alphaIndex[cell] = i;
            alphaCells.Add(cell);
            for (int l = 0; l < alphaLayers; l++)
            {
                alphaBase.Add(0);
                alphaLast.Add(0);
            }
        }

        for (int l = 0; l < alphaLayers; l++)
        {
            alphaBase[i * alphaLayers + l] = ToByte(baseWeights[l]);
            alphaLast[i * alphaLayers + l] = ToByte(lastWeights[l]);
        }
    }

    public void RemoveAlphas(ICollection<int> cells)
    {
        var keepCells = new List<int>();
        var keepBase = new List<byte>();
        var keepLast = new List<byte>();
        for (int i = 0; i < alphaCells.Count; i++)
        {
            if (cells.Contains(alphaCells[i]))
            {
                continue;
            }

            keepCells.Add(alphaCells[i]);
            for (int l = 0; l < alphaLayers; l++)
            {
                keepBase.Add(alphaBase[i * alphaLayers + l]);
                keepLast.Add(alphaLast[i * alphaLayers + l]);
            }
        }

        alphaCells = keepCells;
        alphaBase = keepBase;
        alphaLast = keepLast;
        alphaIndex = null;
    }

    // ------------------------------------------------------------------ grass and other details

    public IEnumerable<int> DetailCells => detailCells;
    public int DetailResolution => detailResolution;

    /// <summary>A detail cell's <see cref="TryGetDetail"/> "last" when it was changed by hand after a build: builds leave
    /// it as it is (hand paint wins) until it is given back.</summary>
    public const int HandKept = -1;

    public bool TryGetDetail(int key, out int baseValue, out int last)
    {
        detailIndex ??= Index(detailCells);
        if (detailIndex.TryGetValue(key, out int i))
        {
            baseValue = detailBase[i];
            last = i < detailLast.Count ? detailLast[i] : 0;
            return true;
        }

        baseValue = last = 0;
        return false;
    }

    public void SetDetail(int key, int baseValue, int last)
    {
        detailIndex ??= Index(detailCells);
        if (detailIndex.TryGetValue(key, out int i))
        {
            detailBase[i] = baseValue;
            detailLast[i] = last;
            return;
        }

        detailIndex[key] = detailCells.Count;
        detailCells.Add(key);
        detailBase.Add(baseValue);
        detailLast.Add(last);
    }

    public void RemoveDetails(ICollection<int> keys)
    {
        RemoveWhere(detailCells, key => keys.Contains(key), detailBase, detailLast);
        detailIndex = null;
    }

    public int HandKeptDetailCount => detailLast.Count(l => l == HandKept);

    /// <summary>Lets the features grow or clear the grass touched by hand again (inside <paramref name="area"/>, a detail
    /// grid rectangle, or everywhere): what is there now becomes the ground they work on. Returns how many cells.</summary>
    public int ReleaseHandKeptDetails(RectInt? area)
    {
        int square = detailResolution * detailResolution;
        var released = new HashSet<int>();
        for (int i = 0; i < detailCells.Count; i++)
        {
            if (i >= detailLast.Count || detailLast[i] != HandKept) continue;
            int cell = square > 0 ? detailCells[i] % square : 0;
            if (area.HasValue && !area.Value.Contains(new Vector2Int(cell % detailResolution, cell / detailResolution))) continue;
            released.Add(detailCells[i]);
        }

        // Forgotten, so the next build takes the grass there now as the ground it works on.
        RemoveDetails(released);
        return released.Count;
    }

    // ------------------------------------------------------------------ helpers

    public static byte ToByte(float weight) => (byte)Mathf.Clamp(Mathf.RoundToInt(weight * 255f), 0, 255);

    private void Relayer(int layers)
    {
        if (alphaLayers > 0 && alphaCells.Count > 0)
        {
            var newBase = new List<byte>(alphaCells.Count * layers);
            var newLast = new List<byte>(alphaCells.Count * layers);
            for (int i = 0; i < alphaCells.Count; i++)
            {
                for (int l = 0; l < layers; l++)
                {
                    newBase.Add(l < alphaLayers ? alphaBase[i * alphaLayers + l] : (byte)0);
                    newLast.Add(l < alphaLayers ? alphaLast[i * alphaLayers + l] : (byte)0);
                }
            }

            alphaBase = newBase;
            alphaLast = newLast;
        }
        else
        {
            alphaCells.Clear();
            alphaBase.Clear();
            alphaLast.Clear();
            alphaIndex = null;
        }

        alphaLayers = layers;
    }

    private static Dictionary<int, int> Index(List<int> cells)
    {
        var index = new Dictionary<int, int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            index[cells[i]] = i;
        }

        return index;
    }

    private static void RemoveWhere<T>(List<int> cells, Predicate<int> doomed, List<T> a, List<T> b = null)
    {
        int write = 0;
        for (int read = 0; read < cells.Count; read++)
        {
            if (doomed(cells[read]))
            {
                continue;
            }

            cells[write] = cells[read];
            a[write] = a[read];
            if (b != null)
            {
                b[write] = b[read];
            }

            write++;
        }

        cells.RemoveRange(write, cells.Count - write);
        a.RemoveRange(write, a.Count - write);
        b?.RemoveRange(write, b.Count - write);
    }
}
