using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Warnings drawn on the ground itself (Docs/LookAndFeelResearch_2026-11-18.md V1): a strike's area and fill, a lob's landing, a
/// lingering hazard, the opening ring at a spent foe's feet, the Hunter's Mark, the backstab zone. With CombatFeel › Ground Mark
/// Material set (Shaders/GroundTelegraph.shader), each is a box (<see cref="Box"/>) the shader paints its shape from onto whatever
/// surface lies inside it, so a fan follows a slope or a step instead of clipping into the ground; its shape (arc, ring, rim, pattern)
/// rides on the renderer's property block (<see cref="Shape"/>). Without that material, or on a pipeline with no depth texture, the
/// flat fans and bands of before are drawn with Telegraph Material.
/// <para>A mark's x and z scale are its radius; its y scale stays 1, so the box reaches <see cref="Reach"/> metres up and down.</para>
/// </summary>
public static class GroundMarks
{
    /// <summary>Metres a projected mark reaches above and below its centre: slopes and steps within it take the mark.</summary>
    public const float Reach = 2.5f;

    /// <summary>The pattern that tells a mark's kind without its colour (Game Accessibility Guidelines: colour backs shape up).</summary>
    public enum Pattern
    {
        /// <summary>A solid rim: an ordinary strike.</summary>
        Solid = 0,
        /// <summary>A dashed rim: a strike any blow breaks off.</summary>
        Dashed = 1,
        /// <summary>Hatched: no guard stops it (an unblockable strike, a lingering hazard). Get out.</summary>
        Hatched = 2,
    }

    private static readonly int ArcId = Shader.PropertyToID("_Arc");
    private static readonly int InnerId = Shader.PropertyToID("_Inner");
    private static readonly int RimId = Shader.PropertyToID("_Rim");
    private static readonly int PatternId = Shader.PropertyToID("_Pattern");
    private static readonly int GradientId = Shader.PropertyToID("_Gradient");
    private static readonly int FrontId = Shader.PropertyToID("_Front");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int HeightId = Shader.PropertyToID("_Height");
    private static readonly int ContrastId = Shader.PropertyToID("_TelegraphContrast");
    private static readonly int HeightmapId = Shader.PropertyToID("_GroundMarkHeightmap");
    private static readonly int PlaceId = Shader.PropertyToID("_GroundMarkPlace");
    private static readonly int GroundScaleId = Shader.PropertyToID("_GroundMarkScale");
    // A heightmap texture holds the terrain's 0-1 heights as 0 to this (Unity's kMaxHeight).
    private const float HeightmapTop = 32766f / 65535f;
    private static Mesh box;
    private static Terrain boundTerrain;
    private static Texture boundHeightmap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        boundTerrain = null;
        boundHeightmap = null;
        Shader.SetGlobalVector(GroundScaleId, Vector4.zero);
    }

    /// <summary>The terrain under a mark about to show, for the shader: what stands more than half a metre above its ground (a
    /// character, a tall rock's top) never takes the mark, only the ground and what lies low on it. Cheap when it is the terrain bound
    /// last; with no terrain under the point, only the slope keeps marks off what stands.</summary>
    public static void BindGround(Vector3 at)
    {
        Terrain terrain = TerrainUnder(at);
        Texture heightmap = terrain != null && terrain.terrainData != null ? terrain.terrainData.heightmapTexture : null;
        if (heightmap != null && terrain == boundTerrain && heightmap == boundHeightmap)
        {
            return;
        }

        boundTerrain = heightmap != null ? terrain : null;
        boundHeightmap = heightmap;
        if (heightmap == null)
        {
            // None under it (or the last one bound is gone with its scene): the slope alone keeps marks off what stands.
            Shader.SetGlobalVector(GroundScaleId, Vector4.zero);
            return;
        }

        TerrainData data = terrain.terrainData;
        Vector3 corner = terrain.GetPosition();
        Vector3 size = data.size;
        float resolution = data.heightmapResolution;
        Shader.SetGlobalTexture(HeightmapId, heightmap);
        // As the terrain's samples sit on its corners: uv = (xz - corner) * (resolution - 1) / (resolution * size) + half a texel.
        Shader.SetGlobalVector(PlaceId, new Vector4(corner.x, corner.z, (resolution - 1f) / (resolution * size.x), (resolution - 1f) / (resolution * size.z)));
        Shader.SetGlobalVector(GroundScaleId, new Vector4(corner.y, size.y / HeightmapTop, 0.5f / resolution, 1f));
    }

    private static Terrain TerrainUnder(Vector3 at)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 corner = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            if (at.x >= corner.x && at.x <= corner.x + size.x && at.z >= corner.z && at.z <= corner.z + size.z)
            {
                return terrain;
            }
        }

        return null;
    }

    /// <summary>Whether marks are projected onto the ground (the material is set and the pipeline draws a depth texture).</summary>
    public static bool Projected(CombatFeelSettings feel) =>
        feel != null && feel.GroundMarkMaterial != null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp
        && urp.supportsCameraDepthTexture;

    /// <summary>The material a mark is drawn with: the projected one, or the flat Telegraph Material.</summary>
    public static Material MaterialFor(CombatFeelSettings feel) =>
        feel == null ? null : Projected(feel) ? feel.GroundMarkMaterial : feel.TelegraphMaterial;

    /// <summary>A fan or disc's mesh: the box when projected, else the flat fan of <paramref name="arc"/> degrees.</summary>
    public static Mesh FanFor(CombatFeelSettings feel, float arc) => Projected(feel) ? Box : AttackTelegraph.FanFor(arc);

    /// <summary>A band's mesh (an arc's outer edge, a ring): the box when projected, else the flat band.</summary>
    public static Mesh BandFor(CombatFeelSettings feel, float arc, float width) => Projected(feel) ? Box : AttackTelegraph.BandFor(arc, width);

    /// <summary>The scale a mark of this radius takes: x and z the radius, y 1 (the box's reach is in metres).</summary>
    public static Vector3 Scale(float radius) => new Vector3(radius, 1f, radius);

    /// <summary>A mark's shape on its property block (the projected shader reads it; the flat fans ignore it). <paramref name="inner"/>
    /// is the ring's inner edge as a share of the radius (0: a fan or disc), <paramref name="rim"/> the bright rim in metres (0: none),
    /// <paramref name="gradient"/> how much dimmer its middle is than its edge, <paramref name="front"/> how much a fill's front glows.</summary>
    public static void Shape(MaterialPropertyBlock block, float arc, float inner, float rim, Pattern pattern, float gradient, float front = 0f)
    {
        block.SetFloat(ArcId, Mathf.Clamp(arc, 1f, 360f));
        block.SetFloat(InnerId, Mathf.Clamp(inner, 0f, 0.99f));
        block.SetFloat(RimId, Mathf.Max(0f, rim));
        block.SetFloat(PatternId, (float)pattern);
        block.SetFloat(GradientId, Mathf.Clamp01(gradient));
        block.SetFloat(FrontId, Mathf.Clamp01(front));
        block.SetFloat(FlashId, 0f);
        block.SetFloat(HeightId, Reach);
    }

    /// <summary>The flash as a strike lands (1 at the hit, fading to 0).</summary>
    public static void Flash(MaterialPropertyBlock block, float flash) => block.SetFloat(FlashId, Mathf.Clamp01(flash));

    /// <summary>Options › High-Contrast Warnings: wider rims with a dark line inside them, stronger fills.</summary>
    public static void SetHighContrast(bool on) => Shader.SetGlobalFloat(ContrastId, on ? 1f : 0f);

    /// <summary>The box a projected mark is drawn on: x and z from -1 to 1 (its radius), y from -<see cref="Reach"/> to
    /// <see cref="Reach"/> metres. One for every mark.</summary>
    public static Mesh Box
    {
        get
        {
            if (box == null)
            {
                box = MakeBox();
            }

            return box;
        }
    }

    private static Mesh MakeBox()
    {
        const float h = Reach;
        var vertices = new[]
        {
            new Vector3(-1f, -h, -1f), new Vector3(1f, -h, -1f), new Vector3(1f, h, -1f), new Vector3(-1f, h, -1f),
            new Vector3(-1f, -h, 1f), new Vector3(1f, -h, 1f), new Vector3(1f, h, 1f), new Vector3(-1f, h, 1f),
        };
        // Wound clockwise seen from outside (Unity's front faces); the shader draws the far faces (Cull Front).
        var triangles = new[]
        {
            0, 3, 2, 0, 2, 1, // -z
            5, 6, 7, 5, 7, 4, // +z
            4, 7, 3, 4, 3, 0, // -x
            1, 2, 6, 1, 6, 5, // +x
            3, 7, 6, 3, 6, 2, // +y
            1, 5, 4, 1, 4, 0, // -y
        };
        var mesh = new Mesh { name = "Ground Mark Box", vertices = vertices, triangles = triangles, hideFlags = HideFlags.DontSave };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
