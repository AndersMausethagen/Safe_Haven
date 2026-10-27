using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One multiplier per spline knot (a river's width, a cliff's height...), interpolated between knots. The
/// World Builder keeps the list in step with the spline: a knot added or removed adds or removes its value, so the
/// others stay with their knots. Missing values count as 1.</summary>
[Serializable]
public sealed class KnotValues
{
    [SerializeField] private List<float> values = new List<float>();

    public int Count => values.Count;

    public float this[int index]
    {
        get => index >= 0 && index < values.Count ? values[index] : 1f;
        set
        {
            Resize(index + 1);
            values[index] = Mathf.Max(0f, value);
        }
    }

    /// <summary>The value at a knot-space position (0 = first knot, 1 = second...; past the last knot it wraps to the
    /// first for closed splines).</summary>
    public float Evaluate(float knotT, int knotCount, bool closed)
    {
        if (knotCount <= 0)
        {
            return 1f;
        }

        float limit = closed ? knotCount : knotCount - 1;
        knotT = Mathf.Clamp(knotT, 0f, limit);
        int a = Mathf.FloorToInt(knotT);
        float f = knotT - a;
        int b = a + 1;
        if (b >= knotCount)
        {
            b = closed ? 0 : knotCount - 1;
        }

        return Mathf.Lerp(this[a % knotCount], this[b], f);
    }

    /// <summary>Grows or shrinks the list to the knot count (new knots get 1).</summary>
    public void Resize(int count)
    {
        while (values.Count < count)
        {
            values.Add(1f);
        }

        if (values.Count > count)
        {
            values.RemoveRange(count, values.Count - count);
        }
    }

    /// <summary>A knot was inserted at <paramref name="index"/>: it takes the average of its neighbours.</summary>
    public void Insert(int index, int knotCountAfter)
    {
        index = Mathf.Clamp(index, 0, values.Count);
        float before = index > 0 ? this[index - 1] : this[0];
        float after = index < values.Count ? this[index] : before;
        values.Insert(index, (before + after) * 0.5f);
        Resize(knotCountAfter);
    }

    /// <summary>The spline was reversed: the values go with their knots.</summary>
    public void Reverse() => values.Reverse();

    public void Remove(int index, int knotCountAfter)
    {
        if (index >= 0 && index < values.Count)
        {
            values.RemoveAt(index);
        }

        Resize(knotCountAfter);
    }
}
