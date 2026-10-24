using UnityEngine;

/// <summary>Builds a waterfall sheet for "Safe Haven/Water": a flat run of water (the lip, joining the river above)
/// that leaves the edge and falls in a natural arc (thrown at <c>throwSpeed</c>, pulled down by gravity) to the pool
/// <c>height</c> metres below. UV are metres (u across, v downhill) so the shader's fall streaks run down it, and
/// vertex alpha fades the sides, the top and the bottom. The mesh is rebuilt when loaded or edited (it is not an
/// asset). <c>plunge</c> (foam, ripples, mist and spray) is moved to where the water lands.
/// Local axes: +Y up, +Z the way the water flows and falls out of the cliff.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class Waterfall : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float width = 3f;
    [SerializeField, Min(0.2f)] private float height = 4f;
    [Tooltip("Flat run of water before the edge (metres), where it joins the river above.")]
    [SerializeField, Min(0f)] private float lip = 1f;
    [Tooltip("How fast the water leaves the edge (m/s): 0 runs straight down the cliff, higher arcs out further.")]
    [SerializeField, Range(0f, 4f)] private float throwSpeed = 1.2f;
    [Tooltip("How much wider the sheet gets as it falls.")]
    [SerializeField, Range(0f, 1f)] private float spread = 0.15f;
    [Tooltip("Metres faded out at the sides, the top and the bottom.")]
    [SerializeField, Range(0.05f, 1f)] private float edgeFade = 0.3f;
    [Tooltip("Moved to where the water lands: the Waterfall Plunge (foam, ripples, mist and spray).")]
    [SerializeField] private Transform plunge;

    private Mesh mesh;

    public float Width => width;
    public float Height => height;
    public Transform Plunge
    {
        get => plunge;
        set => plunge = value;
    }

    /// <summary>Where the water lands, in local space.</summary>
    public Vector3 Landing => new Vector3(0f, 0f, throwSpeed * FallTime(height));

    public void Configure(float sheetWidth, float sheetHeight, float lipLength, float speed)
    {
        width = Mathf.Max(0.2f, sheetWidth);
        height = Mathf.Max(0.2f, sheetHeight);
        lip = Mathf.Max(0f, lipLength);
        throwSpeed = Mathf.Clamp(speed, 0f, 4f);
        Rebuild();
    }

    private void OnEnable() => Rebuild();

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                Rebuild();
            }
        };
#endif
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            DestroyImmediate(mesh);
        }
    }

    public void Rebuild()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "Waterfall", hideFlags = HideFlags.DontSave };
        }

        Build(mesh, width, height, lip, throwSpeed, spread, edgeFade);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (plunge != null)
        {
            plunge.localPosition = Landing;
        }
    }

    private static float FallTime(float drop) => Mathf.Sqrt(2f * drop / 9.81f);

    /// <summary>Fills <paramref name="target"/> with the sheet (see the class summary).</summary>
    public static void Build(Mesh target, float width, float height, float lip, float throwSpeed, float spread, float edgeFade)
    {
        // Path down the middle of the sheet: the flat lip, then the falling arc (uniform in time: denser at the top
        // where it bends).
        var path = new System.Collections.Generic.List<Vector3>();
        var falling = new System.Collections.Generic.List<float>();
        int lipSegments = lip > 0f ? Mathf.Max(1, Mathf.CeilToInt(lip / 0.5f)) : 0;
        for (int i = 0; i < lipSegments; i++)
        {
            path.Add(new Vector3(0f, height, -lip + lip * i / lipSegments));
            falling.Add(0f);
        }

        const int fallSegments = 18;
        float time = FallTime(height);
        for (int i = 0; i <= fallSegments; i++)
        {
            float t = time * i / fallSegments;
            path.Add(new Vector3(0f, Mathf.Max(height - 4.905f * t * t, 0f), throwSpeed * t));
            falling.Add((float)i / fallSegments);
        }

        var length = new float[path.Count];
        for (int i = 1; i < path.Count; i++)
        {
            length[i] = length[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }

        float total = length[path.Count - 1];
        const int columns = 8;
        int rowCount = path.Count;
        var vertices = new Vector3[rowCount * (columns + 1)];
        var uv = new Vector2[vertices.Length];
        var colours = new Color[vertices.Length];
        float half = width * 0.5f;
        for (int row = 0; row < rowCount; row++)
        {
            float widen = 1f + spread * falling[row];
            float topFade = lip > 0f ? Mathf.Clamp01(length[row] / edgeFade) : 1f;
            float bottomFade = Mathf.Clamp01((total - length[row]) / edgeFade);
            for (int column = 0; column <= columns; column++)
            {
                float across = -half + width * column / columns;
                int index = row * (columns + 1) + column;
                vertices[index] = path[row] + new Vector3(across * widen, 0f, 0f);
                uv[index] = new Vector2(across + half, length[row]);
                float side = Mathf.Clamp01(Mathf.Min(across + half, half - across) / edgeFade);
                colours[index] = new Color(1f, 1f, 1f, side * topFade * bottomFade);
            }
        }

        var triangles = new int[(rowCount - 1) * columns * 6];
        int t6 = 0;
        for (int row = 0; row < rowCount - 1; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int a = row * (columns + 1) + column;
                int below = a + columns + 1;
                // Front faces point up on the lip and out (+Z) on the fall.
                triangles[t6++] = a;
                triangles[t6++] = below;
                triangles[t6++] = a + 1;
                triangles[t6++] = a + 1;
                triangles[t6++] = below;
                triangles[t6++] = below + 1;
            }
        }

        target.Clear();
        target.vertices = vertices;
        target.uv = uv;
        target.colors = colours;
        target.triangles = triangles;
        target.RecalculateNormals();
        target.RecalculateTangents();
        target.RecalculateBounds();
    }
}
