using UnityEngine;

/// <summary>Lets "Safe Haven/Grass" take the colour of the ground it grows on: a top-down colour map of this terrain
/// (baked by Safe Haven › World › Grass and Plants, and again whenever the terrain's paint changes) goes to the grass shader
/// as a global, with the terrain's position and size. One terrain's map is active at a time (the last enabled).
/// How much each grass material follows it: the material's Take Ground Colour / Ground Colour at the Tips.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Terrain))]
public sealed class GrassTerrainColour : MonoBehaviour
{
    [Tooltip("Top-down colour of the terrain (baked from its painted layers).")]
    [SerializeField] private Texture2D colourMap;

    private static readonly int MapId = Shader.PropertyToID("_GrassTerrainColor");
    private static readonly int RectId = Shader.PropertyToID("_GrassTerrainRect");
    private static readonly int OnId = Shader.PropertyToID("_GrassTerrainColorOn");

    public Texture2D ColourMap
    {
        get => colourMap;
        set
        {
            colourMap = value;
            Apply();
        }
    }

    public static GrassTerrainColour Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        Shader.SetGlobalFloat(OnId, 0f);
    }

    private void OnEnable() => Apply();

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
            Shader.SetGlobalFloat(OnId, 0f);
        }
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            transform.hasChanged = false;
            Apply();
        }
    }

    /// <summary>Sends this terrain's colour map and placement to the grass shader.</summary>
    public void Apply()
    {
        TerrainData data = GetComponent<Terrain>().terrainData;
        if (colourMap == null || data == null)
        {
            if (Current == this || Current == null) Shader.SetGlobalFloat(OnId, 0f);
            return;
        }

        Vector3 position = transform.position;
        Vector3 size = data.size;
        Shader.SetGlobalTexture(MapId, colourMap);
        Shader.SetGlobalVector(RectId, new Vector4(position.x, position.z, 1f / Mathf.Max(size.x, 1e-3f),
                                                   1f / Mathf.Max(size.z, 1e-3f)));
        Shader.SetGlobalFloat(OnId, 1f);
        Current = this;
    }
}
