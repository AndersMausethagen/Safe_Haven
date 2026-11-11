using System;
using UnityEngine;

/// <summary>A horse's nature, rolled once like its stats. It changes how near it lets the player come, how hard it fights
/// the rope and the saddle, how often it must be fed before it is broken, and how far it runs when hurt.</summary>
public enum HorseTemperament
{
    Calm = 0,
    Skittish = 1,
    Stubborn = 2,
    Bold = 3,
}

/// <summary>What one temperament does, as multipliers on the horse's own numbers (1 = no change).</summary>
[Serializable]
public struct HorseTemperamentRules
{
    public HorseTemperament temperament;
    [Tooltip("How often horses are born with it.")]
    [Min(0f)] public float weight;
    [Tooltip("How near it lets the player come (on its shy distance).")]
    [Min(0.1f)] public float shyness;
    [Tooltip("How hard it pulls on the rope.")]
    [Min(0.1f)] public float pull;
    [Tooltip("How much fight it has (struggle stamina).")]
    [Min(0.1f)] public float fight;
    [Tooltip("Feeds it takes more (or fewer) on its breaking ride.")]
    public int feeds;
    [Tooltip("How hard it bucks and veers on its breaking ride.")]
    [Min(0f)] public float bucking;
    [Tooltip("How long it runs when hurt, before its bond takes the edge off.")]
    [Min(0f)] public float panic;
    [TextArea] public string description;

    public static HorseTemperamentRules Neutral(HorseTemperament temperament) => new HorseTemperamentRules
    {
        temperament = temperament, weight = 1f, shyness = 1f, pull = 1f, fight = 1f, feeds = 0, bucking = 1f, panic = 1f,
    };

    public static string NameOf(HorseTemperament temperament) => temperament.ToString().ToLowerInvariant();
}
