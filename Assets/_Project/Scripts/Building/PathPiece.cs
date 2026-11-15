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
        decal = holder.AddComponent<DecalProjector>();
        decal.material = look;
        decal.scaleMode = DecalScaleMode.ScaleInvariant;
        decal.size = new Vector3(length, width, Depth);
        decal.pivot = Vector3.zero;
        decal.drawDistance = 90f;
        decal.fadeFactor = 1f;
        decal.startAngleFade = 50f;
        decal.endAngleFade = 75f;
    }
}
