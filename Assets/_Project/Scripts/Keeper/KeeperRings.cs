using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rings of the Keeper's view on the ground (RingWork): each ring drawn for the woodcutters, and the one being drawn, as a thin
/// gold line laid over the terrain. Only while the view is open. A plain helper owned by KeeperView; its lines live under the portal.
/// </summary>
public sealed class KeeperRings
{
    private const int Points = 56;
    private const float Lift = 0.35f;
    private static Material lineMaterial;
    private readonly Transform root;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly Color? tint;

    public KeeperRings(Transform owner) : this(owner, null, "Keeper's View Rings")
    {
    }

    /// <summary>Lines in <paramref name="colour"/> (the style's accent when null), under a holder called <paramref name="name"/>.</summary>
    public KeeperRings(Transform owner, Color? colour, string name)
    {
        tint = colour;
        var holder = new GameObject(name);
        holder.transform.SetParent(owner, false);
        root = holder.transform;
    }

    /// <summary>Draws <paramref name="rings"/> and, when <paramref name="ghostRadius"/> is above 0, the one being drawn at <paramref name="ghost"/>.</summary>
    public void Show(IReadOnlyList<RingOrder> rings, Vector3 ghost, float ghostRadius)
    {
        int used = 0;
        Color gold = tint ?? UIStyle.Current.Accent;
        for (int i = 0; rings != null && i < rings.Count; i++)
        {
            Draw(Line(used++), rings[i].Centre, rings[i].Radius, gold);
        }

        if (ghostRadius > 0f)
        {
            Draw(Line(used++), ghost, ghostRadius, new Color(gold.r, gold.g, gold.b, 0.6f));
        }

        for (int i = used; i < lines.Count; i++)
        {
            lines[i].enabled = false;
        }
    }

    /// <summary>Draws a small ring of <paramref name="radius"/> at each of <paramref name="points"/>, and the one being set when shown.</summary>
    public void ShowPoints(IReadOnlyList<Vector3> points, float radius, Vector3 ghost, bool ghostShown)
    {
        int used = 0;
        Color colour = tint ?? UIStyle.Current.Accent;
        for (int i = 0; points != null && i < points.Count; i++)
        {
            Draw(Line(used++), points[i], radius, colour);
        }

        if (ghostShown)
        {
            Draw(Line(used++), ghost, radius, new Color(colour.r, colour.g, colour.b, 0.6f));
        }

        for (int i = used; i < lines.Count; i++)
        {
            lines[i].enabled = false;
        }
    }

    /// <summary>Hides every line (the view closing).</summary>
    public void Hide()
    {
        foreach (LineRenderer line in lines)
        {
            line.enabled = false;
        }
    }

    private LineRenderer Line(int index)
    {
        while (lines.Count <= index)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(root, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = Points;
            line.widthMultiplier = 0.22f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            }

            line.sharedMaterial = lineMaterial;
            lines.Add(line);
        }

        lines[index].enabled = true;
        return lines[index];
    }

    private static void Draw(LineRenderer line, Vector3 centre, float radius, Color colour)
    {
        line.startColor = line.endColor = colour;
        for (int i = 0; i < Points; i++)
        {
            float angle = i * Mathf.PI * 2f / Points;
            Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            point = KeeperView.Ground(point);
            point.y += Lift;
            line.SetPosition(i, point);
        }
    }
}
