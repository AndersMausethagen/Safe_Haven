using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a set of lines picks the next one.</summary>
public enum LineOrder
{
    /// <summary>First to last, then from the start again.</summary>
    InOrder,
    /// <summary>First to last, then keeps saying the last one.</summary>
    InOrderThenLast,
    /// <summary>Any line, never the same one twice in a row.</summary>
    Random,
}

/// <summary>
/// A few interchangeable lines for something said again and again (a giver's "come back later"), so it doesn't repeat
/// itself. Holds which line comes next; the lines themselves are authored data.
/// </summary>
[Serializable]
public sealed class DialogueLines
{
    [SerializeField, TextArea(1, 3)] private List<string> lines = new List<string>();
    [SerializeField] private LineOrder order = LineOrder.InOrder;

    [NonSerialized] private int next;
    [NonSerialized] private int last = -1;

    public DialogueLines()
    {
    }

    public DialogueLines(LineOrder order, params string[] lines)
    {
        this.order = order;
        this.lines = new List<string>(lines);
    }

    public bool HasAny
    {
        get
        {
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The line said now; stays the same until Advance.</summary>
    public string Current => last >= 0 && last < lines.Count ? lines[last] : Advance();

    /// <summary>Moves to the next line and returns it, or null when there are none. Pick(count) returns a value in [0, count).</summary>
    public string Advance(Func<int, int> pick = null)
    {
        var usable = new List<int>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                usable.Add(i);
            }
        }

        if (usable.Count == 0)
        {
            last = -1;
            return null;
        }

        switch (order)
        {
            case LineOrder.Random:
                if (usable.Count > 1)
                {
                    usable.Remove(last);
                }

                int index = pick != null ? pick(usable.Count) : UnityEngine.Random.Range(0, usable.Count);
                last = usable[Mathf.Clamp(index, 0, usable.Count - 1)];
                break;
            case LineOrder.InOrderThenLast:
                last = usable[Mathf.Min(next, usable.Count - 1)];
                next++;
                break;
            default:
                last = usable[next % usable.Count];
                next++;
                break;
        }

        return lines[last];
    }
}
