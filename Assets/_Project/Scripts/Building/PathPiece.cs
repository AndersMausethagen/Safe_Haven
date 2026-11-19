using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One piece of a path the player drew from the build menu (Paths tab: a dirt path for nothing, a stone path for Stone
/// Blocks), laid end to end like a fence (BuildPlacer.Lines). What it looks like is a decal along the piece
/// (BuildingDefinition.PathDecal), so nothing is written into the terrain; what it does is HavenPaths' (walking faster at
/// home) and HomeQuality's (a paved lane by a door). Its box mesh only gives the piece its size for placing and is never
/// drawn. It blocks nothing: no collider, no carving.
/// <para>Where it belongs: on the path prefabs (Prefabs/Structures/Dirt Path, Stone Path), with BuildingObject and SaveableEntity.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingObject))]
public sealed class PathPiece : MonoBehaviour
{
    private const float Depth = 1.6f;
    // On terrain the decal lies along the ground's slope, this deep (half above, half below): URP's angle fade needs decal layers,
    // which are off, so a deeper box printed the path on whoever stood on it (the user, 2026-11-19), up to their waist.
    private const float GroundDepth = 0.5f;
    private static readonly List<PathPiece> all = new List<PathPiece>();

    private BuildingObject building;
    private DecalProjector decal;

    /// <summary>Every path piece standing in the scene.</summary>
    public static IReadOnlyList<PathPiece> All => all;

    public BuildingDefinition Definition => building != null ? building.Definition : null;

    /// <summary>How much faster the player walks on it.</summary>
    public float Speed => Definition != null ? Definition.PathSpeed : 1f;

    public bool Paved => Definition != null && Definition.Paved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    private void Awake()
    {
        building = GetComponent<BuildingObject>();
        foreach (Renderer shape in GetComponentsInChildren<Renderer>())
        {
            shape.forceRenderingOff = true;
            shape.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Lay();
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable() => all.Remove(this);

    /// <summary>True when <paramref name="point"/> (on the ground) is on this piece, with <paramref name="margin"/> metres to spare.</summary>
    public bool Covers(Vector3 point, float margin = 0.15f)
    {
        FootprintRect footprint = building.Footprint;
        Vector2 offset = new Vector2(point.x, point.z) - footprint.Center;
        return Mathf.Abs(Vector2.Dot(offset, footprint.AxisX)) <= footprint.HalfSize.x + margin
            && Mathf.Abs(Vector2.Dot(offset, footprint.AxisZ)) <= footprint.HalfSize.y + margin;
    }

    /// <summary>Its two ends on the ground, for a paved lane past a door and a path to a yard's gate.</summary>
    public void Ends(out Vector2 a, out Vector2 b)
    {
        BuildPlacement.LineEnds(transform.position, transform.eulerAngles.y, building.LocalBounds, out Vector3 start, out Vector3 end);
        a = new Vector2(start.x, start.z);
        b = new Vector2(end.x, end.z);
    }

    // The look: one decal down onto the ground along the piece, a little longer than it so pieces laid end to end meet.
    private void Lay()
    {
        Material look = Definition != null ? Definition.PathDecal : null;
        if (look == null || decal != null)
        {
            return;
        }

        Bounds bounds = building.LocalBounds;
        bool alongX = BuildPlacement.RunsAlongX(bounds);
        float length = BuildPlacement.PieceLength(bounds) * 1.08f;
        float width = alongX ? bounds.size.z : bounds.size.x;
        var holder = new GameObject("Path Decal");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y + Depth * 0.4f, bounds.center.z);
        // Looking down; the texture's length (u) along the piece.
        holder.transform.localRotation = Quaternion.LookRotation(Vector3.down, alongX ? Vector3.forward : Vector3.left);
        float depth = Depth;
        if (AlongTheGround(holder.transform, length, out float bulge))
        {
            depth = GroundDepth + bulge * 2f;
        }

        decal = holder.AddComponent<DecalProjector>();
        decal.material = look;
        decal.scaleMode = DecalScaleMode.ScaleInvariant;
        decal.size = new Vector3(length, width, depth);
        decal.pivot = Vector3.zero;
        decal.drawDistance = 90f;
        decal.fadeFactor = 1f;
        decal.startAngleFade = 50f;
        decal.endAngleFade = 75f;
    }

    /// <summary>
    /// Lays the decal's holder along the terrain under the piece: its middle on the line between the ground at its two ends, its
    /// length (x) tilted with that line, projecting down square to it. <paramref name="bulge"/> is how far the ground at its middle
    /// lies off that line, for the box's depth. False off terrain: the holder stays as it was, level.
    /// </summary>
    private bool AlongTheGround(Transform holder, float length, out float bulge)
    {
        bulge = 0f;
        Vector3 along = holder.right;
        along.y = 0f;
        if (along.sqrMagnitude < 1e-6f)
        {
            return false;
        }

        along.Normalize();
        Vector3 middle = holder.position;
        float half = length * 0.5f;
        if (!GroundAt(middle - along * half, out float low) || !GroundAt(middle + along * half, out float high) || !GroundAt(middle, out float centre))
        {
            return false;
        }

        Vector3 a = new Vector3(middle.x, low, middle.z) - along * half;
        Vector3 b = new Vector3(middle.x, high, middle.z) + along * half;
        Vector3 line = (b - a).normalized;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, line).normalized;
        Vector3 down = -up;
        // LookRotation's right is up x forward: this up keeps the decal's length (x) along the line.
        holder.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(down, Vector3.Cross(down, line)));
        bulge = Mathf.Abs(centre - (low + high) * 0.5f);
        return true;
    }

    private static bool GroundAt(Vector3 point, out float height)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
            {
                height = terrain.SampleHeight(point) + terrain.transform.position.y;
                return true;
            }
        }

        height = 0f;
        return false;
    }
}
