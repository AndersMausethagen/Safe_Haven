using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector weather symbols, classified from live conditions rather than asset names.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WeatherIconGraphic : MaskableGraphic
{
    public enum Symbol { Sun, Moon, Wind, Mist, Cloud, Rain, Storm }

    private Symbol symbol;
    public Symbol CurrentSymbol => symbol;

    public void SetWeather(WeatherState weather, bool night)
    {
        Symbol next = Classify(weather, night);
        if (next != symbol)
        {
            symbol = next;
            SetVerticesDirty();
        }
    }

    public static Symbol Classify(WeatherState state, bool night)
    {
        if (state.LightningPerMinute > 0.1f) return Symbol.Storm;
        if (state.Rain > 0.1f) return Symbol.Rain;
        if (state.Fog > 1.5f || state.Mist > 0.5f) return Symbol.Mist;
        if (state.Wind > 1.2f) return Symbol.Wind;
        if (state.Clouds > 0.4f) return Symbol.Cloud;
        return night ? Symbol.Moon : Symbol.Sun;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float scale = Mathf.Min(rect.width, rect.height) / 32f;
        switch (symbol)
        {
            case Symbol.Sun:
                Disc(mesh, center, scale, 0f, 0f, 5f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(mesh, center + direction * (8f * scale), center + direction * (12f * scale), 1.6f * scale);
                }
                break;
            case Symbol.Moon:
                // A crescent strip leaves the centre transparent, even over a translucent HUD.
                for (int i = 0; i < 24; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 24f;
                    float b = Mathf.PI * 0.5f + (i + 1) * Mathf.PI / 24f;
                    Vector2 outerA = new Vector2(Mathf.Cos(a) * 10f, Mathf.Sin(a) * 10f);
                    Vector2 outerB = new Vector2(Mathf.Cos(b) * 10f, Mathf.Sin(b) * 10f);
                    Vector2 innerA = new Vector2(Mathf.Cos(a) * 3f + 1f, Mathf.Sin(a) * 10f);
                    Vector2 innerB = new Vector2(Mathf.Cos(b) * 3f + 1f, Mathf.Sin(b) * 10f);
                    Polygon(mesh, center + outerA * scale, center + outerB * scale,
                        center + innerB * scale, center + innerA * scale);
                }
                break;
            case Symbol.Wind:
            case Symbol.Mist:
                for (int i = 0; i < 3; i++)
                {
                    float y = (i - 1) * 6f;
                    Line(mesh, center + new Vector2(-12f + i * 2f, y) * scale,
                        center + new Vector2(10f - i * 2f, y) * scale, 1.8f * scale);
                    if (symbol == Symbol.Wind)
                    {
                        Line(mesh, center + new Vector2(10f - i * 2f, y) * scale,
                            center + new Vector2(7f - i * 2f, y + 3f) * scale, 1.8f * scale);
                    }
                }
                break;
            default:
                Disc(mesh, center, scale, -7f, 1f, 5f);
                Disc(mesh, center, scale, 0f, 5f, 7f);
                Disc(mesh, center, scale, 8f, 1f, 5f);
                EnvironmentTimelineGraphic.Quad(mesh, center.x - 7f * scale, center.y - 4f * scale,
                    center.x + 8f * scale, center.y + 3f * scale, color);
                if (symbol == Symbol.Storm)
                {
                    Line(mesh, center + new Vector2(2f, -4f) * scale, center + new Vector2(-2f, -9f) * scale, 2f * scale);
                    Line(mesh, center + new Vector2(-2f, -9f) * scale, center + new Vector2(3f, -9f) * scale, 2f * scale);
                    Line(mesh, center + new Vector2(3f, -9f) * scale, center + new Vector2(-1f, -14f) * scale, 2f * scale);
                }
                else if (symbol == Symbol.Rain)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Line(mesh, center + new Vector2(-6f + i * 6f, -7f) * scale,
                            center + new Vector2(-8f + i * 6f, -12f) * scale, 1.8f * scale);
                    }
                }
                break;
        }
    }

    private void Disc(VertexHelper mesh, Vector2 center, float scale, float x, float y, float radius)
        => EnvironmentTimelineGraphic.Circle(mesh, center + new Vector2(x, y) * scale, radius * scale, color);

    private void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * (width * 0.5f);
        Polygon(mesh, a - normal, a + normal, b + normal, b - normal);
    }

    private void Polygon(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }
}
