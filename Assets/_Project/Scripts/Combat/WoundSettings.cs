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

    public GameObject ArmourShards => armourShards;
    public SoundDefinition ArmourChipSound => armourChipSound;
    public float TrailSeconds => trailSeconds;
}
