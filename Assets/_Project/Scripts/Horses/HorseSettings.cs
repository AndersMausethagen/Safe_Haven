using UnityEngine;

/// <summary>Which horse roams round the Haven (Resources/HorseSettings). Its numbers are on the HorseDefinition.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Horses/Horse Settings", fileName = "HorseSettings")]
public sealed class HorseSettings : GameSettings<HorseSettings>
{
    [Tooltip("The wild horse met round the Haven, tamed with a lasso and barley.")]
    [SerializeField] private HorseDefinition wildHorse;

    public HorseDefinition WildHorse => wildHorse;
}
