using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player sees of the town while building (Docs/BuildingSystemResearch.md §3.4, steps 2 and 4): the "Here" readout
/// beside the ghost (BuildHere), circles on the ground for the radii that matter (lights, the Watch Post's bowshot, a Ward
/// Totem's reach, an extension's reach round its station), and every fenced ring with its yard's name and quality, tinted
/// by role. Shown while the build menu is open or something is being placed; nothing at all otherwise. Rings and labels are
/// laid again only when the yards change or the ghost moves; no allocations per frame.
/// <para>Made by BuildPlacer the first time it is needed; one for the local player.</para>
/// </summary>
public sealed class BuildOverlay : MonoBehaviour
{
    private const int CirclePoints = 56;
    private const float RingLift = 0.12f;
    private const float RingWidth = 0.12f;
    private const float RingsWithin = 45f;
    private const int MostLabels = 24;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly Color LightColour = new Color(1f, 0.82f, 0.35f, 0.75f);
    private static readonly Color GuardColour = new Color(0.95f, 0.4f, 0.32f, 0.75f);
    private static readonly Color WardColour = new Color(0.62f, 0.5f, 1f, 0.75f);
    private static readonly Color ReachColour = new Color(0.45f, 0.95f, 0.55f, 0.75f);

    private Material material;
    private readonly List<LineRenderer> circles = new List<LineRenderer>();
    private readonly List<LineRenderer> yardLines = new List<LineRenderer>();
    private readonly List<(TMP_Text label, Vector3 at)> labels = new List<(TMP_Text, Vector3)>();
    private readonly Vector3[] points = new Vector3[CirclePoints];
    private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    private readonly List<Vector3> corners = new List<Vector3>();
    private RectTransform canvasRect;
    private RectTransform panel;
    private TMP_Text readout;
    private bool shown;
    private bool yardsStale = true;
    private Vector3 lastPoint = new Vector3(float.NaN, 0f, 0f);
    private float lastYaw;
    private BuildingDefinition lastKind;
    private Vector3 readoutAt;
    private int usedCircles;

    public static BuildOverlay Create(Material lineMaterial)
    {
        var root = new GameObject("Build Overlay");
        DontDestroyOnLoad(root);
        var overlay = root.AddComponent<BuildOverlay>();
        overlay.material = lineMaterial;
        overlay.BuildUI();
        return overlay;
    }

    private void OnEnable() => Yards.Changed += MarkYards;

    private void OnDisable() => Yards.Changed -= MarkYards;

    private void MarkYards() => yardsStale = true;

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        UIRoot.ConfigureScaler(gameObject.AddComponent<CanvasScaler>());
        canvasRect = (RectTransform)transform;
        UIStyle style = UIStyle.Current;
        panel = HearthKit.Card(transform, "Here", 0f, 0f, 470f, 210f, 0.5f, style.Panel);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0.5f);
        TMP_Text title = CodeUI.Label("Title", panel, 18f, 10f, 300f, 26f, 18f, style.Accent);
        title.text = "HERE";
        title.characterSpacing = 6f;
        readout = CodeUI.Paragraph("Readout", panel, 18f, 38f, 434f, 166f, 16f, style.Text);
        readout.richText = true;
        panel.gameObject.SetActive(false);
    }

    /// <summary>Shows or hides everything (the build menu open, or placing).</summary>
    public void Show(bool visible)
    {
        if (visible == shown)
        {
            return;
        }

        shown = visible;
        if (!visible)
        {
            panel.gameObject.SetActive(false);
            SetCircles(0);
            foreach (LineRenderer line in yardLines)
            {
                line.gameObject.SetActive(false);
            }

            foreach ((TMP_Text label, Vector3 _) in labels)
            {
                label.gameObject.SetActive(false);
            }

            lastKind = null;
            return;
        }

        yardsStale = true;
    }

    /// <summary>The ghost stands at <paramref name="point"/>: the readout and the radii follow it. Null hides the readout.</summary>
    public void Here(BuildingDefinition kind, Vector3 point, float yaw, Bounds local)
    {
        Show(true);
        if (kind == null)
        {
            panel.gameObject.SetActive(false);
            lastKind = null;
            return;
        }

        readoutAt = point;
        panel.gameObject.SetActive(true);
        if (kind == lastKind && (point - lastPoint).sqrMagnitude < 0.01f && Mathf.Abs(yaw - lastYaw) < 0.5f)
        {
            return;
        }

        lastKind = kind;
        lastPoint = point;
        lastYaw = yaw;
        readout.text = BuildHere.Describe(kind, point, yaw, local);
        LayCircles(kind, point);
    }

    /// <summary>Nothing is being placed (the menu is open): radii and the readout go, the yards stay.</summary>
    public void NoGhost()
    {
        Show(true);
        panel.gameObject.SetActive(false);
        if (lastKind != null)
        {
            lastKind = null;
            SetCircles(0);
        }
    }

    private void LateUpdate()
    {
        if (!shown)
        {
            return;
        }

        if (yardsStale)
        {
            yardsStale = false;
            LayYards();
        }

        Camera view = Camera.main;
        if (view == null)
        {
            return;
        }

        Vector2 scale = new Vector2(canvasRect.rect.width / Mathf.Max(1, Screen.width), canvasRect.rect.height / Mathf.Max(1, Screen.height));
        if (panel.gameObject.activeSelf)
        {
            Vector3 screen = view.WorldToScreenPoint(readoutAt + Vector3.up * 1.5f);
            float x = Mathf.Clamp(screen.x * scale.x + 90f, 20f, canvasRect.rect.width - panel.rect.width - 20f);
            float y = Mathf.Clamp(screen.y * scale.y, panel.rect.height * 0.5f + 20f, canvasRect.rect.height - panel.rect.height * 0.5f - 20f);
            panel.anchoredPosition = new Vector2(x, y);
        }

        foreach ((TMP_Text label, Vector3 at) in labels)
        {
            if (!label.gameObject.activeSelf)
            {
                continue;
            }

            Vector3 screen = view.WorldToScreenPoint(at);
            label.enabled = screen.z > 0f;
            label.rectTransform.anchoredPosition = new Vector2(screen.x * scale.x, screen.y * scale.y);
        }
    }

    // ----- Radii -----

    private void LayCircles(BuildingDefinition kind, Vector3 point)
    {
        usedCircles = 0;
        float reach = RingsWithin * RingsWithin;
        foreach (VisibilitySource source in VisibilitySource.Active)
        {
            if (source != null && source.IsLight && source.GetComponentInParent<BuildingObject>() != null
                && Flat(source.transform.position - point).sqrMagnitude < reach)
            {
                Circle(source.transform.position, source.Radius, LightColour);
            }
        }

        foreach (WatchPostArcher post in FindObjectsByType<WatchPostArcher>())
        {
            if (Flat(post.transform.position - point).sqrMagnitude < reach)
            {
                Circle(post.transform.position, post.Reach, GuardColour);
            }
        }

        foreach (WardTotem totem in FindObjectsByType<WardTotem>())
        {
            if (Flat(totem.transform.position - point).sqrMagnitude < reach)
            {
                Circle(totem.transform.position, totem.Radius, WardColour);
            }
        }

        // What the ghost itself would reach, from its prefab.
        GameObject prefab = kind.Prefab;
        if (prefab != null)
        {
            VisibilitySource light = prefab.GetComponentInChildren<VisibilitySource>(true);
            if (light != null && light.IsLight)
            {
                Circle(point, light.Radius, LightColour);
            }

            if (prefab.TryGetComponent(out WatchPostArcher archer))
            {
                Circle(point, archer.Reach, GuardColour);
            }

            if (prefab.TryGetComponent(out WardTotem ward))
            {
                Circle(point, ward.Radius, WardColour);
            }
        }

        // An extension: the reach round each station it could serve.
        if (kind.Extends != null)
        {
            foreach (BuildingObject station in BuildingObject.Active)
            {
                if (station.Definition == kind.Extends && Flat(station.transform.position - point).sqrMagnitude < reach)
                {
                    FootprintRect box = station.Footprint;
                    Circle(station.transform.position, kind.ExtensionReach + Mathf.Max(box.HalfSize.x, box.HalfSize.y), ReachColour);
                }
            }
        }

        SetCircles(usedCircles);
    }

    private void Circle(Vector3 centre, float radius, Color colour)
    {
        if (radius <= 0.1f)
        {
            return;
        }

        LineRenderer line = Line(circles, usedCircles++, "Radius");
        for (int i = 0; i < CirclePoints; i++)
        {
            float angle = i * Mathf.PI * 2f / CirclePoints;
            points[i] = Ground(centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }

        line.positionCount = CirclePoints;
        line.SetPositions(points);
        line.loop = true;
        Tint(line, colour);
    }

    private void SetCircles(int count)
    {
        for (int i = 0; i < circles.Count; i++)
        {
            circles[i].gameObject.SetActive(i < count);
        }
    }

    // ----- Yards -----

    private void LayYards()
    {
        IReadOnlyList<Yards.Yard> yards = Yards.All;
        YardSettings settings = YardSettings.Current;
        int used = 0;
        for (int y = 0; y < yards.Count; y++)
        {
            Yards.Yard yard = yards[y];
            LineRenderer line = Line(yardLines, used++, "Yard");
            corners.Clear();
            List<Vector2> ring = yard.Ring.Corners;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f));
                for (int s = 0; s < steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    // A little inside the fence, so the line shows beside it rather than under it.
                    Vector2 inward = (yard.Ring.Centre - p).normalized * 0.5f;
                    corners.Add(Ground(new Vector3(p.x + inward.x, 0f, p.y + inward.y)));
                }
            }

            line.positionCount = corners.Count;
            for (int i = 0; i < corners.Count; i++)
            {
                line.SetPosition(i, corners[i]);
            }

            line.loop = true;
            line.widthMultiplier = RingWidth * 2f;
            Color colour = yard.HasGate && yard.Role != null ? yard.Role.Colour : settings.PlainColour;
            colour.a = 0.8f;
            Tint(line, colour);

            if (y < MostLabels)
            {
                TMP_Text label = Label(y);
                string name = yard.HasGate && yard.Role != null ? $"<b>{yard.Role.DisplayName}</b>  quality {yard.Quality}/5" : $"<b>{yard.Name}</b>";
                label.text = string.IsNullOrEmpty(yard.Hint) ? name : $"{name}\n<size=80%><color=#{ColorUtility.ToHtmlStringRGB(UIStyle.Current.Muted)}>{yard.Hint}</color></size>";
                label.color = colour;
                labels[y] = (label, Ground(yard.Centre(0f)) + Vector3.up * 0.5f);
                label.gameObject.SetActive(true);
            }
        }

        for (int i = used; i < yardLines.Count; i++)
        {
            yardLines[i].gameObject.SetActive(false);
        }

        for (int i = Mathf.Min(yards.Count, MostLabels); i < labels.Count; i++)
        {
            labels[i].label.gameObject.SetActive(false);
        }
    }

    private TMP_Text Label(int index)
    {
        while (labels.Count <= index)
        {
            UIStyle style = UIStyle.Current;
            TMP_Text label = CodeUI.Label("Yard Label", transform, 0f, 0f, 360f, 60f, 18f, style.Text, TextAlignmentOptions.Center);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            label.richText = true;
            label.raycastTarget = false;
            labels.Add((label, Vector3.zero));
        }

        return labels[index].label;
    }

    // ----- Drawing -----

    private LineRenderer Line(List<LineRenderer> pool, int index, string name)
    {
        while (pool.Count <= index)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            var line = holder.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = RingWidth;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            pool.Add(line);
        }

        LineRenderer found = pool[index];
        found.gameObject.SetActive(true);
        return found;
    }

    private void Tint(Renderer renderer, Color colour)
    {
        block.Clear();
        block.SetColor(BaseColor, colour);
        block.SetColor(ColorId, colour);
        renderer.SetPropertyBlock(block);
        if (renderer is LineRenderer line)
        {
            line.startColor = line.endColor = colour;
        }
    }

    private static Vector3 Ground(Vector3 point)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point + Vector3.up * RingLift;
    }

    private static Vector2 Flat(Vector3 value) => new Vector2(value.x, value.z);
}
