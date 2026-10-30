using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The arc drawn along a weapon swing: a flat ring sector the size of the attack's reach and width, swept by a bright
/// head with a fading tail ("Safe Haven/Slash" shader). Arcs are pooled and their meshes shared per shape, so a fight
/// makes no garbage. CombatFeedback draws one for each of the player's swings; tune them in Combat Feel › Slash arcs and
/// on each attack (Slash Color, Sweep and Tilt).
/// <para>
/// Drawn with Graphics.RenderMesh each frame rather than a MeshRenderer: with the GPU Resident Drawer on, a renderer made
/// at runtime whose (non-DOTS) shader is animated through a MaterialPropertyBlock was not drawn at all.
/// </para>
/// </summary>
public sealed class SlashEffect : MonoBehaviour
{
    private const int Segments = 28;
    private const float InnerShare = 0.3f;
    // How far past the end the head travels so the tail can leave the arc too.
    private const float Overshoot = 0.6f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();
    private static readonly Stack<SlashEffect> Idle = new Stack<SlashEffect>();

    private Mesh mesh;
    private Material material;
    private MaterialPropertyBlock block;
    private float reach;
    private float startTime;
    private float seconds;
    private Color color;

    /// <summary>Draws an arc at <paramref name="origin"/> facing <paramref name="direction"/>; <paramref name="tilt"/> rolls
    /// it (degrees) for diagonal swings and <paramref name="reverse"/> sweeps it the other way.</summary>
    public static void Play(Material material, Vector3 origin, Vector3 direction, float range, float arc, Color color, float seconds,
        float tilt, bool reverse)
    {
        if (material == null || color.a <= 0f || range <= 0f)
        {
            return;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-6f)
        {
            return;
        }

        SlashEffect slash = Take(material);
        slash.mesh = MeshFor(Mathf.Clamp(arc, 10f, 360f), range, reverse);
        slash.reach = range;
        slash.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, tilt));
        slash.color = color;
        slash.seconds = Mathf.Max(0.02f, seconds);
        slash.startTime = Time.time;
        slash.gameObject.SetActive(true);
        slash.Apply(0f);
    }

    private static SlashEffect Take(Material material)
    {
        while (Idle.Count > 0)
        {
            SlashEffect pooled = Idle.Pop();
            if (pooled != null)
            {
                pooled.material = material;
                return pooled;
            }
        }

        var slash = new GameObject("Slash").AddComponent<SlashEffect>();
        slash.material = material;
        slash.block = new MaterialPropertyBlock();
        return slash;
    }

    private void Update()
    {
        float t = (Time.time - startTime) / seconds;
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            Idle.Push(this);
            return;
        }

        Apply(t);
        var draw = new RenderParams(material)
        {
            matProps = block,
            layer = gameObject.layer,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(transform.position, Vector3.one * (reach * 2f + 1f)),
        };
        Graphics.RenderMesh(draw, mesh, 0, transform.localToWorldMatrix);
    }

    private void Apply(float t)
    {
        block.SetColor(ColorId, color);
        block.SetFloat(ProgressId, t * (1f + Overshoot));
        block.SetFloat(FadeId, 1f - Mathf.SmoothStep(0.6f, 1f, t));
    }

    /// <summary>A flat ring sector in the XZ plane, centred on +Z: uv.x along the sweep, uv.y from inner to outer edge.</summary>
    public static Mesh MeshFor(float arc, float range, bool reverse)
    {
        int key = Mathf.RoundToInt(arc) * 10000 + Mathf.RoundToInt(range * 20f) * 2 + (reverse ? 1 : 0);
        if (Meshes.TryGetValue(key, out Mesh cached) && cached != null)
        {
            return cached;
        }

        var vertices = new Vector3[(Segments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[Segments * 6];
        float inner = range * InnerShare;
        for (int i = 0; i <= Segments; i++)
        {
            float u = i / (float)Segments;
            float angle = Mathf.Deg2Rad * Mathf.Lerp(-arc * 0.5f, arc * 0.5f, reverse ? 1f - u : u);
            var along = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            vertices[i * 2] = along * inner;
            vertices[i * 2 + 1] = along * range;
            uvs[i * 2] = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);
            if (i < Segments)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
        }

        var mesh = new Mesh { name = $"Slash {arc:0}° {range:0.0} m", vertices = vertices, uv = uvs, triangles = triangles };
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.DontSave;
        Meshes[key] = mesh;
        return mesh;
    }
}
