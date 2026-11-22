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

    public KeeperRings(Transform owner)
    {
        var holder = new GameObject("Keeper's View Rings");
        holder.transform.SetParent(owner, false);
        root = holder.transform;
    }

    /// <summary>Draws <paramref name="rings"/> and, when <paramref name="ghostRadius"/> is above 0, the one being drawn at <paramref name="ghost"/>.</summary>
    public void Show(IReadOnlyList<RingOrder> rings, Vector3 ghost, float ghostRadius)
    {
        int used = 0;
        Color gold = UIStyle.Current.Accent;
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
