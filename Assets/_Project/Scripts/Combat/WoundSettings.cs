using UnityEngine;

/// <summary>
/// How hurts show (CreatureWounds, CarriedGrime): armour breaking off, blood trails, and the blood and mud the player and
/// their weapon carry from a fight. Lives at Resources/WoundSettings.
/// </summary>
[CreateAssetMenu(fileName = "WoundSettings", menuName = "Safe Haven/Settings/Wounds")]
public sealed class WoundSettings : GameSettings<WoundSettings>
{
    [Header("Creatures")]
    [Tooltip("Thrown where a piece of armour breaks off (a pooled effect).")]
    [SerializeField] private GameObject armourShards;
    [SerializeField] private SoundDefinition armourChipSound;
    [Tooltip("Seconds a blood trail's drops stay on the ground.")]
    [SerializeField, Min(5f)] private float trailSeconds = 90f;

    [Header("What the player carries from a fight (CarriedGrime)")]
    [SerializeField] private Color playerBlood = new Color(0.4f, 0.02f, 0.02f, 0.95f);
    [Tooltip("The colour of the mud splashed up the legs (drawn with the blood's splatter shapes).")]
    [SerializeField] private Color mud = new Color(0.22f, 0.15f, 0.08f, 0.95f);
    [Tooltip("A decal of its own for the mud. Empty: the splatter shapes in the mud's colour.")]
    [SerializeField] private Material mudDecal;
    [Tooltip("Blood on the body from a blow taken (scaled by how hard it was).")]
    [SerializeField, Range(0f, 1f)] private float bloodPerHitTaken = 0.12f;
    [Tooltip("Blood on the blade from each blow dealt up close (some spatters the body; a kill, more).")]
    [SerializeField, Range(0f, 1f)] private float bloodPerHitDealt = 0.12f;
    [Tooltip("Mud up the legs for each metre walked on wet ground (a roll throws three times as much).")]
    [SerializeField, Range(0f, 0.2f)] private float mudPerMetre = 0.02f;
    [Tooltip("How fast heavy rain washes it off, per second at full wetness.")]
    [SerializeField, Range(0f, 0.2f)] private float rainWash = 0.02f;
    [Tooltip("How fast standing in water washes it off, per second.")]
    [SerializeField, Range(0f, 2f)] private float waterWash = 0.5f;

    public Color PlayerBlood => playerBlood;
    public Material MudDecal => mudDecal;
    public Color Mud => mud;
    public float BloodPerHitTaken => bloodPerHitTaken;
    public float BloodPerHitDealt => bloodPerHitDealt;
    public float MudPerMetre => mudPerMetre;
    public float RainWash => rainWash;
    public float WaterWash => waterWash;
    public GameObject ArmourShards => armourShards;
    public SoundDefinition ArmourChipSound => armourChipSound;
    public float TrailSeconds => trailSeconds;
}
