using UnityEngine;

/// <summary>
/// The cut opening in a trunk as a tree is chopped. A trunk's mesh can't be carved, so this is a lens of raw wood laid over
/// it, facing the feller and widening with each blow; its two halves slope in towards a line, so the light falls on it as
/// on a real V-shaped cut. It goes when the tree does.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class TreeNotch : MonoBehaviour
{
    private const int Columns = 10;
    private const float StandOff = 0.012f;
    private const float StartDegrees = 35f;
    private const float EndDegrees = 80f;
    private const float StartHeight = 0.1f;
    private const float EndHeight = 0.3f;
    private const float Slope = 0.75f;

    private MeshFilter filter;
    private Mesh mesh;
    private float radius;

    /// <summary>A notch on a trunk of this radius, not cut yet.</summary>
    public static TreeNotch Open(Transform tree, Vector3 position, float radius, Material wood)
    {
        var made = new GameObject("Notch");
        made.transform.SetParent(tree, true);
        made.transform.position = position;
        made.GetComponent<Transform>().localScale = Inverse(tree.lossyScale);
        made.AddComponent<MeshFilter>();
        made.AddComponent<MeshRenderer>().sharedMaterial = wood;
        var notch = made.AddComponent<TreeNotch>();
        notch.filter = made.GetComponent<MeshFilter>();
        notch.radius = radius;
        return notch;
    }

    private static Vector3 Inverse(Vector3 scale) => new Vector3(1f / Mathf.Max(0.01f, scale.x), 1f / Mathf.Max(0.01f, scale.y), 1f / Mathf.Max(0.01f, scale.z));

    /// <summary>Opens the notch to a share of the way through the trunk, on the side facing the blow.</summary>
    public void Widen(float progress, Vector3 from)
    {
        Vector3 toward = from - transform.position;
        toward.y = 0f;
        if (toward.sqrMagnitude > 0.0001f && mesh == null)
        {
            transform.rotation = Quaternion.LookRotation(toward.normalized);
        }

        if (mesh != null)
        {
            Destroy(mesh);
        }

        mesh = Build(radius, progress);
        filter.sharedMesh = mesh;
    }

    /// <summary>The lens for a trunk of this radius and a cut this far through, facing +Z.</summary>
    public static Mesh Build(float radius, float progress)
    {
        progress = Mathf.Clamp01(progress);
        float halfAngle = Mathf.Lerp(StartDegrees, EndDegrees, progress) * Mathf.Deg2Rad * 0.5f;
        float height = Mathf.Lerp(StartHeight, EndHeight, progress) * 0.5f;
        float around = radius + StandOff;
        int rows = 3;
        var vertices = new Vector3[(Columns + 1) * rows];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        for (int column = 0; column <= Columns; column++)
        {
            float along = column / (float)Columns * 2f - 1f;
            float angle = along * halfAngle;
            var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float open = height * Mathf.Cos(along * Mathf.PI * 0.5f);
            for (int row = 0; row < rows; row++)
            {
                int index = column * rows + row;
                float up = row - 1;
                vertices[index] = outward * around + Vector3.up * (up * open);
                // The upper face looks down into the cut and the lower one up, as on a real notch.
                normals[index] = (outward + Vector3.up * (-up * Slope)).normalized;
                uvs[index] = new Vector2(0.5f + along * 0.28f, 0.5f + up * 0.2f);
            }
        }

        var triangles = new int[Columns * (rows - 1) * 6];
        int triangle = 0;
        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < rows - 1; row++)
            {
                int a = column * rows + row;
                int b = a + rows;
                triangles[triangle++] = a;
                triangles[triangle++] = b;
                triangles[triangle++] = a + 1;
                triangles[triangle++] = b;
                triangles[triangle++] = b + 1;
                triangles[triangle++] = a + 1;
            }
        }

        var built = new Mesh { name = "Notch", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
        built.RecalculateBounds();
        built.RecalculateTangents();
        return built;
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }
}
