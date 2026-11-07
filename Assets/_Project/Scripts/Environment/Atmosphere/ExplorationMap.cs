using System;
using UnityEngine;

/// <summary>
/// The scene's memory of what the player has seen. It covers all of the scene's terrain tiles, explores the ground around every
/// revealing VisibilitySource a few times a second, and hands the result to the fog of war (AtmosphereFeature) as a
/// texture. At home it is saved (Persist, with a SaveableEntity beside it); in a raid it starts dark every time, as the
/// map is turned and rolled anew. One per scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExplorationMap : MonoBehaviour, ISaveable, ISharedState
{
    [Tooltip("Metres per cell. Smaller is sharper and costs more memory.")]
    [SerializeField, Min(0.25f)] private float cellSize = 1f;
    [Tooltip("Seconds between reveals.")]
    [SerializeField, Min(0.02f)] private float revealInterval = 0.2f;
    [Tooltip("Keep what was explored in the save (Safe Haven). Off: every visit starts unexplored (raids).")]
    [SerializeField] private bool persist;

    [Serializable]
    private struct SavedState
    {
        public int width;
        public int height;
        public string bits;
    }

    private ExplorationGrid grid;
    private Texture2D texture;
    private bool dirty;
    private float nextRevealAt;

    /// <summary>The map in the loaded scene, or null.</summary>
    public static ExplorationMap Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Raised with how many cells the player has just explored (for experience).</summary>
    public static event System.Action<int> Explored;

    public ExplorationGrid Grid => grid;
    public Texture2D Texture => texture;
    public string SaveKey => "exploration";

    // Each player's own in co-op.
    bool ISharedState.IsShared => false;

    int ISharedState.SharedMark => 0;

    /// <summary>Lifts the fog of war around a place the player hasn't been (a beacon, a rumour).</summary>
    public void RevealAround(Vector3 position, float radius)
    {
        dirty |= grid.Reveal(new Vector2(position.x, position.z), radius, 0.25f);
    }

    /// <summary>0–1 how explored the ground under a position is.</summary>
    public float ExploredAt(Vector3 position) => grid != null ? grid.At(new Vector2(position.x, position.z)) : 1f;

    private void Awake() => Build();

    /// <summary>Measures the ground again, for a map grown after this woke (a large raid). Nothing has been explored yet.</summary>
    public void Refit()
    {
        if (texture != null)
        {
            Destroy(texture);
        }

        Build();
    }

    private void Build()
    {
        Rect area = AreaFromTerrain();
        grid = new ExplorationGrid(area, cellSize);
        texture = new Texture2D(grid.Width, grid.Height, TextureFormat.R8, false, true)
        {
            name = "Exploration",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        dirty = true;
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }

    private void Update()
    {
        if (Time.time >= nextRevealAt)
        {
            nextRevealAt = Time.time + revealInterval;
            foreach (VisibilitySource source in VisibilitySource.Active)
            {
                if (source.Reveals)
                {
                    Vector3 position = source.transform.position;
                    dirty |= grid.Reveal(new Vector2(position.x, position.z), source.RevealRadius, source.Softness);
                }
            }
        }

        if (grid.NewlyExplored > 0)
        {
            int explored = grid.NewlyExplored;
            grid.NewlyExplored = 0;
            Explored?.Invoke(explored);
        }

        if (dirty)
        {
            dirty = false;
            texture.SetPixelData(grid.Cells, 0);
            texture.Apply(false, false);
        }
    }

    public string CaptureState()
    {
        if (!persist || grid == null)
        {
            return null;
        }

        return JsonUtility.ToJson(new SavedState { width = grid.Width, height = grid.Height, bits = grid.ToBits() });
    }

    public void RestoreState(string state)
    {
        if (!persist || grid == null || string.IsNullOrEmpty(state))
        {
            return;
        }

        SavedState saved = JsonUtility.FromJson<SavedState>(state);
        if (saved.width == grid.Width && saved.height == grid.Height && grid.FromBits(saved.bits))
        {
            dirty = true;
        }
    }

    // Every terrain tile in the scene together, or a 200 m square around this object when there is none.
    private Rect AreaFromTerrain()
    {
        bool any = false;
        float xMin = 0f, zMin = 0f, xMax = 0f, zMax = 0f;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            xMin = any ? Mathf.Min(xMin, origin.x) : origin.x;
            zMin = any ? Mathf.Min(zMin, origin.z) : origin.z;
            xMax = any ? Mathf.Max(xMax, origin.x + size.x) : origin.x + size.x;
            zMax = any ? Mathf.Max(zMax, origin.z + size.z) : origin.z + size.z;
            any = true;
        }

        if (any)
        {
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        Vector3 center = transform.position;
        return new Rect(center.x - 100f, center.z - 100f, 200f, 200f);
    }
}
