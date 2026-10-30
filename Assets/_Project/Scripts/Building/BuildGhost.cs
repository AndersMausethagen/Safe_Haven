using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player sees while placing: a see-through copy of the building (one per piece of a line), green where it may
/// stand and red where not, with its yard drawn in front; and the yards of the buildings already standing. The copy has
/// only the meshes, so nothing on the real building (saving, crafting, health) wakes up. Plain C#, owned by a BuildPlacer.
/// </summary>
public sealed class BuildGhost
{
    private const string YardName = "Yard";
    // Yard markers float this far above the build plane so they don't flicker against the ground.
    private const float MarkerHeight = 0.04f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Material material;
    private readonly Color validColor;
    private readonly Color invalidColor;
    private readonly Color yardColor;
    private readonly Color placedYardColor;
    private readonly List<Piece> pieces = new List<Piece>();
    private readonly List<GameObject> placedYards = new List<GameObject>();
    private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

    private sealed class Piece
    {
        public GameObject Root;
        public Renderer[] Body;
        public Renderer Yard;
        public string Problem;
    }

    public BuildGhost(Material material, Color validColor, Color invalidColor, Color yardColor, Color placedYardColor)
    {
        this.material = material;
        this.validColor = validColor;
        this.invalidColor = invalidColor;
        this.yardColor = yardColor;
        this.placedYardColor = placedYardColor;
    }

    /// <summary>How many pieces are shown.</summary>
    public int Count { get; private set; }

    /// <summary>The building's bounds around its pivot, unturned.</summary>
    public Bounds LocalBounds { get; private set; }

    public bool Exists => pieces.Count > 0;

    /// <summary>One piece, copied from <paramref name="source"/>, with a yard marker when <paramref name="hasYard"/>.
    /// More pieces for a line are copies of this first one.</summary>
    public void Create(Transform source, bool hasYard)
    {
        Clear();
        var ghost = new GameObject("Build Ghost");
        LocalBounds = BuildPlacement.LocalBounds(source);
        Matrix4x4 toRoot = source.worldToLocalMatrix;
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer original) || !original.enabled)
            {
                continue;
            }

            Matrix4x4 relative = toRoot * filter.transform.localToWorldMatrix;
            var part = new GameObject(filter.name);
            part.transform.SetParent(ghost.transform, false);
            part.transform.localPosition = relative.GetColumn(3);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = relative.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (hasYard)
        {
            CreateMarker(YardName).transform.SetParent(ghost.transform, false);
        }

        pieces.Add(Describe(ghost));
        Count = 1;
    }

    /// <summary>Shows <paramref name="count"/> pieces, copying more when a line grows.</summary>
    public void Show(int count)
    {
        while (pieces.Count < count)
        {
            pieces.Add(Describe(Object.Instantiate(pieces[0].Root)));
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].Root.SetActive(i < count);
        }

        Count = count;
    }

    /// <summary>Puts piece <paramref name="index"/> at a spot and tints it by <paramref name="problem"/> (null: fine).
    /// <paramref name="kind"/> gives the yard.</summary>
    public void Place(int index, Vector3 point, float yaw, string problem, BuildingDefinition kind)
    {
        Piece piece = pieces[index];
        piece.Problem = problem;
        piece.Root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        Tint(piece.Body, problem == null ? validColor : invalidColor);
        if (piece.Yard != null && kind != null
            && BuildPlacement.TryGetYard(point, yaw, LocalBounds, kind.YardSide, kind.YardDepth, out FootprintRect yard))
        {
            Lay(piece.Yard.transform, yard, point.y + MarkerHeight);
            Tint(piece.Yard, problem == null ? yardColor : invalidColor * new Color(1f, 1f, 1f, 0.6f));
        }
    }

    /// <summary>Why piece <paramref name="index"/> can't be built where it stands, or null.</summary>
    public string ProblemAt(int index) => pieces[index].Problem;

    public Transform PieceAt(int index) => pieces[index].Root.transform;

    /// <summary>True when <paramref name="collider"/> belongs to the ghost (it never blocks itself).</summary>
    public bool Contains(Collider collider)
    {
        foreach (Piece piece in pieces)
        {
            if (collider.transform.IsChildOf(piece.Root.transform))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Marks the yards of the buildings already standing, so the player sees where not to build.</summary>
    public void ShowPlacedYards(float groundHeight)
    {
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (!building.TryGetYard(out FootprintRect yard))
            {
                continue;
            }

            GameObject marker = CreateMarker("Placed Yard");
            Lay(marker.transform, yard, groundHeight + MarkerHeight);
            Tint(marker.GetComponent<Renderer>(), placedYardColor);
            placedYards.Add(marker);
        }
    }

    /// <summary>Removes the ghost and the yard markers.</summary>
    public void Clear()
    {
        foreach (Piece piece in pieces)
        {
            if (piece.Root != null)
            {
                Object.Destroy(piece.Root);
            }
        }

        foreach (GameObject marker in placedYards)
        {
            if (marker != null)
            {
                Object.Destroy(marker);
            }
        }

        pieces.Clear();
        placedYards.Clear();
        Count = 0;
    }

    private static Piece Describe(GameObject root)
    {
        var piece = new Piece { Root = root };
        var body = new List<Renderer>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == YardName)
            {
                piece.Yard = renderer;
            }
            else
            {
                body.Add(renderer);
            }
        }

        piece.Body = body.ToArray();
        return piece;
    }

    private GameObject CreateMarker(string markerName)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.name = markerName;
        var renderer = marker.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return marker;
    }

    // A quad lies flat (its face up) when tilted a quarter turn about x; its height then runs along the ground's z.
    private static void Lay(Transform marker, FootprintRect area, float height)
    {
        marker.SetPositionAndRotation(area.WorldCenter(height), Quaternion.Euler(90f, area.Yaw, 0f));
        Vector3 parentScale = marker.parent != null ? marker.parent.lossyScale : Vector3.one;
        marker.localScale = new Vector3(area.HalfSize.x * 2f / parentScale.x, area.HalfSize.y * 2f / parentScale.z, 1f);
    }

    private void Tint(Renderer[] renderers, Color color)
    {
        foreach (Renderer renderer in renderers)
        {
            Tint(renderer, color);
        }
    }

    private void Tint(Renderer renderer, Color color)
    {
        block.SetColor(BaseColor, color);
        block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(block);
    }
}
