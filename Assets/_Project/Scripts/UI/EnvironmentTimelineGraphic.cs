using UnityEngine;
using UnityEngine.UI;

/// <summary>A thin 24-hour track, phase boundaries and a moving clock dot; no textures or font glyphs needed.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EnvironmentTimelineGraphic : MaskableGraphic
{
    [SerializeField] private Color nightColor = new Color(0.43f, 0.5f, 0.7f);
    [SerializeField] private Color dawnColor = new Color(0.87f, 0.63f, 0.51f);
    [SerializeField] private Color dayColor = new Color(0.91f, 0.77f, 0.42f);
    [SerializeField] private Color duskColor = new Color(0.79f, 0.48f, 0.35f);

    private float hour = 9f;
    private float sunrise = 6f;
    private float sunset = 20f;

    public float NormalizedHour => Mathf.Repeat(hour, 24f) / 24f;

    public void SetTime(float value, float rise, float set)
    {
        if (hour == value && sunrise == rise && sunset == set)
        {
            return;
        }

        hour = value;
        sunrise = rise;
        sunset = set;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float y = rect.center.y;
        for (int i = 0; i < 5; i++)
        {
            float left = rect.xMin + rect.width * EnvironmentHudView.Boundary(i, sunrise, sunset) / 24f;
            float right = rect.xMin + rect.width * EnvironmentHudView.Boundary(i + 1, sunrise, sunset) / 24f;
            Color tint = i == 1 ? dawnColor : i == 2 ? dayColor : i == 3 ? duskColor : nightColor;
            Quad(mesh, left, y - 1f, right, y + 1f, tint);
            Quad(mesh, left - 0.5f, y - 4f, left + 0.5f, y + 4f, color);
        }

        Quad(mesh, rect.xMax - 0.5f, y - 4f, rect.xMax + 0.5f, y + 4f, color);
        float x = rect.xMin + rect.width * NormalizedHour;
        Circle(mesh, new Vector2(x, y), 7f, new Color(color.r, color.g, color.b, 0.16f));
        Circle(mesh, new Vector2(x, y), 3.5f, color);
    }

    internal static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
        mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
        mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
        mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }

    internal static void Circle(VertexHelper mesh, Vector2 center, float radius, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(center, tint, Vector2.zero);
        const int sides = 24;
        for (int i = 0; i <= sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            if (i > 0)
            {
                mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
