using UnityEngine;

/// <summary>What the minimap shows and how far (MinimapHud).</summary>
[CreateAssetMenu(fileName = "MinimapSettings", menuName = "Safe Haven/Settings/Minimap")]
public sealed class MinimapSettings : GameSettings<MinimapSettings>
{
    [Tooltip("Safe Haven/UI/Minimap, which draws the round map.")]
    [SerializeField] private Material material;
    [Tooltip("Safe Haven/UI/World Map, which draws the M map. Referenced here so builds keep it (Shader.Find alone lets " +
             "the build strip it).")]
    [SerializeField] private Shader worldMapShader;
    [Tooltip("The M map's parchment (Art/UI/Map/Map Parchment, tileable; Tools/UI/map_parchment.py).")]
    [SerializeField] private Texture2D worldMapPaper;
    [Tooltip("The M map's charcoal grain, linear data (Art/UI/Map/Map Grain: R tooth, G smudge, B slow, A fibres).")]
    [SerializeField] private Texture2D worldMapGrain;
    [Tooltip("Pixels of the painted map, per side. It is painted once per scene.")]
    [SerializeField, Range(128, 1024)] private int mapSize = 512;
    [Tooltip("The map's size on screen, in UI units.")]
    [SerializeField, Min(80f)] private float size = 236f;
    [Tooltip("How far off a creature shows by day, in metres. Breach attackers always show.")]
    [SerializeField, Min(0f)] private float creatureSight = 24f;
    [Tooltip("How far off a creature shows in the dark of night: the map knows no more than the player sees.")]
    [SerializeField, Min(0f)] private float creatureSightAtNight = 11f;
    [Tooltip("How much darker ground nobody can walk on is painted: trees, rocks, cliffs.")]
    [SerializeField, Range(0f, 1f)] private float blockedShade = 0.62f;
    [SerializeField] private Color water = new Color(0.3f, 0.45f, 0.5f, 1f);

    public Material Material => material;
    public Shader WorldMapShader => worldMapShader;
    public Texture2D WorldMapPaper => worldMapPaper;
    public Texture2D WorldMapGrain => worldMapGrain;
    public int MapSize => mapSize;
    public float Size => size;
    public float BlockedShade => blockedShade;
    public Color Water => water;

    /// <summary>How far off a creature shows at this darkness, 0..1.</summary>
    public float CreatureSight(float darkness) => Mathf.Lerp(creatureSight, creatureSightAtNight, darkness);
}
