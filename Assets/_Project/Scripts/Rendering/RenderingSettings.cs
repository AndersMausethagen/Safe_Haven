using UnityEngine;

/// <summary>
/// The numbers of the world's runtime rendering (WorldRendering): the reflection probe that follows the player and the
/// cloud shadows drifting over the ground. Lives at Resources/RenderingSettings; built-in defaults apply when it is missing.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Rendering Settings", fileName = "RenderingSettings")]
public sealed class RenderingSettings : GameSettings<RenderingSettings>
{
    [Header("Reflections")]
    [Tooltip("A realtime reflection probe that follows the player, so metal, water and wet stone reflect what is round them " +
             "instead of only the sky.")]
    [SerializeField] private bool reflections = true;
    [Tooltip("Cube face size in pixels.")]
    [SerializeField] private int reflectionResolution = 128;
    [Tooltip("Metres above the ground it is taken from: above the player's head, so they don't fill it.")]
    [SerializeField, Min(0.5f)] private float reflectionHeight = 4f;
    [Tooltip("Rendered again once the player has moved this far...")]
    [SerializeField, Min(1f)] private float reflectionMove = 8f;
    [Tooltip("...or the hour has moved on this much...")]
    [SerializeField, Min(0.01f)] private float reflectionHours = 0.25f;
    [Tooltip("...and never more often than this, in seconds (each render is spread over nine frames, a face a frame).")]
    [SerializeField, Min(0.1f)] private float reflectionInterval = 1f;
    [Tooltip("What it sees.")]
    [SerializeField] private LayerMask reflectionLayers = ~(1 << 5);

    [Header("Cloud shadows")]
    [Tooltip("Soft shadows of passing clouds on the sunlit ground, at home only: the raids keep their own look.")]
    [SerializeField] private bool cloudShadows = true;
    [Tooltip("Metres one tile of the cloud pattern covers.")]
    [SerializeField, Min(20f)] private float cloudSize = 180f;
    [Tooltip("Metres a second they drift at the wind's usual strength.")]
    [SerializeField, Min(0f)] private float cloudSpeed = 2.5f;
    [Tooltip("Share of the ground under cloud on a clear day, and under a sky that is half cloud.")]
    [SerializeField] private Vector2 cloudCover = new Vector2(0.28f, 0.6f);
    [Tooltip("How dark a cloud's shadow is in full sun (0 none, 1 black).")]
    [SerializeField, Range(0f, 1f)] private float cloudDarkness = 0.55f;
    [Tooltip("How soft the shadows' edges are.")]
    [SerializeField, Range(0.02f, 0.5f)] private float cloudSoftness = 0.18f;
    [Tooltip("Shaders/CloudCookie.shader, referenced so builds include it.")]
    [SerializeField] private Shader cloudShader;

    public bool Reflections => reflections;
    public int ReflectionResolution => Mathf.ClosestPowerOfTwo(Mathf.Clamp(reflectionResolution, 16, 1024));
    public float ReflectionHeight => reflectionHeight;
    public float ReflectionMove => reflectionMove;
    public float ReflectionHours => reflectionHours;
    public float ReflectionInterval => reflectionInterval;
    public LayerMask ReflectionLayers => reflectionLayers;
    public bool CloudShadows => cloudShadows;
    public float CloudSize => cloudSize;
    public float CloudSpeed => cloudSpeed;
    public Vector2 CloudCover => cloudCover;
    public float CloudDarkness => cloudDarkness;
    public float CloudSoftness => cloudSoftness;
    public Shader CloudShader => cloudShader != null ? cloudShader : Shader.Find("Hidden/Safe Haven/Cloud Cookie");
}
