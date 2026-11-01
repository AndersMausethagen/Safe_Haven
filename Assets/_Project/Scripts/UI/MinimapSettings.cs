using UnityEngine;

/// <summary>What the minimap shows and how far (MinimapHud).</summary>
[CreateAssetMenu(fileName = "MinimapSettings", menuName = "Safe Haven/Settings/Minimap")]
public sealed class MinimapSettings : GameSettings<MinimapSettings>
{
    [Tooltip("Safe Haven/UI/Minimap, which draws the round map.")]
    [SerializeField] private Material material;
    [Tooltip("Pixels of the painted map, per side. It is painted once per scene.")]
    [SerializeField, Range(128, 1024)] private int mapSize = 512;
    [Tooltip("The map's size on screen, in UI units.")]
    [SerializeField, Min(80f)] private float size = 236f;
    [Tooltip("Metres from the player to the edge, close in.")]
    [SerializeField, Min(10f)] private float nearReach = 28f;
    [Tooltip("Metres from the player to the edge, zoomed out.")]
    [SerializeField, Min(10f)] private float farReach = 105f;
    [Tooltip("How far off a creature shows by day, in metres. Breach attackers always show.")]
    [SerializeField, Min(0f)] private float creatureSight = 24f;
    [Tooltip("How far off a creature shows in the dark of night: the map knows no more than the player sees.")]
    [SerializeField, Min(0f)] private float creatureSightAtNight = 11f;
    [Tooltip("How much darker ground nobody can walk on is painted: trees, rocks, cliffs.")]
    [SerializeField, Range(0f, 1f)] private float blockedShade = 0.62f;
    [SerializeField] private Color water = new Color(0.3f, 0.45f, 0.5f, 1f);

    public Material Material => material;
    public int MapSize => mapSize;
    public float Size => size;
    public float NearReach => nearReach;
    public float FarReach => farReach;
    public float BlockedShade => blockedShade;
    public Color Water => water;

    /// <summary>How far off a creature shows at this darkness, 0..1.</summary>
    public float CreatureSight(float darkness) => Mathf.Lerp(creatureSight, creatureSightAtNight, darkness);
}
