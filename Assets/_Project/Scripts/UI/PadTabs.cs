using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LB and RB turn a window's tabs on a controller (PadCursor reads them while its pointer is up). A window with tabs
/// registers how to step them while it exists; the one registered last of those showing now takes the press, so the
/// Options screen over the Escape menu, or a station over the world, turns its own. The keyboard and mouse pick tabs by
/// clicking, as before.
/// </summary>
public static class PadTabs
{
    private sealed class Entry
    {
        public object Owner;
        public Func<bool> Showing;
        public Action<int> Step;
        public Func<IEnumerable<RectTransform>> Row;
    }

    private static readonly List<Entry> Entries = new List<Entry>();

    /// <summary>
    /// Lets LB (-1) and RB (+1) step this window's tabs while <paramref name="showing"/> says it is up. <paramref name="row"/>
    /// gives the tabs themselves, so the bumpers' glyphs can stand at either end of them (PadCursor).
    /// </summary>
    public static void Register(object owner, Func<bool> showing, Action<int> step, Func<IEnumerable<RectTransform>> row = null)
    {
        Unregister(owner);
        Entries.Add(new Entry { Owner = owner, Showing = showing, Step = step, Row = row });
    }

    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>The front window's tab row on the screen, in pixels: where the bumpers' glyphs go. False when it gave none.</summary>
    public static bool TryGetFrontRow(out Rect screen)
    {
        screen = default;
        Entry front = Front();
        if (front?.Row == null)
        {
            return false;
        }

        bool any = false;
        float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
        foreach (RectTransform tab in front.Row())
        {
            if (tab == null || !tab.gameObject.activeInHierarchy)
            {
                continue;
            }

            Canvas canvas = tab.GetComponentInParent<Canvas>();
            Camera eye = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            tab.GetWorldCorners(Corners);
            foreach (Vector3 corner in Corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(eye, corner);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }

            any = true;
        }

        if (any)
        {
            screen = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        return any;
    }

    public static void Unregister(object owner) => Entries.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));

    /// <summary>True while some window showing has tabs to turn.</summary>
    public static bool Any => Front() != null;

    /// <summary>Turns the front window's tabs one way (-1 left, +1 right). False when no window with tabs is up.</summary>
    public static bool Step(int direction)
    {
        Entry front = Front();
        if (front == null)
        {
            return false;
        }

        front.Step(direction);
        return true;
    }

    /// <summary>The tab <paramref name="direction"/> steps from <paramref name="current"/>, round from the last to the first.</summary>
    public static int Next(int current, int count, int direction)
    {
        return count <= 0 ? 0 : ((current + direction) % count + count) % count;
    }

    private static Entry Front()
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry entry = Entries[i];
            // An owner destroyed without unregistering (a scene unloading) is dropped.
            if (entry.Owner is UnityEngine.Object unityObject && unityObject == null)
            {
                Entries.RemoveAt(i);
                continue;
            }

            if (entry.Showing())
            {
                return entry;
            }
        }

        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Entries.Clear();
}
